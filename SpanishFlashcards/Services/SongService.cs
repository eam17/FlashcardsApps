using System.Net.Http.Json;
using SpanishFlashcards.Models.Reading;

namespace SpanishFlashcards.Services;

/// <summary>
/// Finds lyrics on LRCLIB (lrclib.net): free, no key, crowdsourced. Its API allows calls from any web page.
/// </summary>
public sealed class SongService(HttpClient http)
{
    private const string Api = "https://lrclib.net/api/search";

    /// <summary>Free-text search: a song, an artist, or both.</summary>
    public Task<List<LrcTrack>> SearchAsync(string query) =>
        GetAsync($"{Api}?q={Uri.EscapeDataString(query.Trim())}");

    /// <summary>A recognised song: by title and artist, then looser if that finds nothing.</summary>
    public async Task<List<LrcTrack>> FindAsync(string title, string artist)
    {
        title = title.Trim();
        artist = artist.Trim();
        if (artist.Length == 0) return await SearchAsync(title);
        var exact = await GetAsync($"{Api}?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}");
        if (exact.Any(t => t.HasLyrics)) return exact;
        var both = await SearchAsync($"{title} {artist}");
        if (both.Any(t => t.HasLyrics)) return both;
        // "Song (Remix)", "Song [Live]" or "Song - Acoustic": try the title without the extra part.
        var cut = title.IndexOfAny(['(', '[']);
        var dash = title.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0 && (cut < 0 || dash < cut)) cut = dash;
        var core = cut > 0 ? title[..cut].Trim() : "";
        return core.Length > 0 ? await SearchAsync($"{core} {artist}") : both;
    }

    private async Task<List<LrcTrack>> GetAsync(string url) =>
        Songs.Tidy(await http.GetFromJsonAsync<List<LrcTrack>>(url) ?? new());
}
