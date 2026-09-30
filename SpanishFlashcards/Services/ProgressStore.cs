using System.Text.Json;
using Microsoft.JSInterop;
using SpanishFlashcards.Models;

namespace SpanishFlashcards.Services;

/// <summary>Everything the app remembers between visits.</summary>
public sealed class Progress
{
    /// <summary>Review schedule per word (keyed by the Spanish word).</summary>
    public Dictionary<string, CardState> Cards { get; set; } = new();

    /// <summary>Shuffled card order for practice mode (null = frequency order).</summary>
    public List<string>? Order { get; set; }

    public bool EnglishFirst { get; set; }

    public string? Group { get; set; }

    /// <summary>Old format ("known"/"learning" per word). Converted to <see cref="Cards"/> on load.</summary>
    public Dictionary<string, string>? Status { get; set; }
}

/// <summary>Keeps progress in the browser's localStorage so it survives reloads.</summary>
public sealed class ProgressStore(IJSRuntime js)
{
    private const string Key = "palabras-flashcards-v1";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<Progress> LoadAsync()
    {
        Progress? p = null;
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (!string.IsNullOrWhiteSpace(raw))
                p = JsonSerializer.Deserialize<Progress>(raw, Options);
        }
        catch (Exception)
        {
            // Storage blocked or data corrupted: start fresh rather than crash.
        }

        p ??= new Progress();
        MigrateOldStatus(p);
        return p;
    }

    public async Task SaveAsync(Progress progress)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(progress, Options));
        }
        catch (Exception)
        {
            // Storage unavailable (e.g. private browsing): progress lasts for this session only.
        }
    }

    /// <summary>Earlier versions only stored known / learning. Turn those into schedule entries.</summary>
    private static void MigrateOldStatus(Progress p)
    {
        if (p.Status is not { Count: > 0 }) { p.Status = null; return; }

        var today = Srs.Today;
        foreach (var (word, status) in p.Status)
        {
            if (p.Cards.ContainsKey(word)) continue;
            p.Cards[word] = status == WordStatus.Known ? Srs.MarkLearned(today) : Srs.Forgot(today);
        }
        p.Status = null;
    }
}

public static class WordStatus
{
    public const string Known = "known";
    public const string Learning = "learning";
}
