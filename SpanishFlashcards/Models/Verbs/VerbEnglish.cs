namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// What a form means in English, for the "What does it mean?" game: tuvimos → "we had", hablaré → "I will
/// speak", comía → "I used to eat". Built from each verb's main meaning (VerbEnglish.Data.cs, made by
/// tools/verb-english/gen_english.py) and one English pattern per tense.
/// </summary>
public static partial class VerbEnglish
{
    /// <summary>
    /// Verbs like gustar: the Spanish subject is the thing that pleases (me gusta = it pleases me), so
    /// "I ___" would be the wrong way round. Left out of the game.
    /// </summary>
    private static readonly HashSet<string> Backwards =
        ["gustar", "encantar", "importar", "doler", "faltar", "sobrar", "molestar", "preocupar"];

    public static bool Has(Verb v) => !v.OnlyThird && !Backwards.Contains(v.Inf) && Data.ContainsKey(v.Inf);

    public static string Subject(string group) => group switch
    {
        "yo" => "I",
        "tu" => "you",
        "el" => "he/she",
        "nos" => "we",
        "vos" => "you all",
        _ => "they",
    };

    private static string BeNow(string group) => group switch
    {
        "yo" => "am",
        "el" => "is",
        _ => "are",
    };

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>The English for one form, or null if the verb has no English here.</summary>
    public static string? Meaning(Verb v, string t, int p)
    {
        if (!Has(v) || !v.HasForm(t, p)) return null;
        var d = Data[v.Inf];
        var (b, s, past, pp) = (d[0], d[1], d[2], d[3]);
        var person = VerbGrammar.PersonsFor(t)[p];

        if (t == "cmd")
        {
            return person.Id switch
            {
                "tu" => $"{Cap(b)}! (tú)",
                "tu-neg" => $"Don't {b}! (tú)",
                "usted" => $"{Cap(b)}! (usted)",
                "nos" => $"Let's {b}!",
                "vos" => $"{Cap(b)}! (vosotros)",
                "vos-neg" => $"Don't {b}! (vosotros)",
                _ => $"{Cap(b)}! (ustedes)",
            };
        }

        var g = person.Group;
        var subj = Subject(g);
        var third = g == "el";
        // "be", "be able to", "be born": am / is / are and was / were.
        var isBe = b == "be" || b.StartsWith("be ", StringComparison.Ordinal);
        var rest = isBe ? b[2..] : "";
        var present = isBe ? BeNow(g) + rest : third ? s : b;
        var simplePast = isBe ? (g is "yo" or "el" ? "was" : "were") + rest : past;
        var have = third ? "has" : "have";

        return t switch
        {
            "pres" => $"{subj} {present}",
            "pret" => $"{subj} {simplePast}",
            "impf" => $"{subj} used to {b}",
            "fut" => $"{subj} will {b}",
            "cond" => $"{subj} would {b}",
            "near" => $"{subj} {BeNow(g)} going to {b}",
            "perf" => $"{subj} {have} {pp}",
            "plup" => $"{subj} had {pp}",
            "futperf" => $"{subj} will have {pp}",
            "condperf" => $"{subj} would have {pp}",
            "subj" => $"(that) {subj} {present}",
            "impsubj" => $"(if) {subj} {(isBe ? "were" + rest : past)}",
            "subjperf" => $"(that) {subj} {have} {pp}",
            "plupsubj" => $"(if) {subj} had {pp}",
            _ => null,
        };
    }
}
