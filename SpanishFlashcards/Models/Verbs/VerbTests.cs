using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>The latest test result for one item in the tree (the item tested, or a group inside it).</summary>
public sealed class VerbTestRecord
{
    [JsonPropertyName("d")] public DateTime Date { get; set; }

    /// <summary>0 to 100. A missing accent counts as half a point.</summary>
    [JsonPropertyName("s")] public int Score { get; set; }

    [JsonPropertyName("p")] public bool Passed { get; set; }
}

/// <summary>One question in a test, and the group ("unit") it checks.</summary>
public sealed record TestItem(PracticeItem Item, VerbNode Unit);

/// <summary>One answered test question.</summary>
public sealed record TestAnswer(TestItem Item, VerbQuestion Question, string Given, Grade Grade);

/// <summary>How one group did in a test.</summary>
public sealed record UnitResult(VerbNode Unit, int Asked, double Points, bool Passed)
{
    public int Percent => Asked == 0 ? 0 : (int)Math.Round(Points * 100 / Asked);
}

/// <summary>
/// Tests: short, spelling-only checks that cover every group under an item.
/// Passing a group marks all its forms as known for about a week (they come back once to confirm).
/// Failing a group brings its forms you've seen back for practice now.
/// Passing the whole test (90% or more) marks the item as Mastered while you keep remembering it.
/// </summary>
public static class VerbTests
{
    public const int PassPercent = 90;

    /// <summary>Strength given to every form of a group you pass in a test: next review in about a week.</summary>
    public const double TestedOutStrength = 0.7;

    /// <summary>A group passes when nothing in it was wrong and it scored at least this much (an accent slip is half).</summary>
    public const double UnitPass = 0.75;

    public static bool CanTest(VerbNode node) => node.Kind != NodeKind.Root && node.AllForms.Count > 0;

    /// <summary>The groups a test on this item checks: every group or pattern underneath (the item itself if it's one).</summary>
    public static List<VerbNode> Units(VerbNode node) => node.Kind switch
    {
        NodeKind.Verb or NodeKind.Subtype => new List<VerbNode> { node },
        _ => node.Children.SelectMany(Units).ToList(),
    };

    /// <summary>
    /// The questions: every group gets at least one, bigger groups get more, up to about 20 for a tense.
    /// Questions lean towards the forms that change and spread over different verbs and persons. Shuffled.
    /// </summary>
    public static List<TestItem> Plan(VerbNode node, VerbBook book)
    {
        var rng = Random.Shared;
        var units = Units(node);
        var items = new List<TestItem>();

        if (node.Kind == NodeKind.Verb)
        {
            items.AddRange(VerbQuiz.ItemsFor(node).Select(i => new TestItem(i, node)));
            return items.OrderBy(_ => rng.Next()).ToList();
        }

        int target;
        if (units.Count == 1)
        {
            var u = units[0];
            target = u.Pattern is not null
                ? Enumerable.Range(0, VerbGrammar.PersonsFor(u.Tense!).Count).Count(p => VerbSettings.IsActive(u.Tense!, p))
                : Math.Clamp(ActiveCount(u), 1, 8);
        }
        else target = Math.Clamp(units.Count + (units.Count + 1) / 2, 8, 20);

        // One each, then the rest to the biggest groups first.
        var counts = units.ToDictionary(u => u, _ => 1);
        var bySize = units.OrderByDescending(ActiveCount).ToList();
        var extra = target - units.Count;
        for (var i = 0; extra > 0 && bySize.Count > 0; i++)
        {
            var u = bySize[i % bySize.Count];
            if (counts[u] < ActiveCount(u)) { counts[u]++; extra--; }
            else if (bySize.All(x => counts[x] >= ActiveCount(x))) break;
        }

        foreach (var u in units) items.AddRange(Pick(u, counts[u], book).Select(i => new TestItem(i, u)));
        return items.OrderBy(_ => rng.Next()).ToList();
    }

    private static int ActiveCount(VerbNode u) => u.AllForms.Count(x => VerbSettings.IsActive(x));

    /// <summary>k questions for one group: different verbs and persons where possible.</summary>
    private static List<PracticeItem> Pick(VerbNode unit, int k, VerbBook book)
    {
        var rng = Random.Shared;
        var t = unit.Tense!;
        var picks = new List<PracticeItem>();

        if (unit.Pattern is not null)
        {
            var samples = unit.Members.Count > 0 ? unit.Members : book.PatternSamples(t, unit.Pattern);
            var persons = Enumerable.Range(0, VerbGrammar.PersonsFor(t).Count).Where(p => VerbSettings.IsActive(t, p)).OrderBy(_ => rng.Next()).ToList();
            var verbs = samples.OrderBy(_ => rng.Next()).ToList();
            for (var i = 0; i < k && verbs.Count > 0 && persons.Count > 0; i++)
            {
                var form = new FormRef(FormRef.PatternKey(unit.Pattern), t, persons[i % persons.Count]);
                picks.Add(new PracticeItem(form, verbs[i % verbs.Count].Inf));
            }
            return picks;
        }

        // Group: take turns over its verbs (shuffled), a random tracked form from each.
        var byVerb = unit.AllForms.Where(x => VerbSettings.IsActive(x)).GroupBy(f => f.VerbKey)
            .Select(g => g.OrderBy(_ => rng.Next()).ToList())
            .OrderBy(_ => rng.Next())
            .ToList();
        var used = new HashSet<string>();
        for (var round = 0; picks.Count < k && round < 10; round++)
        {
            foreach (var forms in byVerb)
            {
                if (picks.Count >= k) break;
                var f = forms.FirstOrDefault(x => !used.Contains(x.Key));
                if (f is null) continue;
                used.Add(f.Key);
                picks.Add(new PracticeItem(f));
            }
        }
        return picks;
    }

    public static double Points(Grade g) => g switch { Grade.Right => 1, Grade.AccentSlip => 0.5, _ => 0 };

    /// <summary>
    /// Scores a finished test, saves the results and applies its effects:
    /// passed groups → every form known (strength at least 0.7, next review in about a week);
    /// failed groups → the forms you'd already seen come back for practice now.
    /// </summary>
    public static (int Percent, bool Passed, List<UnitResult> Units) Finish(
        VerbNode node, IReadOnlyList<TestAnswer> answers,
        Dictionary<string, VerbSkill> skills, Dictionary<string, VerbTestRecord> tests, DateTime nowUtc)
    {
        var total = answers.Count == 0 ? 0 : answers.Sum(a => Points(a.Grade)) / answers.Count;
        var percent = (int)Math.Round(total * 100);
        var passed = answers.Count > 0 && percent >= PassPercent;

        var units = new List<UnitResult>();
        foreach (var g in answers.GroupBy(a => a.Item.Unit))
        {
            var pts = g.Sum(a => Points(a.Grade));
            var ok = g.All(a => a.Grade != Grade.Wrong) && pts / g.Count() >= UnitPass;
            units.Add(new UnitResult(g.Key, g.Count(), pts, ok));
        }

        foreach (var u in units)
        {
            if (u.Unit != node)
                tests[u.Unit.Id] = new VerbTestRecord { Date = nowUtc, Score = u.Percent, Passed = u.Passed };

            foreach (var f in u.Unit.AllForms)
            {
                skills.TryGetValue(f.Key, out var s);
                if (u.Passed)
                {
                    s ??= new VerbSkill();
                    if (s.Strength < TestedOutStrength) s.Strength = TestedOutStrength;
                    var due = VerbSrs.NextDue(s.Strength, nowUtc);
                    if (s.Reps == 0 || s.Due < due) s.Due = due;
                    s.Reps = Math.Max(1, s.Reps);
                    s.Last ??= nowUtc;
                    skills[f.Key] = s;
                }
                else if (s is not null && VerbSrs.IsSeen(s) && s.Due > nowUtc)
                {
                    s.Due = nowUtc;
                }
            }
        }

        tests[node.Id] = new VerbTestRecord { Date = nowUtc, Score = percent, Passed = passed };
        return (percent, passed, units.OrderBy(u => u.Passed).ThenBy(u => u.Percent).ToList());
    }

    /// <summary>
    /// Mastered: the last test on this item was passed, and you still remember it: at least 80% of the
    /// forms you've seen under it are reasonably strong. Forgetting takes the badge away.
    /// </summary>
    public static bool IsMastered(VerbNode node, IReadOnlyDictionary<string, VerbSkill> skills, IReadOnlyDictionary<string, VerbTestRecord> tests)
    {
        if (!tests.TryGetValue(node.Id, out var r) || !r.Passed) return false;
        var seen = node.AllForms.Where(x => VerbSettings.IsActive(x)).Select(f => skills.GetValueOrDefault(f.Key)).Where(VerbSrs.IsSeen).ToList();
        if (seen.Count == 0) return false;
        return seen.Count(s => s!.Strength >= 0.5) >= 0.8 * seen.Count;
    }

    public sealed record WeakSpot(VerbNode Node, string Path, string Reason);

    /// <summary>
    /// The weakest groups under an item: groups you didn't pass in your last test first, then groups whose
    /// forms you've practised are weak or often wrong. Up to <paramref name="max"/>.
    /// </summary>
    public static List<WeakSpot> WeakSpots(VerbNode under, IReadOnlyDictionary<string, VerbSkill> skills,
                                           IReadOnlyDictionary<string, VerbTestRecord> tests, int max = 5)
    {
        var found = new List<(VerbNode Node, bool Failed, double Avg, double Mistakes)>();
        foreach (var u in AllUnits(under))
        {
            var failed = tests.TryGetValue(u.Id, out var r) && !r.Passed;
            var seen = u.AllForms.Where(x => VerbSettings.IsActive(x)).Select(f => skills.GetValueOrDefault(f.Key)).Where(VerbSrs.IsSeen).Select(s => s!).ToList();
            var avg = seen.Count == 0 ? 0 : seen.Average(s => s.Strength);
            var mistakes = seen.Sum(s => s.MistakeScore);
            var weak = failed || (seen.Count >= 2 && avg < 0.45) || (mistakes >= 3 && avg < 0.7);
            if (weak && !IsMastered(u, skills, tests)) found.Add((u, failed, avg, mistakes));
        }
        return found
            .OrderByDescending(x => x.Failed).ThenBy(x => x.Avg).ThenByDescending(x => x.Mistakes)
            .Take(max)
            .Select(x => new WeakSpot(x.Node, PathOf(x.Node, under),
                x.Failed ? "Not passed in your last test" : $"{(int)Math.Round(x.Avg * 100)}% strength · {Fmt(x.Mistakes)} mistakes"))
            .ToList();
    }

    private static IEnumerable<VerbNode> AllUnits(VerbNode n) => n.Kind switch
    {
        NodeKind.Subtype => new[] { n },
        NodeKind.Verb => Array.Empty<VerbNode>(),
        _ => n.Children.SelectMany(AllUnits),
    };

    /// <summary>"Present › Stem-changing verbs", leaving out the item you're looking from.</summary>
    private static string PathOf(VerbNode n, VerbNode from) =>
        string.Join(" › ", n.Ancestors().Reverse().Where(a => a.Kind != NodeKind.Root && a != from && !from.Ancestors().Contains(a)).Select(a => a.Title));

    private static string Fmt(double m) => m % 1 == 0 ? m.ToString("0") : m.ToString("0.0");
}
