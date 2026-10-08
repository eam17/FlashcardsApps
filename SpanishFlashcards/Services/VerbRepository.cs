using System.Net.Http.Json;
using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Services;

/// <summary>Loads wwwroot/data/verbs.json once (made by tools/verb-data/gen.py) and builds the Verbs tree.</summary>
public sealed class VerbRepository(HttpClient http, WordRepository words)
{
    private VerbBook? _book;
    private VerbNode? _tree;
    private Task<(VerbBook Book, VerbNode Tree)>? loading;

    /// <summary>Loads once: callers at the same time share one load (and so one tree).</summary>
    public Task<(VerbBook Book, VerbNode Tree)> GetAsync()
    {
        if (_book is not null && _tree is not null) return Task.FromResult((_book, _tree));
        return loading ??= LoadAsync();
    }

    private async Task<(VerbBook Book, VerbNode Tree)> LoadAsync()
    {
        try { return await Load(); }
        catch
        {
            loading = null; // try again next time
            throw;
        }
    }

    private async Task<(VerbBook Book, VerbNode Tree)> Load()
    {
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
