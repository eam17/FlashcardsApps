using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SpanishFlashcards.Models;
using SpanishFlashcards.Models.Reading;
using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Services;

/// <summary>The file format written by Export JSON and read by Import.</summary>
public sealed class ProgressExport
{
    public string App { get; set; } = "Palabras";
    public int Version { get; set; } = 5;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;

    // Easy-to-read lists
    public List<string> Learned { get; set; } = new();
    public List<string> StillLearning { get; set; } = new();
    public List<string> NotStarted { get; set; } = new();

    /// <summary>Full schedule (gap, next review date, ease…) so an import restores exactly.</summary>
    public Dictionary<string, ExportCard>? Schedule { get; set; }

    /// <summary>Verbs tab: every practised form ("verb|tense|person"), with strength, due date and mistakes.</summary>
    public Dictionary<string, ExportVerbSkill>? Verbs { get; set; }

    /// <summary>Verbs tab: latest test result per tree item (score 0 to 100, passed = 90 or more).</summary>
    public Dictionary<string, ExportVerbTest>? VerbTests { get; set; }

    /// <summary>Read tab: your texts and the words picked to study for each.</summary>
    public List<SavedText>? Texts { get; set; }

    /// <summary>Words you added from texts (cards outside the word list). Their progress is in Schedule.</summary>
    public List<MyWord>? MyWords { get; set; }

    /// <summary>Verbs tab, Path: lessons passed or placed (missing before October 2026).</summary>
    public Dictionary<string, LessonRecord>? VerbLessons { get; set; }

    /// <summary>Your target retention and schedule parameters (missing before October 2026).</summary>
    public ExportScheduler? Scheduler { get; set; }

    /// <summary>Every answer on a word card, oldest first (missing before October 2026).</summary>
    public List<ReviewEntry>? History { get; set; }
}

public sealed class ExportScheduler
{
    public double Retention { get; set; }

    /// <summary>Null = the standard parameters.</summary>
    public double[]? Parameters { get; set; }
}

public sealed class ExportVerbTest
{
    public DateTime Date { get; set; }
    public int Score { get; set; }
    public bool Passed { get; set; }
}

public sealed class ExportVerbSkill
{
    /// <summary>0 (new) to 1 (solid).</summary>
    public double Strength { get; set; }
    public DateTime Due { get; set; }
    public int Mistakes { get; set; }
    public int AccentSlips { get; set; }
    public int Reps { get; set; }
    public DateTime? Last { get; set; }
}

public sealed class ExportCard
{
    /// <summary>0 to 5, how well it's known (older versions of the app read this as the level).</summary>
    public int Level { get; set; }
    public DateOnly Due { get; set; }

    /// <summary>Marked as known: never comes back for review.</summary>
    public bool Retired { get; set; }

    /// <summary>Times forgotten in a review (for "tricky words").</summary>
    public int Lapses { get; set; }

    /// <summary>"learning", "review" or "relearning" (missing in exports before October 2026).</summary>
    public string? Phase { get; set; }

    /// <summary>Gap in days between reviews.</summary>
    public int Interval { get; set; }

    /// <summary>How fast the gap grows (2.5 = normal).</summary>
    public double Ease { get; set; }

    public DateOnly? LastReview { get; set; }

    public int Reps { get; set; }

    /// <summary>Memory: days until the chance of remembering falls to 90% (0 or missing = worked out from the gap).</summary>
    public double Stability { get; set; }

    /// <summary>Memory: 1 (easy) to 10 (hard).</summary>
    public double Difficulty { get; set; }

    /// <summary>The day of the last answer that updated the memory.</summary>
    public DateOnly? RatedOn { get; set; }
}

/// <summary><paramref name="VerbSkills"/> is null when the file has no verb progress (older exports, CSV).</summary>
public sealed record ImportResult(Dictionary<string, CardState> Cards, int Learned, int Learning, int Unknown,
                                  Dictionary<string, VerbSkill>? VerbSkills = null,
                                  Dictionary<string, VerbTestRecord>? VerbTests = null,
                                  ExportScheduler? Scheduler = null,
                                  List<ReviewEntry>? History = null);

/// <summary>Turns progress into JSON / CSV files and back.</summary>
public static class ProgressTransfer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Keep accented letters readable (á, ñ…) instead of á escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ---------- export ----------

    public static string ToJson(IEnumerable<Word> words, IReadOnlyDictionary<string, CardState> cards,
                                IReadOnlyDictionary<string, VerbSkill>? verbSkills = null,
                                IReadOnlyDictionary<string, VerbTestRecord>? verbTests = null,
                                List<SavedText>? texts = null, List<MyWord>? myWords = null,
                                ExportScheduler? scheduler = null, List<ReviewEntry>? history = null,
                                IReadOnlyDictionary<string, LessonRecord>? verbLessons = null)
    {
        var export = new ProgressExport { Schedule = new(), Scheduler = scheduler };
        if (verbLessons is { Count: > 0 }) export.VerbLessons = verbLessons.ToDictionary(kv => kv.Key, kv => kv.Value);
        if (history is { Count: > 0 }) export.History = history.OrderBy(e => e.T).ToList();
        if (texts is { Count: > 0 }) export.Texts = texts;
        if (myWords is { Count: > 0 }) export.MyWords = myWords;
        if (verbTests is { Count: > 0 })
        {
            export.VerbTests = verbTests
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => new ExportVerbTest { Date = kv.Value.Date, Score = kv.Value.Score, Passed = kv.Value.Passed });
        }
        if (verbSkills is not null)
        {
            export.Verbs = verbSkills
                .Where(kv => VerbSrs.IsSeen(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => new ExportVerbSkill
                {
                    Strength = Math.Round(kv.Value.Strength, 3),
                    Due = kv.Value.Due,
                    Mistakes = kv.Value.Mistakes,
                    AccentSlips = kv.Value.AccentSlips,
                    Reps = kv.Value.Reps,
                    Last = kv.Value.Last,
                });
        }
        foreach (var w in words)
        {
            cards.TryGetValue(w.Es, out var s);
            if (Srs.IsLearned(s)) export.Learned.Add(w.Es);
            else if (!Srs.IsNew(s)) export.StillLearning.Add(w.Es);
            else export.NotStarted.Add(w.Es);

            if (!Srs.IsNew(s)) export.Schedule[w.Es] = ToExport(s!);
        }
        return JsonSerializer.Serialize(export, JsonOptions);
    }

    private static ExportCard ToExport(CardState s) => new()
    {
        Level = Srs.Strength(s),
        Due = s.Due,
        Retired = s.Retired,
        Lapses = s.Lapses,
        Phase = s.Phase switch
        {
            CardPhase.Learning => "learning",
            CardPhase.Relearning => "relearning",
            _ => "review",
        },
        Interval = s.Interval,
        Ease = Math.Round(s.Ease, 2),
        LastReview = s.LastReview,
        Reps = s.Reps,
        Stability = Math.Round(s.Stability, 4),
        Difficulty = Math.Round(s.Difficulty, 4),
        RatedOn = s.RatedOn,
    };

    /// <summary>A card from an export: this format, or an older one with only a level (converted like saved progress).</summary>
    private static CardState? FromExport(ExportCard c, DateOnly today)
    {
        if (c.Retired)
        {
            var learned = Srs.MarkLearned(today);
            learned.Lapses = Math.Max(0, c.Lapses);
            return learned;
        }
        if (c.Phase is null && c.Interval <= 0)
            return Srs.Upgrade(new CardState { Box = Math.Clamp(c.Level, 0, 5), Due = c.Due, Lapses = Math.Max(0, c.Lapses) }, today);

        var phase = c.Phase?.Trim().ToLowerInvariant() switch
        {
            "learning" => CardPhase.Learning,
            "relearning" => CardPhase.Relearning,
            _ => CardPhase.Review,
        };
        var learning = phase != CardPhase.Review;
        return new CardState
        {
            V = Srs.Version,
            Phase = phase,
            Step = learning ? 1 : 0,
            DueAt = learning ? DateTime.UtcNow : null,
            Due = c.Due,
            Interval = Math.Clamp(c.Interval, 1, Srs.MaxInterval),
            Ease = c.Ease > 0 ? Math.Clamp(c.Ease, Srs.MinEase, Srs.StartEase) : Srs.StartEase,
            LastReview = c.LastReview,
            Lapses = Math.Max(0, c.Lapses),
            Reps = Math.Max(0, c.Reps),
            // Older exports have no memory: it's worked out from the gap at the next answer.
            Stability = c.Stability > 0 && c.Difficulty > 0 ? c.Stability : 0,
            Difficulty = c.Stability > 0 && c.Difficulty > 0 ? Math.Clamp(c.Difficulty, 1, 10) : 0,
            RatedOn = c.Stability > 0 && c.Difficulty > 0 ? c.RatedOn : null,
        };
    }

    public static string ToCsv(IEnumerable<Word> words, IReadOnlyDictionary<string, CardState> cards)
    {
        var sb = new StringBuilder();
        sb.Append('﻿'); // BOM so Excel opens accents correctly
        sb.AppendLine("rank,spanish,english,part_of_speech,topic,status,level,next_review,interval_days");
        foreach (var w in words)
        {
            cards.TryGetValue(w.Es, out var s);
            var status = Srs.IsLearned(s) ? "learned" : Srs.IsNew(s) ? "not started" : "still learning";
            sb.Append(w.Rank).Append(',')
              .Append(Csv(w.SpanishWithArticle)).Append(',')
              .Append(Csv(w.En)).Append(',')
              .Append(Csv(w.Pos)).Append(',')
              .Append(Csv(w.Topic)).Append(',')
              .Append(status).Append(',')
              .Append(Srs.Strength(s)).Append(',')
              .Append(Srs.IsNew(s) ? "" : Srs.IsRetired(s) ? "never" : Srs.IsLearning(s) ? "learning"
                      : s!.Due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
              .Append(Srs.IsNew(s) || Srs.IsRetired(s) ? "" : s!.Interval.ToString(CultureInfo.InvariantCulture))
              .AppendLine();
        }
        return sb.ToString();
    }

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    // ---------- import ----------

    /// <summary>
    /// The Read tab's texts and your own words from a JSON export (null for CSV files or older exports).
    /// Read these first: your own words have to exist before their card progress can be matched.
    /// </summary>
    public static (List<SavedText>? Texts, List<MyWord>? MyWords) ReadReading(string text, string fileName)
    {
        text = text.TrimStart('\uFEFF').Trim();
        var looksJson = text.StartsWith('{') || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        if (!looksJson) return (null, null);
        try
        {
            var data = JsonSerializer.Deserialize<ProgressExport>(text, JsonOptions);
            return (data?.Texts, data?.MyWords);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>From an Export JSON file: the verb lessons done (null when missing or a CSV).</summary>
    public static Dictionary<string, LessonRecord>? ReadLessons(string text, string fileName)
    {
        text = text.TrimStart('\uFEFF').Trim();
        var looksJson = text.StartsWith('{') || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        if (!looksJson) return null;
        try { return JsonSerializer.Deserialize<ProgressExport>(text, JsonOptions)?.VerbLessons; }
        catch (JsonException) { return null; }
    }

    /// <summary>From an Export JSON file: the schedule settings and the review history (null when missing or a CSV).</summary>
    public static (ExportScheduler? Scheduler, List<ReviewEntry>? History) ReadHistory(string text, string fileName)
    {
        text = text.TrimStart('\uFEFF').Trim();
        var looksJson = text.StartsWith('{') || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        if (!looksJson) return (null, null);
        try
        {
            var data = JsonSerializer.Deserialize<ProgressExport>(text, JsonOptions);
            var history = data?.History?.Where(e => !string.IsNullOrEmpty(e.W) && e.T > 0 && e.R is >= 0 and <= 4).ToList();
            return (data?.Scheduler, history);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>Reads a file made by Export JSON or Export CSV (or a similar CSV you edited yourself).</summary>
    public static ImportResult Import(string text, string fileName, IEnumerable<Word> words)
    {
        text = text.TrimStart('﻿').Trim();
        if (text.Length == 0) throw new FormatException("The file is empty.");

        // Match words ignoring case, spaces and a leading article ("la casa" → "casa").
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words)
        {
            lookup.TryAdd(w.Es.Trim(), w.Es);
            lookup.TryAdd(w.SpanishWithArticle.Trim(), w.Es);
        }

        var today = Srs.Today;
        var looksJson = text.StartsWith('{') || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        Dictionary<string, VerbSkill>? verbSkills = null;
        Dictionary<string, VerbTestRecord>? verbTests = null;
        var entries = looksJson ? ReadJson(text, today, out verbSkills, out verbTests) : ReadCsv(text, today);

        var cards = new Dictionary<string, CardState>();
        var unknown = 0;
        foreach (var (word, state) in entries)
        {
            if (state is null) continue;
            if (!lookup.TryGetValue(word.Trim(), out var id)) { unknown++; continue; }
            cards[id] = state;
        }

        return new ImportResult(
            cards,
            cards.Values.Count(Srs.IsLearned),
            cards.Values.Count(c => !Srs.IsLearned(c) && !Srs.IsNew(c)),
            unknown,
            verbSkills,
            verbTests);
    }

    private static List<(string Word, CardState? State)> ReadJson(string text, DateOnly today,
                                                                  out Dictionary<string, VerbSkill>? verbSkills,
                                                                  out Dictionary<string, VerbTestRecord>? verbTests)
    {
        var data = JsonSerializer.Deserialize<ProgressExport>(text, JsonOptions)
                   ?? throw new FormatException("That JSON file isn't a Palabras export.");

        verbSkills = data.Verbs?.ToDictionary(kv => kv.Key, kv => new VerbSkill
        {
            Strength = Math.Clamp(kv.Value.Strength, 0, 1),
            Due = kv.Value.Due.Kind == DateTimeKind.Local ? kv.Value.Due.ToUniversalTime() : kv.Value.Due,
            Mistakes = Math.Max(0, kv.Value.Mistakes),
            AccentSlips = Math.Max(0, kv.Value.AccentSlips),
            Reps = Math.Max(1, kv.Value.Reps),
            Last = kv.Value.Last,
        });
        verbTests = data.VerbTests?.ToDictionary(kv => kv.Key, kv => new VerbTestRecord
        {
            Date = kv.Value.Date.Kind == DateTimeKind.Local ? kv.Value.Date.ToUniversalTime() : kv.Value.Date,
            Score = Math.Clamp(kv.Value.Score, 0, 100),
            Passed = kv.Value.Passed,
        });

        var result = new List<(string, CardState?)>();
        if (data.Schedule is { Count: > 0 })
        {
            foreach (var (w, c) in data.Schedule)
                result.Add((w, FromExport(c, today)));
            return result;
        }

        if (data.Learned.Count == 0 && data.StillLearning.Count == 0 && data.NotStarted.Count == 0 && verbSkills is null)
            throw new FormatException("That JSON file has no \"learned\", \"stillLearning\" or \"notStarted\" lists.");

        foreach (var w in data.Learned) result.Add((w, Srs.MarkLearned(today)));
        foreach (var w in data.StillLearning) result.Add((w, Srs.StillLearning(today)));
        return result;
    }

    private static List<(string Word, CardState? State)> ReadCsv(string text, DateOnly today)
    {
        var result = new List<(string, CardState?)>();
        var rows = ParseCsv(text);
        if (rows.Count == 0) return result;

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var wordCol = header.FindIndex(h => h is "spanish" or "español" or "espanol" or "word");
        var statusCol = header.FindIndex(h => h == "status");
        var levelCol = header.FindIndex(h => h == "level");
        var dueCol = header.FindIndex(h => h is "next_review" or "due");
        var gapCol = header.FindIndex(h => h is "interval_days" or "interval");
        var hasHeader = wordCol >= 0 && (statusCol >= 0 || levelCol >= 0);
        if (!hasHeader) { wordCol = 0; statusCol = -1; levelCol = -1; dueCol = -1; gapCol = -1; }

        foreach (var row in rows.Skip(hasHeader ? 1 : 0))
        {
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace) || wordCol >= row.Count) continue;
            var word = row[wordCol];

            // Prefer the exact level + date when present.
            if (levelCol >= 0 && levelCol < row.Count && int.TryParse(row[levelCol].Trim(), out var level))
            {
                if (level <= 0) { result.Add((word, null)); continue; }
                if (dueCol >= 0 && dueCol < row.Count && row[dueCol].Trim().Equals("never", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add((word, Srs.MarkLearned(today)));
                    continue;
                }
                var due = today;
                if (dueCol >= 0 && dueCol < row.Count &&
                    DateOnly.TryParseExact(row[dueCol].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    due = d;
                // This version's CSV has the gap in days; older ones only the level (converted like saved progress).
                if (gapCol >= 0 && gapCol < row.Count && int.TryParse(row[gapCol].Trim(), out var gap) && gap > 0)
                {
                    result.Add((word, new CardState
                    {
                        V = Srs.Version,
                        Phase = CardPhase.Review,
                        Interval = Math.Min(gap, Srs.MaxInterval),
                        Due = due,
                        LastReview = due.AddDays(-gap) <= today ? due.AddDays(-gap) : today,
                    }));
                    continue;
                }
                result.Add((word, Srs.Upgrade(new CardState { Box = Math.Min(level, 5), Due = due }, today)));
                continue;
            }

            var sIdx = statusCol >= 0 ? statusCol : row.Count - 1;
            if (sIdx >= row.Count) continue;
            result.Add((word, ParseStatus(row[sIdx], today)));
        }
        return result;
    }

    private static CardState? ParseStatus(string raw, DateOnly today) => raw.Trim().ToLowerInvariant() switch
    {
        "learned" or "known" or "yes" or "done" or "aprendida" or "aprendido" => Srs.MarkLearned(today),
        "still learning" or "learning" or "practicing" or "practising" => Srs.StillLearning(today),
        _ => null,
    };

    /// <summary>Minimal RFC 4180 CSV reader (quoted fields, doubled quotes, newlines inside quotes).</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                    break;
                default: field.Append(c); break;
            }
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
