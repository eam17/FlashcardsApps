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

    /// <summary>Marked as known from the To learn list: counts as learned and never comes back.</summary>
    public bool Retired { get; set; }

    /// <summary>How many times you've forgotten this word after already seeing it ("Still learning" or a wrong choice).</summary>
    public int Lapses { get; set; }
}

/// <summary>
/// A deliberately simple spaced-repetition schedule (Leitner boxes).
/// "I know it" on a due card moves it up one level and pushes the next review further out:
/// level 1 → 1 day, 2 → 3 days, 3 → 7 days, 4 → 14 days, 5 → 30 days
/// (nudged a day or two either way to spread reviews evenly over the coming days).
/// "Still learning" drops it back (see <see cref="Forgot(CardState?, DateOnly)"/>), due again today.
/// Answering a card that isn't due yet is practice only: it never moves the card up.
/// </summary>
public static class Srs
{
    public const int LearnedLevel = 4;

    /// <summary>From this level on, the card shows English and you answer in Spanish.</summary>
    public const int RecallLevel = 2;
    public const int MaxLevel = 5;

    /// <summary>Words forgotten at least this many times count as "tricky".</summary>
    public const int TrickyLapses = 2;

    private static readonly int[] IntervalDays = [0, 1, 3, 7, 14, 30];

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static int IntervalFor(int level) => IntervalDays[Math.Clamp(level, 0, MaxLevel)];

    public static bool IsNew(CardState? s) => s is null || s.Box == 0;

    public static bool IsDue(CardState? s, DateOnly today) => s is { Box: > 0, Retired: false } && s.Due <= today;

    /// <summary>Marked as known in the list: never shown again in Review, Practice or games.</summary>
    public static bool IsRetired(CardState? s) => s is { Retired: true };

    public static bool IsLearned(CardState? s) => s is not null && s.Box >= LearnedLevel;

    /// <summary>Seen but often forgotten, and not learned yet.</summary>
    public static bool IsTricky(CardState? s) => s is { Retired: false } && s.Lapses >= TrickyLapses && !IsLearned(s);

    /// <summary>True when an "I know it" answer would count (the card is new or due).</summary>
    public static bool Counts(CardState? s, DateOnly today) => IsNew(s) || IsDue(s, today);

    /// <summary>
    /// Apply "I know it". Returns null when the card wasn't due (practice only, nothing changes).
    /// <paramref name="load"/> (cards already due per day) lets the next review land on a quieter day
    /// within a small window around the normal interval, so reviews don't bunch up.
    /// </summary>
    public static CardState? Know(CardState? s, DateOnly today, IReadOnlyDictionary<DateOnly, int>? load = null)
    {
        if (!Counts(s, today)) return null;
        var level = Math.Min((s?.Box ?? 0) + 1, MaxLevel);
        return new CardState
        {
            Box = level,
            Due = PickDueDay(today, IntervalFor(level), load),
            LastSeen = DateTime.Now,
            Lapses = s?.Lapses ?? 0,
        };
    }

    /// <summary>Choose the least busy day in a window around the interval (ties go to the exact interval).</summary>
    private static DateOnly PickDueDay(DateOnly today, int interval, IReadOnlyDictionary<DateOnly, int>? load)
    {
        var target = today.AddDays(interval);
        if (load is null) return target;
        var fuzz = interval switch
        {
            >= 30 => 4,
            >= 14 => 2,
            >= 3 => 1,
            _ => 0,
        };
        var best = target;
        var bestLoad = load.GetValueOrDefault(target);
        for (var d = -fuzz; d <= fuzz; d++)
        {
            var day = target.AddDays(d);
            var l = load.GetValueOrDefault(day);
            if (l < bestLoad || (l == bestLoad && Math.Abs(d) < Math.Abs(best.DayNumber - target.DayNumber)))
            {
                best = day;
                bestLoad = l;
            }
        }
        return best;
    }

    /// <summary>
    /// Right, but slow (over the answer time limit): the word doesn't move up and isn't counted as forgotten.
    /// A new word starts at level 1 and comes back later today; a seen word keeps its level and comes back tomorrow.
    /// Returns null when the card wasn't due (extra practice, nothing changes).
    /// </summary>
    public static CardState? Slow(CardState? s, DateOnly today)
    {
        if (!Counts(s, today)) return null;
        if (IsNew(s)) return new CardState { Box = 1, Due = today, LastSeen = DateTime.Now };
        return new CardState { Box = s!.Box, Due = today.AddDays(1), LastSeen = DateTime.Now, Lapses = s.Lapses };
    }

    /// <summary>Apply "Still learning" to a card with no history: level 1, due today.</summary>
    public static CardState Forgot(DateOnly today) =>
        new() { Box = 1, Due = today, LastSeen = DateTime.Now };

    /// <summary>
    /// "Still learning" (or a wrong choice): a card you were answering in Spanish drops back to the
    /// first Spanish level (it stays a Spanish-answer card); otherwise back to level 1. Due again today.
    /// Forgetting a word you'd already seen counts as a lapse (used for "tricky words").
    /// </summary>
    public static CardState Forgot(CardState? before, DateOnly today) => new()
    {
        Box = before is { Box: >= RecallLevel } ? RecallLevel : 1,
        Due = today,
        LastSeen = DateTime.Now,
        Lapses = (before?.Lapses ?? 0) + (IsNew(before) ? 0 : 1),
    };

    /// <summary>True when the card should show English and ask for the Spanish word.</summary>
    public static bool AsksForSpanish(CardState? s) => s is not null && s.Box >= RecallLevel;

    /// <summary>"Mark learned" in the To learn list: learned for good, never scheduled again.</summary>
    public static CardState MarkLearned(DateOnly today) =>
        new() { Box = MaxLevel, Due = DateOnly.MaxValue, LastSeen = DateTime.Now, Retired = true };

    public static string StageLabel(CardState? s) => s switch
    {
        null or { Box: 0 } => "New",
        { Box: >= LearnedLevel } => "Learned",
        _ => $"Level {s!.Box}",
    };

    public static string DueLabel(CardState? s, DateOnly today)
    {
        if (IsNew(s)) return "not started";
        if (IsRetired(s)) return "marked as known";
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
