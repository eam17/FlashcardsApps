using System.Net.Http.Json;
using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Services;

/// <summary>Loads wwwroot/data/verbs.json once (made by tools/verb-data/gen.py) and builds the Verbs tree.</summary>
public sealed class VerbRepository(HttpClient http, WordRepository words)
{
    private VerbBook? _book;
    private VerbNode? _tree;

    public async Task<(VerbBook Book, VerbNode Tree)> GetAsync()
    {
        if (_book is not null && _tree is not null) return (_book, _tree);

        var file = await http.GetFromJsonAsync<VerbFile>("data/verbs.json") ?? new VerbFile();
        var ranks = (await words.GetAllAsync()).Where(w => w.Pos == "verb").ToDictionary(w => w.Es, w => w.Rank);

        // haber first: every compound tense is built from it.
        var haberDto = file.Verbs.First(v => v.Inf == "haber");
        var haber = Verb.FromDto(haberDto, ranks.GetValueOrDefault("haber", 999), null);
        var verbs = file.Verbs
            .Select(d => d.Inf == "haber" ? haber : Verb.FromDto(d, ranks.GetValueOrDefault(d.Inf, 999), haber))
            .OrderBy(v => v.Rank)
            .ToList();

        _book = new VerbBook(verbs);
        try
        {
            _book.Stories = (await http.GetFromJsonAsync<StoryFile>("data/stories.json"))?.Stories ?? [];
        }
        catch (Exception)
        {
            // No stories file: the Story tab and "In the stories" just don't appear.
        }
        _tree = VerbTree.Build(_book);
        return (_book, _tree);
    }
}
