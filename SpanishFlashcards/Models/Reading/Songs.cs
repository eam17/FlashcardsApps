using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SpanishFlashcards.Models.Reading;

/// <summary>A song from LRCLIB (lrclib.net, a free crowdsourced lyrics database), as its search returns it.</summary>
public sealed class LrcTrack
{
    public long Id { get; set; }
    public string? TrackName { get; set; }
    public string? ArtistName { get; set; }
    public string? AlbumName { get; set; }
    public double? Duration { get; set; }
    public bool Instrumental { get; set; }
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }

    /// <summary>The lyrics as plain text: the plain version, or the timed one without its times.</summary>
    [JsonIgnore]
    public string? Lyrics =>
        !string.IsNullOrWhiteSpace(PlainLyrics) ? PlainLyrics.Trim()
        : !string.IsNullOrWhiteSpace(SyncedLyrics) ? Songs.StripTimes(SyncedLyrics)
        : null;

    [JsonIgnore]
    public bool HasLyrics => !Instrumental && !string.IsNullOrWhiteSpace(Lyrics);
}

/// <summary>What AudD (audd.io) answers to a recording.</summary>
public sealed class AuddResponse
{
    public string? Status { get; set; }
    public AuddResult? Result { get; set; }
    public AuddError? Error { get; set; }
}

public sealed class AuddResult
{
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }

    /// <summary>Where in the song the recording was ("01:23").</summary>
    public string? Timecode { get; set; }

    [JsonPropertyName("song_link")]
    public string? SongLink { get; set; }
}

public sealed class AuddError
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>wwwroot/js/songs.js record(): a clip waiting to be sent, or why there isn't one.</summary>
public sealed class ClipResult
{
    public bool Ok { get; set; }
    public string? Id { get; set; }
    public string? Error { get; set; }
}

/// <summary>wwwroot/js/songs.js send(): AudD's answer as text, or why it didn't arrive.</summary>
public sealed class SendResult
{
    public bool Ok { get; set; }
    public int Status { get; set; }
    public string? Body { get; set; }
    public string? Error { get; set; }
}

public static partial class Songs
{
    [GeneratedRegex(@"\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\]\s?")]
    private static partial Regex TimeTag();

    /// <summary>"[01:23.45] Hola" → "Hola" (lines that are only a time become blank lines).</summary>
    public static string StripTimes(string synced) =>
        string.Join('\n', synced.Replace("\r", "").Split('\n').Select(l => TimeTag().Replace(l, "").TrimEnd())).Trim();

    /// <summary>3:45</summary>
    public static string DurationText(double? seconds) =>
        seconds is > 0 ? $"{(int)(seconds.Value / 60)}:{(int)(seconds.Value % 60):00}" : "";

    /// <summary>
    /// One result per song (the same song is often there for several albums): the first one with lyrics,
    /// songs with lyrics first. Keeps LRCLIB's order otherwise.
    /// </summary>
    public static List<LrcTrack> Tidy(IEnumerable<LrcTrack> tracks, int max = 12)
    {
        var seen = new Dictionary<string, LrcTrack>();
        var order = new List<string>();
        foreach (var t in tracks)
        {
            var key = $"{Key(t.TrackName)}|{Key(t.ArtistName)}";
            if (!seen.TryGetValue(key, out var have))
            {
                seen[key] = t;
                order.Add(key);
            }
            else if (!have.HasLyrics && t.HasLyrics) seen[key] = t;
        }
        return order.Select(k => seen[k]).OrderByDescending(t => t.HasLyrics).Take(max).ToList();
    }

    private static string Key(string? s) => (s ?? "").Trim().ToLowerInvariant();
}
