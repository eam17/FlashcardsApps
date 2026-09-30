using System.Net.Http.Json;
using SpanishFlashcards.Models;

namespace SpanishFlashcards.Services;

/// <summary>Loads the 1,000-word list from wwwroot/data/words.json once and caches it.</summary>
public sealed class WordRepository(HttpClient http)
{
    // Shape of one entry in words.json. File order = frequency rank.
    private sealed record WordDto(
        string Es, string En, string Pos, string Ex, string ExEn,
        string? Topic, string? G, string? Art, string? Conj, string? Form);

    private IReadOnlyList<Word>? _words;

    public async Task<IReadOnlyList<Word>> GetAllAsync()
    {
        if (_words is not null) return _words;

        var rows = await http.GetFromJsonAsync<List<WordDto>>("data/words.json") ?? [];

        _words = rows
            .Select((r, i) => new Word
            {
                Es = r.Es,
                En = r.En,
                Pos = r.Pos,
                Example = r.Ex,
                ExampleEn = r.ExEn,
                Rank = i + 1,
                Topic = string.IsNullOrWhiteSpace(r.Topic) ? "Other" : r.Topic,
                Gender = r.G,
                Article = r.Art,
                Conjugation = r.Conj,
                Form = r.Form,
            })
            .ToList();

        return _words;
    }
}
