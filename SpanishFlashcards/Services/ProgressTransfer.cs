using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SpanishFlashcards.Models;

namespace SpanishFlashcards.Services;

/// <summary>The file format written by Export JSON and read by Import.</summary>
public sealed class ProgressExport
{
    public string App { get; set; } = "Palabras";
    public int Version { get; set; } = 2;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;

    // Easy-to-read lists
    public List<string> Learned { get; set; } = new();
    public List<string> StillLearning { get; set; } = new();
    public List<string> NotStarted { get; set; } = new();

    /// <summary>Full schedule (level + next review date) so an import restores exactly.</summary>
    public Dictionary<string, ExportCard>? Schedule { get; set; }
}

public sealed class ExportCard
{
    public int Level { get; set; }
    public DateOnly Due { get; set; }
}

public sealed record ImportResult(Dictionary<string, CardState> Cards, int Learned, int Learning, int Unknown);

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

    public static string ToJson(IEnumerable<Word> words, IReadOnlyDictionary<string, CardState> cards)
    {
        var export = new ProgressExport { Schedule = new() };
        foreach (var w in words)
        {
            cards.TryGetValue(w.Es, out var s);
            if (Srs.IsLearned(s)) export.Learned.Add(w.Es);
            else if (!Srs.IsNew(s)) export.StillLearning.Add(w.Es);
            else export.NotStarted.Add(w.Es);

            if (!Srs.IsNew(s)) export.Schedule[w.Es] = new ExportCard { Level = s!.Box, Due = s.Due };
        }
        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public static string ToCsv(IEnumerable<Word> words, IReadOnlyDictionary<string, CardState> cards)
    {
        var sb = new StringBuilder();
        sb.Append('﻿'); // BOM so Excel opens accents correctly
        sb.AppendLine("rank,spanish,english,part_of_speech,topic,status,level,next_review");
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
              .Append(s?.Box ?? 0).Append(',')
              .Append(Srs.IsNew(s) ? "" : s!.Due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
              .AppendLine();
        }
        return sb.ToString();
    }

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    // ---------- import ----------

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
        var entries = looksJson ? ReadJson(text, today) : ReadCsv(text, today);

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
            unknown);
    }

    private static List<(string Word, CardState? State)> ReadJson(string text, DateOnly today)
    {
        var data = JsonSerializer.Deserialize<ProgressExport>(text, JsonOptions)
                   ?? throw new FormatException("That JSON file isn't a Palabras export.");

        var result = new List<(string, CardState?)>();
        if (data.Schedule is { Count: > 0 })
        {
            foreach (var (w, c) in data.Schedule)
                result.Add((w, new CardState { Box = Math.Clamp(c.Level, 0, Srs.MaxLevel), Due = c.Due }));
            return result;
        }

        if (data.Learned.Count == 0 && data.StillLearning.Count == 0 && data.NotStarted.Count == 0)
            throw new FormatException("That JSON file has no \"learned\", \"stillLearning\" or \"notStarted\" lists.");

        foreach (var w in data.Learned) result.Add((w, Srs.MarkLearned(today)));
        foreach (var w in data.StillLearning) result.Add((w, Srs.Forgot(today)));
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
        var hasHeader = wordCol >= 0 && (statusCol >= 0 || levelCol >= 0);
        if (!hasHeader) { wordCol = 0; statusCol = -1; levelCol = -1; dueCol = -1; }

        foreach (var row in rows.Skip(hasHeader ? 1 : 0))
        {
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace) || wordCol >= row.Count) continue;
            var word = row[wordCol];

            // Prefer the exact level + date when present.
            if (levelCol >= 0 && levelCol < row.Count && int.TryParse(row[levelCol].Trim(), out var level))
            {
                if (level <= 0) { result.Add((word, null)); continue; }
                var due = today;
                if (dueCol >= 0 && dueCol < row.Count &&
                    DateOnly.TryParseExact(row[dueCol].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    due = d;
                result.Add((word, new CardState { Box = Math.Min(level, Srs.MaxLevel), Due = due }));
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
        "still learning" or "learning" or "practicing" or "practising" => Srs.Forgot(today),
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
