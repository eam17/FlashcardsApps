using SpanishFlashcards.Models;

namespace SpanishFlashcards.Components.Games;

/// <summary>
/// Shared word picking for all games. Tricky words and words you're currently learning come up most,
/// brand-new and already-learned words are mixed in now and then (the occasional easy win),
/// and words marked as known never appear.
/// </summary>
public static class GamePicker
{
    public static double Weight(CardState? s)
    {
        if (Srs.IsRetired(s)) return 0;
        if (Srs.IsTricky(s)) return 10;   // forgotten often
        if (Srs.IsLearned(s)) return 1.5; // easy win
        if (Srs.IsNew(s)) return 1;       // not seen yet
        return 7;                         // currently learning
    }

    /// <summary>Words from the chosen group that fit the game; falls back to all words if the group is too small.</summary>
    public static List<Word> Pool(IEnumerable<Word> group, IEnumerable<Word> all, Func<Word, CardState?> stateOf,
                                  Func<Word, bool>? fits = null, int min = 10)
    {
        fits ??= _ => true;
        var pool = group.Where(w => fits(w) && !Srs.IsRetired(stateOf(w))).ToList();
        if (pool.Count < min) pool = all.Where(w => fits(w) && !Srs.IsRetired(stateOf(w))).ToList();
        return pool;
    }

    /// <summary>Weighted random pick.</summary>
    public static Word? Pick(IReadOnlyList<Word> candidates, Func<Word, CardState?> stateOf)
    {
        if (candidates.Count == 0) return null;
        var weights = candidates.Select(w => Math.Max(0.01, Weight(stateOf(w)))).ToArray();
        var roll = Random.Shared.NextDouble() * weights.Sum();
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return candidates[i];
        }
        return candidates[^1];
    }

    /// <summary>The separate meanings in an English gloss ("time; weather" → time, weather), "to" removed from verbs.</summary>
    public static HashSet<string> MeaningParts(string en) =>
        en.Split(new[] { ',', ';', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Select(p => p.ToLowerInvariant())
          .Select(p => p.StartsWith("to ") ? p[3..].Trim() : p)
          .Where(p => p.Length > 0)
          .ToHashSet();

    /// <summary>
    /// Believable wrong answers for <paramref name="target"/>: same part of speech (and topic when possible),
    /// never a word whose meaning overlaps. <paramref name="sameArticle"/> keeps nouns to the same el/la.
    /// </summary>
    public static List<Word> Distractors(Word target, IEnumerable<Word> all, int count, bool sameArticle = false,
                                         Func<Word, bool>? fits = null)
    {
        fits ??= _ => true;
        var meaning = MeaningParts(target.En);
        var pool = all
            .Where(w => w.Es != target.Es && w.En != target.En && fits(w) && !MeaningParts(w.En).Overlaps(meaning))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        bool SamePos(Word w) => w.Pos == target.Pos;
        bool SameArt(Word w) => !sameArticle || !target.IsNoun || w.Article == target.Article;

        var tiers = new[]
        {
            pool.Where(w => SamePos(w) && SameArt(w) && w.Topic == target.Topic),
            pool.Where(w => SamePos(w) && SameArt(w)),
            pool.Where(SamePos),
            pool,
        };

        var picks = new List<Word>();
        foreach (var tier in tiers)
        {
            foreach (var w in tier)
            {
                if (picks.Count == count) break;
                if (picks.Any(p => p.Es == w.Es || p.En == w.En || MeaningParts(p.En).Overlaps(MeaningParts(w.En)))) continue;
                picks.Add(w);
            }
            if (picks.Count == count) break;
        }
        return picks;
    }

    /// <summary>The Spanish word as it should be spoken ("la casa").</summary>
    public static string Speakable(Word w) =>
        w.IsNoun ? $"{(w.Article == "el / la" ? "el" : w.Article)} {w.Es}" : w.Es.Replace(" / ", ", ");
}

/// <summary>Deals words from a pool by weight, without repeating anything seen in the last few turns.</summary>
public sealed class WordDealer(List<Word> pool, Func<Word, CardState?> stateOf, int avoidRecent = 8)
{
    private readonly Queue<string> recent = new();

    public Word? Next(Func<Word, bool>? allowed = null)
    {
        allowed ??= _ => true;
        var candidates = pool.Where(w => allowed(w) && !recent.Contains(w.Es)).ToList();
        if (candidates.Count == 0) candidates = pool.Where(allowed).ToList();
        var pick = GamePicker.Pick(candidates, stateOf);
        if (pick is null) return null;
        recent.Enqueue(pick.Es);
        while (recent.Count > Math.Min(avoidRecent, Math.Max(1, pool.Count - 1))) recent.Dequeue();
        return pick;
    }
}

/// <summary>A countdown for timed games, with time penalties. Ticks ~10 times a second.</summary>
public sealed class GameClock : IDisposable
{
    private CancellationTokenSource? cts;
    private DateTime endsAt;

    public double Total { get; private set; }

    public double Remaining => Math.Max(0, (endsAt - DateTime.UtcNow).TotalSeconds);

    public double Percent => Total <= 0 ? 0 : Math.Clamp(Remaining / Total * 100, 0, 100);

    public void Start(double seconds, Func<Task> onTick, Func<Task> onEnd)
    {
        Stop();
        Total = seconds;
        endsAt = DateTime.UtcNow.AddSeconds(seconds);
        cts = new CancellationTokenSource();
        _ = Run(cts.Token, onTick, onEnd);
    }

    public void Penalty(double seconds) => endsAt = endsAt.AddSeconds(-seconds);

    public void Stop() => cts?.Cancel();

    private async Task Run(CancellationToken ct, Func<Task> onTick, Func<Task> onEnd)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(100, ct);
                if (Remaining <= 0)
                {
                    await onEnd();
                    return;
                }
                await onTick();
            }
        }
        catch (TaskCanceledException)
        {
            // stopped
        }
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
