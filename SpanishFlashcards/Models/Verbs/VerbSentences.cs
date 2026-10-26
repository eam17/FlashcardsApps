namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// A sentence with a gap (___) for one verb form: for making the form in context (no tense label, just the sentence,
/// its English and the infinitive) and for reading it in a sentence. <see cref="Unit"/> is the unit it was written for
/// ("" when it's worked out from the form). <see cref="Clue"/> marks the words that decide the tense (highlighted).
/// <see cref="Alt"/> lists other accepted answers, separated by "|" (*me gustaría* for *quisiera*).
/// </summary>
public sealed record VerbSentence(string Es, string Inf, int Person, string Tense, string En, string Unit,
                                  string? Clue = null, string? Alt = null);

/// <summary>The sentences (VerbSentences.Data.cs, made by tools/verb-sentences/sentences_src.py) and helpers.</summary>
public static partial class VerbSentences
{
    public const string Gap = "___";

    /// <summary>Every accepted answer: the verb's own (with the -se forms) and any listed alternatives.</summary>
    public static IReadOnlyList<string> Answers(VerbSentence s, VerbBook book)
    {
        var list = book.ByInf.TryGetValue(s.Inf, out var v) ? v.Answers(s.Tense, s.Person).ToList() : new List<string>();
        if (s.Alt is { } alt)
            list.AddRange(alt.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return list;
    }

    /// <summary>The main answer ("" if the verb is missing).</summary>
    public static string Answer(VerbSentence s, VerbBook book) =>
        book.ByInf.TryGetValue(s.Inf, out var v) ? v.Form(s.Tense, s.Person) ?? "" : "";

    /// <summary>The sentence on either side of the gap.</summary>
    public static (string Before, string After) Split(string es)
    {
        var i = es.IndexOf(Gap, StringComparison.Ordinal);
        return i < 0 ? (es, "") : (es[..i], es[(i + Gap.Length)..]);
    }

    /// <summary>The gap starts the sentence (only "¿" or "¡" before it): its word gets a capital letter.</summary>
    public static bool AtStart(string before) => before.Trim().TrimStart('¿', '¡').Length == 0;

    /// <summary>A word as it's shown at that point of the sentence: capitalised at the start.</summary>
    public static string Shown(string form, bool atStart) =>
        atStart && form.Length > 0 ? char.ToUpperInvariant(form[0]) + form[1..] : form;

    /// <summary>A clue-words sentence (the Clue words game) as a gap sentence.</summary>
    public static VerbSentence FromClue(ClueSentence c) => new(c.Es, c.Inf, c.Person, c.Tense, c.En, "", c.Clue);
}
