using SpanishFlashcards.Models.Reading;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// A verb in one of your Read tab texts or songs, in a line that has an English translation: something real to
/// read in a verb session ("From your texts").
/// </summary>
public sealed record TextSpot(string Title, string Line, int Start, int Length, string English,
                              Verb Verb, string Tense, int Person, IReadOnlyList<(Verb Verb, string Tense, int Person)> Readings)
{
    /// <summary>The verb as it appears in the line ("tuvimos", "he comido").</summary>
    public string Word => Line.Substring(Start, Length);
}

/// <summary>Finds the verbs of the Verbs tab in your translated texts, the same way the Read tab does.</summary>
public static class YourTexts
{
    private static string? cacheKey;
    private static List<TextSpot> cache = new();

    /// <summary>
    /// Every verb form (from the Verbs tab's verbs) in a translated line of your texts. Worked out once and kept
    /// until a text or translation changes.
    /// </summary>
    public static List<TextSpot> Spots(IReadOnlyList<SavedText> texts, IReadOnlyList<Word> words, VerbBook book)
    {
        var translated = texts.Where(t => t.Translation is { Lines.Count: > 0 }).ToList();
        var key = string.Join(";", translated.Select(t => $"{t.Id}:{t.Text.Length}:{t.Translation!.Made.Ticks}"));
        if (key == cacheKey) return cache;

        var spots = new List<TextSpot>();
        foreach (var text in translated)
        {
            TextAnalysis a;
            try { a = TextAnalysis.Analyse(text.Text, words, _ => null, book, null, new HashSet<string>()); }
            catch (Exception) { continue; }

            var lines = text.Text.Split('\n');
            var english = text.Translation!.Lines;
            var line = 0;
            var col = 0;
            for (var i = 0; i < a.Tokens.Count; i++)
            {
                var tok = a.Tokens[i];
                if (!tok.IsWord)
                {
                    // Track the line and the column, so a verb can be found in its line.
                    foreach (var ch in tok.Text)
                    {
                        if (ch == '\n') { line++; col = 0; }
                        else col++;
                    }
                    continue;
                }
                var start = col;
                col += tok.Text.Length;
                if (tok.Continues || tok.Readings.Count == 0 || line >= lines.Length || line >= english.Count) continue;
                var en = english[line].Trim();
                if (en.Length == 0) continue;

                var readings = tok.Readings
                    .Select(r => (Verb: book.ByInf.GetValueOrDefault(r.Inf), r.Tense, r.Person))
                    .Where(r => r.Verb is not null)
                    .Select(r => (Verb: r.Verb!, r.Tense, r.Person))
                    .ToList();
                if (readings.Count == 0) continue;

                // The phrase it starts ("he comido") when it runs on in the same line.
                var phrase = tok.Phrase.Length > tok.Text.Length ? tok.Phrase : tok.Text;
                var lineText = lines[line].TrimEnd('\r');
                var length = start + phrase.Length <= lineText.Length
                             && string.Equals(lineText.Substring(start, phrase.Length), phrase, StringComparison.OrdinalIgnoreCase)
                    ? phrase.Length
                    : tok.Text.Length;
                if (start + length > lineText.Length) continue;

                var (v, t, p) = readings[0];
                spots.Add(new TextSpot(text.Title, lineText, start, length, en, v, t, p, readings));
            }
        }
        cacheKey = key;
        cache = spots;
        return spots;
    }
}
