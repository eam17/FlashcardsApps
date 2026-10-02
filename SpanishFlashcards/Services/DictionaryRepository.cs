using SpanishFlashcards.Models.Reading;

namespace SpanishFlashcards.Services;

/// <summary>
/// Loads the offline dictionary (wwwroot/data/dict.tsv, about 2 MB) the first time the Read tab needs it,
/// then keeps it for as long as the app is open.
/// </summary>
public sealed class DictionaryRepository(HttpClient http)
{
    private Task<SpanishDictionary>? loading;

    public Task<SpanishDictionary> GetAsync() => loading ??= Load();

    private async Task<SpanishDictionary> Load()
    {
        try
        {
            var tsv = await http.GetStringAsync("data/dict.tsv");
            return SpanishDictionary.Parse(tsv);
        }
        catch
        {
            loading = null; // try again next time
            throw;
        }
    }
}
