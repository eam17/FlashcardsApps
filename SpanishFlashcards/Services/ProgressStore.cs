using System.Text.Json;
using Microsoft.JSInterop;
using SpanishFlashcards.Models;
using SpanishFlashcards.Models.Reading;
using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Services;

/// <summary>Everything the app remembers between visits.</summary>
public sealed class Progress
{
    /// <summary>Review schedule per word (keyed by the Spanish word).</summary>
    public Dictionary<string, CardState> Cards { get; set; } = new();

    /// <summary>Shuffled card order for practice mode (null = frequency order).</summary>
    public List<string>? Order { get; set; }

    /// <summary>Practice mode only: "auto" (follow the card's level), "es" (Spanish first) or "en" (English first).</summary>
    public string PracticeDirection { get; set; } = "auto";

    public string? Group { get; set; }

    /// <summary>Most reviews you HAVE to do per day (0 = no limit). Anything beyond is optional extra.</summary>
    public int DailyReviewLimit { get; set; } = 30;

    /// <summary>Answer time limit in seconds when answering in English (0 = no timer). Slower right answers don't move up.</summary>
    public int EnglishAnswerSeconds { get; set; } = 4;

    /// <summary>Answer time limit in seconds when answering in Spanish (0 = no timer).</summary>
    public int SpanishAnswerSeconds { get; set; } = 6;

    /// <summary>Day the review counter below belongs to.</summary>
    public DateOnly? ReviewDay { get; set; }

    /// <summary>Due reviews answered on <see cref="ReviewDay"/>.</summary>
    public int ReviewsDoneToday { get; set; }

    /// <summary>Recall cards: type the Spanish and have it checked (true), or say it and grade yourself.</summary>
    public bool TypeAnswers { get; set; } = true;

    /// <summary>
    /// How you answer in Spanish on recall cards: "tiles" (tap letter buttons), "type" (the keyboard) or "self"
    /// (say it and grade yourself). Null on older progress: tiles if <see cref="TypeAnswers"/> was on, else self.
    /// </summary>
    public string? SpanishAnswer { get; set; }

    /// <summary>Words learned today come back once more a few hours later for a quick recall.</summary>
    public bool CheckIns { get; set; } = true;

    /// <summary>The chance of remembering a word that reviews are planned for (0.9 = 90%). Higher means more reviews.</summary>
    public double Retention { get; set; } = Fsrs.DefaultRetention;

    /// <summary>Schedule parameters fitted to your own review history (null = the standard ones).</summary>
    public double[]? FsrsParameters { get; set; }

    /// <summary>Songs sent to AudD from this device (to keep an eye on the 300 free ones).</summary>
    public int AuddUses { get; set; }

    /// <summary>The Claude model that translates texts (Settings → Translation with Claude).</summary>
    public string TranslateModel { get; set; } = ClaudeService.DefaultModel;

    /// <summary>Translations made with Claude from this device, and roughly what they cost (US dollars).</summary>
    public int ClaudeTranslations { get; set; }
    public double ClaudeSpend { get; set; }

    /// <summary>Stories Claude wrote with your words (Read tab), from this device.</summary>
    public int ClaudeStories { get; set; }

    /// <summary>New words a day before Cards says you're done for today (0 = no goal). You can always learn more.</summary>
    public int DailyNewLimit { get; set; } = 10;

    /// <summary>New words answered for the first time on <see cref="ReviewDay"/>.</summary>
    public int NewDoneToday { get; set; }

    /// <summary>Best scores per game id (e.g. "match-race").</summary>
    public Dictionary<string, int> GameBests { get; set; } = new();

    /// <summary>Verbs tab: how well you know each form, keyed "verb|tense|person" (patterns as "~ar|pres|yo").</summary>
    public Dictionary<string, VerbSkill> VerbSkills { get; set; } = new();

    /// <summary>Verbs tab: whose turn it is among the regular verbs used to practise each pattern.</summary>
    public Dictionary<string, int> VerbRotation { get; set; } = new();

    /// <summary>Verbs tab: include vosotros forms in tables, practice and tests (Settings → Verbs).</summary>
    public bool ShowVosotros { get; set; } = true;

    /// <summary>Verbs tab: latest test result per tree item (keyed by item id, e.g. "t:pres/stem/o-ue").</summary>
    public Dictionary<string, VerbTestRecord> VerbTests { get; set; } = new();

    /// <summary>Verbs tab, Path: lessons passed or skipped by the placement check (keyed by item id).</summary>
    public Dictionary<string, LessonRecord> VerbLessons { get; set; } = new();

    /// <summary>Verbs tab: the placement check was taken (finished or stopped).</summary>
    public bool VerbPlacementDone { get; set; }

    /// <summary>Verb forms due for review also come up on the Cards tab, after your words.</summary>
    public bool VerbsInCards { get; set; } = true;

    /// <summary>Read tab: texts you pasted, with the words you chose to study for each.</summary>
    public List<SavedText> Texts { get; set; } = new();

    /// <summary>Words you added from texts that aren't in the word list (they become cards in "My words").</summary>
    public List<MyWord> MyWords { get; set; } = new();

    /// <summary>Verbs tab, Decode: the tenses you picked (null = Smart: the first stage plus what you've practised).</summary>
    public List<string>? DecodeTenses { get; set; }

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
        string? raw = null;
        try
        {
            raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                p = JsonSerializer.Deserialize<Progress>(raw, Options);
                // Keep a copy of the last progress that loaded fine, in case a future version misreads it.
                await js.InvokeVoidAsync("localStorage.setItem", Key + "-backup", raw);
            }
        }
        catch (Exception)
        {
            // Data couldn't be read (e.g. a future format change). Never throw it away:
            // stash it under a separate key before the app starts saving fresh progress.
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try { await js.InvokeVoidAsync("localStorage.setItem", $"{Key}-unreadable-{DateTime.Now:yyyyMMddHHmmss}", raw); }
                catch (Exception) { }
            }
        }

        p ??= new Progress();
        Fsrs.Configure(p.FsrsParameters, p.Retention);
        MigrateOldStatus(p);
        if (p.Cards.Values.Any(c => c.V < Srs.Version))
        {
            // Converting from the old schedule (October 2026): keep a copy of the progress as it was, once.
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    // Before the first schedule change (levels → gaps), and before the switch to FSRS.
                    var keep = Key + (p.Cards.Values.Any(c => c.V < 2) ? "-before-new-schedule" : "-before-fsrs");
                    if (string.IsNullOrEmpty(await js.InvokeAsync<string?>("localStorage.getItem", keep)))
                        await js.InvokeVoidAsync("localStorage.setItem", keep, raw);
                }
                catch (Exception) { }
            }
            UpgradeCards(p.Cards);
            await SaveAsync(p);
        }
        return p;
    }

    /// <summary>
    /// Converts cards from the old schedule (levels) to the current one. Marked-as-known words stay marked,
    /// and every word keeps its next review date. Old "not started" entries are dropped (nothing to keep).
    /// </summary>
    public static void UpgradeCards(Dictionary<string, CardState> cards)
    {
        var today = Srs.Today;
        foreach (var (word, card) in cards.ToList())
        {
            CardState? upgraded;
            try { upgraded = Srs.Upgrade(card, today); }
            catch (Exception)
            {
                // Couldn't convert: keep a learned word learned, and a newer card as it is (its memory is
                // worked out from its gap at the next answer).
                if (card.Retired) upgraded = Srs.MarkLearned(today);
                else if (card.V >= 2) { card.V = Srs.Version; upgraded = card; }
                else upgraded = Srs.StillLearning(today);
            }
            if (upgraded is null) cards.Remove(word);
            else cards[word] = upgraded;
        }
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

    // ---------- API keys (Settings → Song recognition, Translation with Claude) ----------
    // Each kept under its own key, outside the progress, so it never ends up in a backup or an export file.

    private const string AuddKeyName = "palabras-audd-key";
    private const string ClaudeKeyName = "palabras-claude-key";

    public Task<string?> GetAuddKeyAsync() => GetSecretAsync(AuddKeyName);
    public Task<bool> SetAuddKeyAsync(string? key) => SetSecretAsync(AuddKeyName, key);
    public Task<string?> GetClaudeKeyAsync() => GetSecretAsync(ClaudeKeyName);
    public Task<bool> SetClaudeKeyAsync(string? key) => SetSecretAsync(ClaudeKeyName, key);

    private async Task<string?> GetSecretAsync(string name)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", name); }
        catch (Exception) { return null; }
    }

    /// <summary>Saves a key (empty removes it). False when the browser wouldn't store it.</summary>
    private async Task<bool> SetSecretAsync(string name, string? key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key)) await js.InvokeVoidAsync("localStorage.removeItem", name);
            else await js.InvokeVoidAsync("localStorage.setItem", name, key.Trim());
            return true;
        }
        catch (Exception) { return false; }
    }

    // ---------- review history (IndexedDB) ----------

    /// <summary>Adds one answer to the history. Never throws: the history is a bonus, not needed to study.</summary>
    public async Task LogAsync(ReviewEntry entry)
    {
        try { await js.InvokeAsync<bool>("palabrasLog.add", entry); }
        catch (Exception) { }
    }

    public async Task<List<ReviewEntry>> ReadLogAsync()
    {
        try { return await js.InvokeAsync<List<ReviewEntry>>("palabrasLog.all") ?? new(); }
        catch (Exception) { return new(); }
    }

    /// <summary>Replaces the whole history (Import). False when it couldn't be saved.</summary>
    public async Task<bool> ReplaceLogAsync(List<ReviewEntry> entries)
    {
        try { return await js.InvokeAsync<bool>("palabrasLog.replace", entries); }
        catch (Exception) { return false; }
    }

    public async Task ClearLogAsync()
    {
        try { await js.InvokeAsync<bool>("palabrasLog.clear"); }
        catch (Exception) { }
    }

    /// <summary>Earlier versions only stored known / learning. Turn those into schedule entries.</summary>
    private static void MigrateOldStatus(Progress p)
    {
        if (p.Status is not { Count: > 0 }) { p.Status = null; return; }

        var today = Srs.Today;
        foreach (var (word, status) in p.Status)
        {
            if (p.Cards.ContainsKey(word)) continue;
            p.Cards[word] = status == WordStatus.Known ? Srs.MarkLearned(today) : Srs.StillLearning(today);
        }
        p.Status = null;
    }
}

public static class WordStatus
{
    public const string Known = "known";
    public const string Learning = "learning";
}
