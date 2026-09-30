namespace SpanishFlashcards.Models;

/// <summary>Where one word is in the review schedule.</summary>
public sealed class CardState
{
    /// <summary>0 = new, 1–5 = level. Level 4+ counts as learned.</summary>
    public int Box { get; set; }

    /// <summary>Day the word is next due for review.</summary>
    public DateOnly Due { get; set; }

    /// <summary>When the word was last answered (used to avoid showing it twice in a row).</summary>
    public DateTime? LastSeen { get; set; }
}

/// <summary>
/// A deliberately simple spaced-repetition schedule (Leitner boxes).
/// "I know it" on a due card moves it up one level and pushes the next review further out:
/// level 1 → 1 day, 2 → 3 days, 3 → 7 days, 4 → 14 days, 5 → 30 days.
/// "Still learning" drops it back to level 1, due again today.
/// Answering a card that isn't due yet is practice only: it never moves the card up.
/// </summary>
public static class Srs
{
    public const int LearnedLevel = 4;
    public const int MaxLevel = 5;
    private static readonly int[] IntervalDays = [0, 1, 3, 7, 14, 30];

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static int IntervalFor(int level) => IntervalDays[Math.Clamp(level, 0, MaxLevel)];

    public static bool IsNew(CardState? s) => s is null || s.Box == 0;

    public static bool IsDue(CardState? s, DateOnly today) => s is { Box: > 0 } && s.Due <= today;

    public static bool IsLearned(CardState? s) => s is not null && s.Box >= LearnedLevel;

    /// <summary>True when an "I know it" answer would count (the card is new or due).</summary>
    public static bool Counts(CardState? s, DateOnly today) => IsNew(s) || IsDue(s, today);

    /// <summary>Apply "I know it". Returns null when the card wasn't due (practice only, nothing changes).</summary>
    public static CardState? Know(CardState? s, DateOnly today)
    {
        if (!Counts(s, today)) return null;
        var level = Math.Min((s?.Box ?? 0) + 1, MaxLevel);
        return new CardState { Box = level, Due = today.AddDays(IntervalFor(level)), LastSeen = DateTime.Now };
    }

    /// <summary>Apply "Still learning": always counts, back to level 1 and due today.</summary>
    public static CardState Forgot(DateOnly today) =>
        new() { Box = 1, Due = today, LastSeen = DateTime.Now };

    /// <summary>Used by "Mark learned" in the lists.</summary>
    public static CardState MarkLearned(DateOnly today) =>
        new() { Box = LearnedLevel, Due = today.AddDays(IntervalFor(LearnedLevel)), LastSeen = DateTime.Now };

    public static string StageLabel(CardState? s) => s switch
    {
        null or { Box: 0 } => "New",
        { Box: >= LearnedLevel } => "Learned",
        _ => $"Level {s!.Box}",
    };

    public static string DueLabel(CardState? s, DateOnly today)
    {
        if (IsNew(s)) return "not started";
        var days = s!.Due.DayNumber - today.DayNumber;
        return days switch
        {
            <= 0 => "due now",
            1 => "due tomorrow",
            < 7 => $"due in {days} days",
            _ => $"due {s.Due:MMM d}",
        };
    }
}
