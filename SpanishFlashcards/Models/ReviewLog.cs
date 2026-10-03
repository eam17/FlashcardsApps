using System.Globalization;
using System.Text;

namespace SpanishFlashcards.Models;

/// <summary>
/// One answer on a word card, saved in the review history (the browser's IndexedDB, and in Export JSON).
/// Short property names keep years of history small. Used to show how well you remember, and to fit the
/// schedule's parameters to your own memory.
/// </summary>
public sealed class ReviewEntry
{
    /// <summary>The word (its Spanish, as in the word list).</summary>
    public string W { get; set; } = "";

    /// <summary>When (UTC, milliseconds since 1970).</summary>
    public long T { get; set; }

    /// <summary>What kind of answer: see <see cref="ReviewKind"/>.</summary>
    public string K { get; set; } = "";

    /// <summary>FSRS rating: 1 forgot, 2 hard, 3 good, 4 easy (I already know it); 0 = not a rating.</summary>
    public int R { get; set; }

    /// <summary>The word's phase before the answer (0 new, 1 learning, 2 review, 3 relearning).</summary>
    public int P { get; set; }

    /// <summary>Days since the word's previous rated answer (-1 = its first).</summary>
    public int E { get; set; } = -1;

    /// <summary>The planned gap before the answer (days, 0 if none).</summary>
    public int I { get; set; }

    /// <summary>The gap after the answer (days, 0 if none).</summary>
    public int N { get; set; }

    /// <summary>Stability after the answer (days).</summary>
    public double S { get; set; }

    /// <summary>Difficulty after the answer (1 to 10).</summary>
    public double D { get; set; }

    /// <summary>Time to answer (ms, 0 if unknown).</summary>
    public int Ms { get; set; }

    /// <summary>Typed answers: right, accent, article, almost, wrong, override ("Count it as right"); null otherwise.</summary>
    public string? C { get; set; }
}

public static class ReviewKind
{
    public const string Intro = "intro";            // met a new word (not a rating)
    public const string PickEnglish = "pick-en";    // learning step 1
    public const string PickSpanish = "pick-es";    // learning step 2
    public const string Recall = "recall";          // learning step 3
    public const string Relearn = "relearn";        // the step after forgetting in a review
    public const string Review = "review";          // a due review
    public const string Extra = "extra";            // extra practice (changes nothing unless forgotten)
    public const string CheckIn = "check-in";       // the same-day check-in
    public const string Known = "known";            // "I already know it" (rated easy)
    public const string MarkLearned = "mark-learned";
    public const string BackToLearn = "back-to-learn";
}

/// <summary>How often you remembered due reviews, overall and by gap.</summary>
public sealed record RecallStats(int Reviews, int Remembered, List<(string Label, int Reviews, int Remembered)> ByGap,
                                 int CheckIns, int CheckInsRemembered)
{
    public double Rate => Reviews == 0 ? 0 : (double)Remembered / Reviews;
}

public static class ReviewHistory
{
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Due reviews (and check-ins) in the last <paramref name="days"/> days: how many you remembered.</summary>
    public static RecallStats Stats(IEnumerable<ReviewEntry> log, int days = 30)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        var recent = log.Where(e => e.T >= since && e.R > 0).ToList();
        var reviews = recent.Where(e => e.K == ReviewKind.Review).ToList();
        var checks = recent.Where(e => e.K == ReviewKind.CheckIn).ToList();
        (string, int, int)[] buckets =
        [
            Bucket(reviews, "1 to 2 days", 0, 2),
            Bucket(reviews, "3 to 7 days", 3, 7),
            Bucket(reviews, "8 to 20 days", 8, 20),
            Bucket(reviews, "21 days or more", 21, int.MaxValue),
        ];
        return new RecallStats(reviews.Count, reviews.Count(e => e.R >= 2), buckets.Where(b => b.Item2 > 0).ToList(),
                               checks.Count, checks.Count(e => e.R >= 2));
    }

    private static (string, int, int) Bucket(List<ReviewEntry> reviews, string label, int from, int to)
    {
        // The real gap (days since the last answer); the planned one if that's unknown.
        static int Gap(ReviewEntry e) => e.E >= 0 ? e.E : e.I;
        var inGap = reviews.Where(e => Gap(e) >= from && Gap(e) <= to).ToList();
        return (label, inGap.Count, inGap.Count(e => e.R >= 2));
    }

    /// <summary>
    /// The history as the FSRS optimizer reads it (one row per rated answer that changed the schedule):
    /// card_id, review_time (ms), review_rating (1 to 4), review_state (0 to 3), review_duration (ms), plus the
    /// word and kind for reading it yourself.
    /// </summary>
    public static string ToCsv(IEnumerable<ReviewEntry> log, out int rows)
    {
        rows = 0;
        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine("card_id,review_time,review_rating,review_state,review_duration,word,kind");
        foreach (var e in log.OrderBy(e => e.T))
        {
            if (e.R <= 0) continue;
            // Extra practice only changes the schedule when you forget.
            if (e.K == ReviewKind.Extra && e.R != 1) continue;
            sb.Append(CardId(e.W).ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(e.T.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(e.R.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(e.P.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(e.Ms.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(e.W)).Append(',')
              .Append(e.K).AppendLine();
            rows++;
        }
        return sb.ToString();
    }

    /// <summary>A stable number for a word (FNV-1a, 31 bits): the optimizer wants numeric card ids.</summary>
    public static int CardId(string word)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in word)
            {
                h ^= c;
                h *= 16777619u;
            }
            return (int)(h & 0x7FFFFFFF);
        }
    }

    private static string Csv(string s) =>
        s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
