namespace SpanishFlashcards.Models.Verbs;

/// <summary>The kinds of question in a verb session.</summary>
public enum AskKind
{
    /// <summary>Make: pick the stem and the ending.</summary>
    Build,
    /// <summary>Make: verb + person + tense → spell the form.</summary>
    Cue,
    /// <summary>Make: a sentence with a gap and the infinitive, no tense label.</summary>
    Context,
    /// <summary>Make: the English ("we had") → spell the Spanish.</summary>
    English,
    /// <summary>Read: the form alone → which verb, who, when.</summary>
    Decode,
    /// <summary>Read: the form alone → its English.</summary>
    Meaning,
    /// <summary>Read: the form in a sentence → its English there.</summary>
    Sentence,
    /// <summary>Read: the form heard → its English.</summary>
    Listen,
}

/// <summary>
/// Time limits for "quick": a right answer within the limit counts towards a meter's quick step and as Good in
/// reviews; slower is right but not automatic yet (Hard). Settings → Verbs scales them (relaxed, normal, tight) or
/// turns them off (every right answer counts as quick).
/// </summary>
public static class VerbSpeed
{
    public static readonly IReadOnlyList<(string Id, string Label)> Settings =
    [
        ("relaxed", "Relaxed"), ("normal", "Normal"), ("tight", "Tight"), ("off", "Off"),
    ];

    public static double Factor(string? setting) => setting switch
    {
        "relaxed" => 1.5,
        "tight" => 0.75,
        "off" => double.PositiveInfinity,
        _ => 1.0,
    };

    /// <summary>The limit in seconds at the normal setting.</summary>
    public static double BaseSeconds(AskKind kind, string answer, bool tiles)
    {
        var letters = answer.Count(char.IsLetter);
        var spell = 2.5 + letters * (tiles ? 0.7 : 0.45);
        return kind switch
        {
            AskKind.Build => 6,
            AskKind.Cue => spell,
            AskKind.Context => spell + 3,
            AskKind.English => spell + 1.5,
            AskKind.Decode => 7,
            AskKind.Meaning => 3.5,
            AskKind.Sentence => 7,
            _ => 5,
        };
    }

    /// <summary>The limit in seconds with the setting applied (infinity when off).</summary>
    public static double Limit(AskKind kind, string answer, bool tiles, string? setting) =>
        BaseSeconds(kind, answer, tiles) * Factor(setting);

    public static bool IsFast(AskKind kind, string answer, bool tiles, double ms, string? setting) =>
        ms / 1000 <= Limit(kind, answer, tiles, setting);
}
