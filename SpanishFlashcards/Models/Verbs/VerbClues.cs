namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// A sentence for the "Clue words" game: a time word or phrase (ayer, de niño, ojalá…) decides which tense
/// the missing verb takes. Es has ___ where the verb goes.
/// </summary>
public sealed record ClueSentence(string Es, string Inf, int Person, string Tense, string Clue, string En, string Why);

/// <summary>The sentences live in VerbClues.Data.cs (made by tools/verb-games/clues_src.py, which checks them).</summary>
public static partial class VerbClues
{
    /// <summary>The sentence split into pieces, with the clue words marked (for highlighting).</summary>
    public static List<(string Text, bool Clue)> Highlight(string text, string clue)
    {
        var parts = clue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pieces = new List<(string, bool)>();
        var i = 0;
        while (i < text.Length)
        {
            var next = -1;
            var len = 0;
            foreach (var part in parts)
            {
                var at = text.IndexOf(part, i, StringComparison.OrdinalIgnoreCase);
                if (at >= 0 && (next < 0 || at < next)) { next = at; len = part.Length; }
            }
            if (next < 0) { pieces.Add((text[i..], false)); break; }
            if (next > i) pieces.Add((text[i..next], false));
            pieces.Add((text.Substring(next, len), true));
            i = next + len;
        }
        return pieces;
    }
}
