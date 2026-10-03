namespace SpanishFlashcards.Models;

/// <summary>Where a word is in the schedule.</summary>
public enum CardPhase
{
    /// <summary>Not started.</summary>
    New = 0,
    /// <summary>Met today: short steps in the same session (pick the English, pick the Spanish, recall the Spanish).</summary>
    Learning = 1,
    /// <summary>Reviewed on days, with a growing gap.</summary>
    Review = 2,
    /// <summary>Forgotten in a review: one short step, then back to reviews with a shorter gap.</summary>
    Relearning = 3,
}

/// <summary>How a review went.</summary>
public enum Rating { Forgot, Hard, Good }

/// <summary>Where one word is in the review schedule (saved in localStorage).</summary>
public sealed class CardState
{
    /// <summary>Format version: <see cref="Srs.Version"/> for this schedule. Older states are converted on load.</summary>
    public int V { get; set; }

    public CardPhase Phase { get; set; }

    /// <summary>Learning: 1 = pick the English, 2 = pick the Spanish, 3 = recall the Spanish yourself. Relearning: 1 (recall).</summary>
    public int Step { get; set; }

    /// <summary>Learning and relearning: when the next step is due (UTC).</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>Reviews: the day the word is next due.</summary>
    public DateOnly Due { get; set; }

    /// <summary>Reviews: the current gap in days. While relearning: the gap it goes back to.</summary>
    public int Interval { get; set; }

    /// <summary>The old schedule's growth factor. Only read to convert older progress to <see cref="Stability"/> / <see cref="Difficulty"/>.</summary>
    public double Ease { get; set; } = Srs.StartEase;

    /// <summary>FSRS: days until the chance of remembering it falls to 90% (0 = not worked out yet).</summary>
    public double Stability { get; set; }

    /// <summary>FSRS: 1 (easy) to 10 (hard), how slowly its stability grows (0 = not worked out yet).</summary>
    public double Difficulty { get; set; }

    /// <summary>The day of the last answer that updated <see cref="Stability"/> (any answer, steps included).</summary>
    public DateOnly? RatedOn { get; set; }

    /// <summary>The last day it graduated or was reviewed (to count a review once a day, and for the check-in).</summary>
    public DateOnly? LastReview { get; set; }

    /// <summary>When the word was last answered.</summary>
    public DateTime? LastSeen { get; set; }

    /// <summary>Marked as known with the button: counts as learned and never comes back.</summary>
    public bool Retired { get; set; }

    /// <summary>Times forgotten in a review (used for "tricky words").</summary>
    public int Lapses { get; set; }

    /// <summary>Reviews done.</summary>
    public int Reps { get; set; }

    /// <summary>
    /// A word learned today: when its quick check later the same day is due (UTC). Cleared once answered.
    /// </summary>
    public DateTime? CheckIn { get; set; }

    private int box;

    /// <summary>
    /// The level (0 to 5) of the old schedule. Read only to convert old progress; for current cards it's
    /// worked out from the gap and still saved, so an older copy of the app (an open tab, a cached page)
    /// reads the word as started instead of new and doesn't throw its progress away.
    /// </summary>
    public int Box
    {
        get => V >= Srs.Version ? Srs.LegacyLevel(this) : box;
        set => box = value;
    }
}

/// <summary>
/// The word schedule.
/// <list type="bullet">
/// <item>A new word is introduced (its meaning, example and sound), then tested three times in the same
/// session: pick the English (a minute later), pick the Spanish (five minutes later), and recall the Spanish
/// yourself (ten minutes later), because producing the Spanish is what really shows you know it. A miss
/// repeats the step a minute later. The first review is always the next day.</item>
/// <item>Every answer updates the word's memory (FSRS, see <see cref="Fsrs"/>), and each review is planned
/// for the day the chance of remembering it falls to your target (90% by default). Words you find easy
/// spread out fast; hard ones come back more often. A late review you still remember counts for more.</item>
/// <item>Forgot: one short relearning step (recall it again), then a much shorter gap worked out from its memory.</item>
/// <item>A word counts as learned from a 20-day gap, or when marked as known.</item>
/// <item>The day starts at 4 am, so a late-night session belongs to the evening it started in.</item>
/// </list>
/// </summary>
public static class Srs
{
    /// <summary>3 = FSRS (October 2026). 2 = the ease-based schedule, 0 or 1 = levels (converted on load).</summary>
    public const int Version = 3;

    public const double StartEase = 2.5;
    public const double MinEase = 1.3;

    /// <summary>A word counts as learned from this gap (days).</summary>
    public const int LearnedDays = 20;

    public const int MaxInterval = 365;

    /// <summary>Words forgotten at least this many times count as "tricky" (until learned).</summary>
    public const int TrickyLapses = 2;

    /// <summary>The app's day starts at this hour (a session at 1 am still counts as the evening before).</summary>
    public const int DayStartHour = 4;

    // Same-session steps (minutes).
    private const int StepAgain = 1;     // a miss, or a slow answer: try again soon
    private const int FirstStep = 1;     // after the introduction: pick the English
    private const int SecondStep = 5;    // then pick the Spanish
    private const int ThirdStep = 10;    // then recall the Spanish yourself

    /// <summary>Hours after learning a word before its same-day check-in.</summary>
    public const int CheckInHours = 4;
    private const int RelearnStep = 3;   // after forgetting in a review

    /// <summary>When nothing else is left, learning steps due within this many minutes are shown early.</summary>
    public const int LearnAheadMinutes = 20;

    private const int AlreadyKnownDays = 4;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now.AddHours(-DayStartHour));

    // ------------------------------------------------------------------ state

    public static bool IsNew(CardState? s) => s is null || (s.Phase == CardPhase.New && !s.Retired);

    /// <summary>Marked as known with the button: never shown again in Cards or games.</summary>
    public static bool IsRetired(CardState? s) => s is { Retired: true };

    /// <summary>In same-session steps (learning or relearning).</summary>
    public static bool IsLearning(CardState? s) =>
        s is { Retired: false, Phase: CardPhase.Learning or CardPhase.Relearning };

    /// <summary>A review due today (or overdue).</summary>
    public static bool IsDue(CardState? s, DateOnly today) =>
        s is { Retired: false, Phase: CardPhase.Review } && s.Due <= today;

    /// <summary>A learning step that's due (or due within <paramref name="aheadMinutes"/>).</summary>
    public static bool IsStepDue(CardState? s, DateTime nowUtc, int aheadMinutes = 0) =>
        IsLearning(s) && (s!.DueAt ?? DateTime.MinValue) <= nowUtc.AddMinutes(aheadMinutes);

    /// <summary>
    /// Marked as known, or in reviews with a memory of about three weeks (stability, which doesn't depend on the
    /// aim in Settings, so changing the aim can't un-learn words). Only forgetting a word takes it back out.
    /// </summary>
    public static bool IsLearned(CardState? s) =>
        s is not null && (s.Retired || (s.Phase == CardPhase.Review
            && (HasMemory(s) ? s.Stability >= LearnedDays - 0.01 : s.Interval >= LearnedDays)));

    /// <summary>Forgotten often and not learned yet.</summary>
    public static bool IsTricky(CardState? s) => s is { Retired: false } && s.Lapses >= TrickyLapses && !IsLearned(s);

    /// <summary>Reviewed already today (a review counts toward the daily limit once a day).</summary>
    public static bool ReviewedToday(CardState? s, DateOnly today) => s?.LastReview == today;

    /// <summary>0 to 5, for the dots on a card: new, learning, then by gap (1+, 3+, 20+, 60+ days).</summary>
    public static int Strength(CardState? s) => s switch
    {
        null => 0,
        { Retired: true } => 5,
        { Phase: CardPhase.New } => 0,
        { Phase: CardPhase.Learning or CardPhase.Relearning } => 1,
        { Interval: >= 60 } => 5,
        _ when IsLearned(s) => 4,
        { Interval: >= 3 } => 3,
        _ => 2,
    };

    // ------------------------------------------------------------------ new words

    /// <summary>The last learning step (recall the Spanish yourself).</summary>
    public const int LearningSteps = 3;

    /// <summary>"Got it" on the introduction: the first step (pick the English) comes a minute later.</summary>
    public static CardState Introduce(DateTime nowUtc) => new()
    {
        V = Version,
        Phase = CardPhase.Learning,
        Step = 1,
        DueAt = nowUtc.AddMinutes(FirstStep),
        LastSeen = DateTime.Now,
    };

    /// <summary>
    /// "I already know it" on the introduction: rated Easy, straight to reviews, first check in a few days
    /// (sooner than its memory says, to make sure).
    /// </summary>
    public static CardState AlreadyKnown(DateOnly today, IReadOnlyDictionary<DateOnly, int>? load = null)
    {
        var s = Fsrs.InitialStability(4);
        var gap = Math.Min(AlreadyKnownDays, Fsrs.NextInterval(s, MaxInterval));
        return new CardState
        {
            V = Version,
            Phase = CardPhase.Review,
            Interval = gap,
            Due = PickDueDay(today, gap, load),
            Stability = s,
            Difficulty = Fsrs.InitialDifficulty(4),
            RatedOn = today,
            LastReview = today,
            LastSeen = DateTime.Now,
        };
    }

    // ------------------------------------------------------------------ same-session steps

    /// <summary>The rating a step answer gives: wrong → forgot, right but slow → hard, right → good.</summary>
    public static Rating StepRating(bool right, bool slow) => !right ? Rating.Forgot : slow ? Rating.Hard : Rating.Good;

    /// <summary>
    /// A learning or relearning step. A miss or a slow answer repeats the step a minute later; a right answer
    /// moves on: learning step 1 → 2 → 3 → reviews (tomorrow); relearning → back to reviews with the gap its
    /// memory gives.
    /// </summary>
    public static CardState Step(CardState s, bool right, bool slow, DateTime nowUtc, DateOnly today,
                                 IReadOnlyDictionary<DateOnly, int>? load = null) =>
        Step(s, StepRating(right, slow), nowUtc, today, load);

    /// <summary>A step answered with a rating: only Good moves on; Hard or Forgot repeats it a minute later.</summary>
    public static CardState Step(CardState s, Rating rating, DateTime nowUtc, DateOnly today,
                                 IReadOnlyDictionary<DateOnly, int>? load = null)
    {
        var next = Copy(s);
        next.LastSeen = DateTime.Now;
        Rate(next, rating, today);
        if (rating != Rating.Good)
        {
            next.DueAt = nowUtc.AddMinutes(StepAgain);
            return next;
        }
        if (s.Phase == CardPhase.Learning && s.Step < LearningSteps)
        {
            next.Step = Math.Max(1, s.Step) + 1;
            next.DueAt = nowUtc.AddMinutes(next.Step == 2 ? SecondStep : ThirdStep);
            return next;
        }
        // Graduate: a new word's first review is always tomorrow (after a night's sleep); a relearned word
        // goes back to the gap its memory gives.
        var gap = s.Phase == CardPhase.Learning ? 1 : Fsrs.NextInterval(next.Stability, MaxInterval);
        next.Phase = CardPhase.Review;
        next.Step = 0;
        next.DueAt = null;
        next.Interval = gap;
        // A word learned for the first time today gets one more quick recall later today (if there's time
        // before the evening ends). Not after relearning, and not twice in a day.
        next.CheckIn = s.Phase == CardPhase.Learning && s.LastReview != today ? CheckInTime(nowUtc, today) : null;
        next.LastReview = today;
        next.Due = gap <= 1 ? today.AddDays(1) : PickDueDay(today, gap, load);
        return next;
    }

    /// <summary>
    /// When today's check-in for a word learned now should be: <see cref="CheckInHours"/> hours later, if that
    /// is still today and not later than 11 pm. Null when it would be too late: tomorrow's review comes first.
    /// </summary>
    private static DateTime? CheckInTime(DateTime nowUtc, DateOnly today)
    {
        var at = nowUtc.AddHours(CheckInHours);
        var local = at.ToLocalTime();
        var sameDay = DateOnly.FromDateTime(local.AddHours(-DayStartHour)) == today;
        return sameDay && local.Hour >= 6 && local.Hour < 23 ? at : null;
    }

    // ------------------------------------------------------------------ the same-day check-in

    /// <summary>A word learned earlier today whose check-in time has come (and it isn't due for a review).</summary>
    public static bool IsCheckInDue(CardState? s, DateTime nowUtc, DateOnly today) =>
        s is { Retired: false, Phase: CardPhase.Review, CheckIn: { } at } && at <= nowUtc && s.Due > today
        && s.LastReview == today;

    /// <summary>A check-in still to come later today.</summary>
    public static bool HasCheckInLater(CardState? s, DateTime nowUtc, DateOnly today) =>
        s is { Retired: false, Phase: CardPhase.Review, CheckIn: { } at } && at > nowUtc && s.Due > today
        && s.LastReview == today;

    /// <summary>
    /// The check-in answer. Remembered (Hard or Good): tomorrow's review stays. Forgotten: back to the recall
    /// step a minute later (not counted as forgetting a learned word: it's still new). Either way the answer
    /// updates its memory.
    /// </summary>
    public static CardState CheckInAnswer(CardState s, Rating rating, DateTime nowUtc, DateOnly today)
    {
        var next = Copy(s);
        next.LastSeen = DateTime.Now;
        next.CheckIn = null;
        Rate(next, rating, today);
        if (rating != Rating.Forgot) return next;
        next.Phase = CardPhase.Learning;
        next.Step = LearningSteps;
        next.DueAt = nowUtc.AddMinutes(StepAgain);
        return next;
    }

    // ------------------------------------------------------------------ reviews

    /// <summary>
    /// A review. Its memory is updated and the next review planned for when the chance of remembering falls
    /// to the target. Returns null for Hard or Good on a word that isn't due (extra practice changes nothing);
    /// Forgot always counts, due or not.
    /// </summary>
    public static CardState? Review(CardState s, Rating rating, DateTime nowUtc, DateOnly today,
                                    IReadOnlyDictionary<DateOnly, int>? load = null)
    {
        var due = IsDue(s, today);
        if (rating != Rating.Forgot && !due) return null;

        var next = Copy(s);
        next.LastSeen = DateTime.Now;
        next.LastReview = today;
        next.CheckIn = null;
        next.Reps++;
        Rate(next, rating, today);
        next.Interval = Fsrs.NextInterval(next.Stability, MaxInterval);
        if (rating == Rating.Forgot)
        {
            // One relearning step a few minutes later; Interval is the gap it goes back to (recomputed then).
            next.Phase = CardPhase.Relearning;
            next.Step = 1;
            next.DueAt = nowUtc.AddMinutes(RelearnStep);
            next.Lapses++;
            return next;
        }
        next.Due = PickDueDay(today, next.Interval, load);
        return next;
    }

    // ------------------------------------------------------------------ memory (FSRS)

    /// <summary>Rating → FSRS rating (1 forgot, 2 hard, 3 good).</summary>
    public static int FsrsRating(Rating r) => (int)r + 1;

    /// <summary>Has a worked-out memory (stability and difficulty).</summary>
    public static bool HasMemory(CardState? s) => s is { Stability: > 0, Difficulty: > 0 };

    /// <summary>
    /// Updates a word's memory with one answer: the first answer sets it, another answer the same day uses the
    /// short-term formula, an answer on a later day the long-term one (with how likely it was to be remembered).
    /// </summary>
    private static void Rate(CardState c, Rating rating, DateOnly today) => RateFsrs(c, FsrsRating(rating), today);

    private static void RateFsrs(CardState c, int g, DateOnly today)
    {
        if (!HasMemory(c) && !MemoryFromGap(c))
        {
            c.Stability = Fsrs.InitialStability(g);
            c.Difficulty = Fsrs.InitialDifficulty(g);
            c.RatedOn = today;
            return;
        }
        var elapsed = today.DayNumber - (c.RatedOn ?? today).DayNumber;
        c.Stability = elapsed <= 0
            ? Fsrs.ShortTermStability(c.Stability, g)
            : Fsrs.NextStability(c.Difficulty, c.Stability, Fsrs.Retrievability(c.Stability, elapsed), g);
        c.Difficulty = Fsrs.NextDifficulty(c.Difficulty, g);
        c.RatedOn = today;
    }

    /// <summary>
    /// A word in reviews without a worked-out memory (older progress, an import): its gap becomes its
    /// stability. False when there's nothing to go on (it then starts fresh with its next answer).
    /// </summary>
    private static bool MemoryFromGap(CardState c)
    {
        if (c.Retired || c.Phase is not (CardPhase.Review or CardPhase.Relearning) || c.Interval <= 0) return false;
        (c.Stability, c.Difficulty) = Fsrs.FromGap(c.Interval, c.Ease > 0 ? c.Ease : StartEase);
        c.RatedOn ??= c.LastReview ?? (c.Due == default ? (DateOnly?)null : c.Due.AddDays(-c.Interval));
        return true;
    }

    /// <summary>The chance (0 to 1) of remembering a word in reviews today; null for new, learning or marked words.</summary>
    public static double? Recall(CardState? s, DateOnly today)
    {
        if (s is not { Retired: false, Phase: CardPhase.Review }) return null;
        var c = s;
        if (!HasMemory(c))
        {
            c = Copy(s);
            if (!MemoryFromGap(c)) return null;
        }
        var elapsed = today.DayNumber - (c.RatedOn ?? today).DayNumber;
        return Fsrs.Retrievability(c.Stability, Math.Max(0, elapsed));
    }

    // ------------------------------------------------------------------ the Words list

    /// <summary>"Mark learned": learned for good, never scheduled again.</summary>
    public static CardState MarkLearned(DateOnly today) => new()
    {
        V = Version,
        Phase = CardPhase.Review,
        Retired = true,
        Interval = MaxInterval,
        Due = DateOnly.MaxValue,
        LastSeen = DateTime.Now,
    };

    /// <summary>
    /// "Relearn" on a word learned through reviews: back to the same-session steps (not counted as forgetting).
    /// Its memory starts again with the next answer.
    /// </summary>
    public static CardState Relearn(CardState? s, DateTime nowUtc) => new()
    {
        V = Version,
        Phase = CardPhase.Learning,
        Step = 1,
        DueAt = nowUtc,
        Lapses = s?.Lapses ?? 0,
        Reps = s?.Reps ?? 0,
        LastSeen = DateTime.Now,
    };

    /// <summary>"Still learning" from an imported list without dates: a review due today.</summary>
    public static CardState StillLearning(DateOnly today) => new()
    {
        V = Version,
        Phase = CardPhase.Review,
        Interval = 1,
        Due = today,
        LastSeen = DateTime.Now,
    };

    // ------------------------------------------------------------------ old progress

    /// <summary>
    /// Converts a word from an older schedule to this one. Marked-as-known words stay marked; every word keeps
    /// its next review date and its gap, which becomes its memory (see <see cref="Fsrs.FromGap"/>). Levels
    /// (the first schedule) become gaps first: 1 → 1 day, 2 → 3, 3 → 8, 4 → 20 (learned), 5 → 45.
    /// Returns null for an old "new" entry (nothing to keep). Current states are returned unchanged.
    /// </summary>
    public static CardState? Upgrade(CardState s, DateOnly today)
    {
        if (s.V >= Version) return s;
        CardState? c = s.V >= 2 ? Copy(s) : FromLevels(s, today);
        if (c is not null && !HasMemory(c)) MemoryFromGap(c);
        return c;
    }

    /// <summary>The first schedule (levels 0 to 5) → gaps.</summary>
    private static CardState? FromLevels(CardState s, DateOnly today)
    {
        if (s.Retired)
        {
            var learned = MarkLearned(today);
            learned.LastSeen = s.LastSeen;
            learned.Lapses = s.Lapses;
            return learned;
        }
        if (s.Box <= 0) return null;
        var interval = LegacyInterval(s.Box);
        var due = s.Due == default ? today : s.Due;
        var lastReview = due.AddDays(-interval);
        return new CardState
        {
            V = Version,
            Phase = CardPhase.Review,
            Interval = interval,
            Due = due,
            LastReview = lastReview <= today ? lastReview : today,
            Ease = Math.Max(MinEase, StartEase - 0.15 * Math.Min(s.Lapses, 4)),
            LastSeen = s.LastSeen,
            Lapses = s.Lapses,
            Reps = s.Box - 1,
        };
    }

    /// <summary>The old level that matches a current card (the reverse of <see cref="LegacyInterval"/>).</summary>
    public static int LegacyLevel(CardState s) => s switch
    {
        { Retired: true } => 5,
        { Phase: CardPhase.New } => 0,
        { Phase: CardPhase.Learning or CardPhase.Relearning } => 1,
        { Interval: >= 45 } => 5,
        { Interval: >= LearnedDays } => 4,
        { Interval: >= 8 } => 3,
        { Interval: >= 3 } => 2,
        _ => 1,
    };

    /// <summary>Old level → gap: 1 → 1 day, 2 → 3, 3 → 8, 4 → 20 (learned), 5 → 45.</summary>
    public static int LegacyInterval(int level) => level switch
    {
        <= 1 => 1,
        2 => 3,
        3 => 8,
        4 => LearnedDays,
        _ => 45,
    };

    // ------------------------------------------------------------------ labels

    public static string StageLabel(CardState? s) => s switch
    {
        _ when IsRetired(s) => "Learned",
        _ when IsNew(s) => "New",
        _ when IsLearning(s) => "Learning",
        _ when IsLearned(s) => "Learned",
        _ => "Reviewing",
    };

    public static string DueLabel(CardState? s, DateOnly today)
    {
        if (IsNew(s)) return "not started";
        if (IsRetired(s)) return "marked as known";
        if (IsLearning(s)) return "learning now";
        var days = s!.Due.DayNumber - today.DayNumber;
        return days switch
        {
            <= 0 => "due now",
            1 => "due tomorrow",
            < 7 => $"due in {days} days",
            _ => $"due {s.Due:MMM d}",
        };
    }

    /// <summary>The gap in words, e.g. "3 days", "2 months".</summary>
    public static string GapText(int days) => days switch
    {
        <= 1 => "1 day",
        < 14 => $"{days} days",
        < 60 => $"{(int)Math.Round(days / 7.0)} weeks",
        < 365 => $"{(int)Math.Round(days / 30.0)} months",
        _ => "1 year",
    };

    // ------------------------------------------------------------------ helpers

    /// <summary>Choose the least busy day in a window around the gap (ties go to the exact gap).</summary>
    private static DateOnly PickDueDay(DateOnly today, int interval, IReadOnlyDictionary<DateOnly, int>? load)
    {
        var target = today.AddDays(interval);
        if (load is null) return target;
        var fuzz = interval switch
        {
            >= 60 => Math.Max(4, interval / 20),
            >= 30 => 3,
            >= 14 => 2,
            >= 3 => 1,
            _ => 0,
        };
        var best = target;
        var bestLoad = load.GetValueOrDefault(target);
        for (var d = -fuzz; d <= fuzz; d++)
        {
            var day = target.AddDays(d);
            if (day <= today) continue;
            var l = load.GetValueOrDefault(day);
            if (l < bestLoad || (l == bestLoad && Math.Abs(d) < Math.Abs(best.DayNumber - target.DayNumber)))
            {
                best = day;
                bestLoad = l;
            }
        }
        return best;
    }

    private static CardState Copy(CardState s) => new()
    {
        V = Version,
        Phase = s.Phase,
        Step = s.Step,
        DueAt = s.DueAt,
        Due = s.Due,
        Interval = s.Interval,
        Ease = s.Ease,
        Stability = s.Stability,
        Difficulty = s.Difficulty,
        RatedOn = s.RatedOn,
        LastReview = s.LastReview,
        LastSeen = s.LastSeen,
        Retired = s.Retired,
        Lapses = s.Lapses,
        Reps = s.Reps,
        CheckIn = s.CheckIn,
    };
}
