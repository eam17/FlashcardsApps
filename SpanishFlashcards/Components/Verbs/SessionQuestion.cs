using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>Why a question is asked: it decides what the answer updates.</summary>
public enum Purpose
{
    /// <summary>Working through the current unit.</summary>
    Learn,
    /// <summary>The check at the start of a unit.</summary>
    Check,
    /// <summary>The rule tried on a verb you haven't met (or a trap).</summary>
    Predict,
    /// <summary>A review item that's due: its answer updates the review memory.</summary>
    Review,
    /// <summary>A question you missed, asked once more a little later (practice only).</summary>
    Retry,
    /// <summary>A real sentence with a form you've learned.</summary>
    Real,
    /// <summary>Against the clock, for a unit that's solid but not automatic yet.</summary>
    Quick,
    /// <summary>The placement check.</summary>
    Placement,
}

/// <summary>
/// One question in a verb session (see SessionRound): what's asked, about which form, and everything the screen
/// needs to show it. Made by SessionRound; immutable apart from the letter tiles, which are made on first show.
/// </summary>
public sealed record SessionQuestion
{
    private static int counter;

    public static int NextId() => ++counter;

    /// <summary>Unique per question shown (a retry gets a new one), for the timer and focus.</summary>
    public int Id { get; init; } = NextId();

    public required AskKind Kind { get; init; }
    public required Purpose Purpose { get; init; }
    public VerbUnit? Unit { get; init; }

    /// <summary>The review item a Review question answers for.</summary>
    public string? ReviewKey { get; init; }

    public Meter Meter => Kind is AskKind.Build or AskKind.Cue or AskKind.Context or AskKind.English ? Meter.Make : Meter.Read;

    // ---- the form

    public required Verb Verb { get; init; }
    public required string Tense { get; init; }
    public required int Person { get; init; }

    /// <summary>The form (the main answer when making it; what's shown when reading it), without the "no" of a don't-command.</summary>
    public required string Form { get; init; }

    /// <summary>Every accepted answer (making).</summary>
    public IReadOnlyList<string> Answers { get; init; } = [];

    /// <summary>The practice skill (VerbSkills) a Make answer updates: a pattern key or the verb's own key.</summary>
    public string? SkillKey { get; init; }

    public PersonInfo PersonInfo => VerbGrammar.PersonsFor(Tense)[Person];

    public bool Negative => PersonInfo.Negative;

    /// <summary>"tener|pret|yo": the form itself, whichever verb a pattern was asked with.</summary>
    public string FormKey => $"{Verb.Inf}|{Tense}|{PersonInfo.Id}";

    /// <summary>The form as it's read: "no hables" for a don't-command.</summary>
    public string Shown => (Negative ? "no " : "") + Form;

    // ---- making it from a cue

    /// <summary>Who, as the cue says it: "usted", "ellas", "nosotros".</summary>
    public string PersonLabel { get; init; } = "";

    /// <summary>A verb you hadn't met in this unit (trying the rule).</summary>
    public bool Unseen { get; init; }

    /// <summary>A form where the group's change doesn't happen (pensamos).</summary>
    public bool Trap { get; init; }

    // ---- sentences: a gap to fill, or a form to read in its sentence

    /// <summary>The sentence before the gap (or the form, when reading).</summary>
    public string? Before { get; init; }

    public string? After { get; init; }

    /// <summary>The infinitive shown with a gap: "(tener)". Null for a set phrase.</summary>
    public string? Hint { get; init; }

    /// <summary>English: the prompt (English to Spanish) or the sentence's translation.</summary>
    public string? English { get; init; }

    /// <summary>The words that decide the tense, highlighted (clue sentences).</summary>
    public string? Clue { get; init; }

    /// <summary>Where a sentence comes from: "From your text: Despacito".</summary>
    public string? Source { get; init; }

    /// <summary>The form in the sentence starts it: shown with a capital letter.</summary>
    public bool AtStart { get; init; }

    // ---- building it from pieces

    public BuildChunks? Build { get; init; }

    // ---- decoding it

    public DecodeItem? Decode { get; init; }
    public IReadOnlyList<Verb> VerbChoices { get; init; } = [];
    public IReadOnlyList<string> WhenChoices { get; init; } = [];

    /// <summary>Show vosotros among the persons even when it's off (recognising it, read-only unit).</summary>
    public bool AllPersons { get; init; }

    // ---- meaning (alone, in a sentence, heard)

    public MeaningChoices? Meaning { get; init; }

    /// <summary>The options are what the whole sentence means (a set phrase), not the verb alone.</summary>
    public bool WholeSentence { get; init; }

    // ---- spelling

    /// <summary>More decoy tiles (reviews and quick questions).</summary>
    public int ExtraDecoys { get; init; }

    /// <summary>Letter tiles for this question, made the first time they're needed.</summary>
    public TileState? Tiles { get; set; }

    /// <summary>The same question once more (a retry): new id, fresh tiles.</summary>
    public SessionQuestion Again(Purpose purpose) => this with { Id = NextId(), Purpose = purpose, Tiles = null, ReviewKey = null };
}
