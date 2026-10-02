using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// One round of decoding: for each form, pick the verb, who and when, then see the answer and the clues.
/// Kept in memory (like practice rounds) so you can look at the cheat sheet and come back.
/// Progress is saved per form under "dec:verb|tense|person".
/// </summary>
public sealed class DecodeRound
{
    public sealed record Result(DecodeItem Item, Grade Grade, string Verb, string Who, string When);

    public DecodeRound(VerbDecode decode, VerbDecode.Mode mode, bool strangeOnly, IReadOnlyCollection<string> tenses, Progress progress)
    {
        Decode = decode;
        Mode = mode;
        StrangeOnly = strangeOnly;
        Tenses = tenses.ToList();
        Progress = progress;
        Items = decode.BuildSession(mode, strangeOnly, progress.VerbSkills, Tenses);
        Ask();
    }

    private VerbDecode Decode { get; }
    private Progress Progress { get; }
    public VerbDecode.Mode Mode { get; }
    public bool StrangeOnly { get; }

    /// <summary>The tenses this round was built from.</summary>
    public IReadOnlyList<string> Tenses { get; }

    public List<DecodeItem> Items { get; }
    public List<Result> Results { get; } = new();
    public int Index { get; private set; }
    public bool Done => Index >= Items.Count;
    public DecodeItem? Item => Done ? null : Items[Index];

    public List<Verb> VerbChoices { get; private set; } = new();
    public List<string> WhenChoices { get; private set; } = new();

    public string? Verb { get; set; }
    public string? Who { get; set; }
    public string? When { get; set; }

    public bool Answered { get; private set; }
    public Grade Grade { get; private set; }

    public bool Ready => Verb is not null && Who is not null && When is not null;

    private void Ask()
    {
        Verb = Who = When = null;
        Answered = false;
        if (Item is not { } item) return;
        VerbChoices = Decode.VerbOptions(item);
        WhenChoices = VerbDecode.WhenOptions(item, Tenses);
    }

    public bool Check()
    {
        if (Answered || Item is not { } item || !Ready) return false;
        Grade = VerbDecode.GradeOf(item, Verb!, Who!, When!);
        Answered = true;
        Progress.VerbSkills.TryGetValue(item.Key, out var before);
        Progress.VerbSkills[item.Key] = VerbSrs.Apply(before, Grade, typed: true, DateTime.UtcNow);
        Results.Add(new Result(item, Grade, Verb!, Who!, When!));
        return true;
    }

    public void Next()
    {
        Index++;
        Ask();
    }

    public string PositionText => $"{Math.Min(Index + 1, Items.Count)} of {Items.Count}";
}
