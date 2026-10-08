using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>One lesson on the path: a group of forms in a tense (regular -ar verbs, o → ue, irregular stems…).</summary>
public sealed record VerbLesson(int Number, VerbNode Node, VerbNode Tense)
{
    /// <summary>"Stem-changing verbs: o → ue", or "-ar verbs".</summary>
    public string Title => Node.Parent is { Kind: NodeKind.Type } type ? $"{type.Title}: {Node.Title}" : Node.Title;

    /// <summary>"Present · Stem-changing verbs: o → ue"</summary>
    public string FullTitle => $"{Tense.Title} · {Title}";
}

/// <summary>A finished lesson (saved with your progress).</summary>
public sealed class LessonRecord
{
    [JsonPropertyName("d")] public DateTime Done { get; set; }

    /// <summary>The practice score, 0 to 100 (0 when placed).</summary>
    [JsonPropertyName("s")] public int Score { get; set; }

    /// <summary>Skipped by the placement check (you already knew it).</summary>
    [JsonPropertyName("p")] public bool Placed { get; set; }
}

/// <summary>
/// The guided path through the Verbs tab: every group of every tense you learn to use (stages 1 to 3), in
/// learning order. Each is one short lesson: the idea, real sentences, recognising the forms, making them,
/// then a check. A lesson counts as done when you pass it, when the placement check skipped it, or when
/// you already know its forms well from practice or a test.
/// </summary>
public static class VerbPath
{
    /// <summary>Share of the lesson's practice to get right to pass (a missing accent is half a point).</summary>
    public const double PassShare = 0.8;

    /// <summary>Forms this strong on average (practised before the path existed) count as a finished lesson.</summary>
    private const int KnownPercent = 70;

    public static List<VerbLesson> Build(VerbNode root)
    {
        var lessons = new List<VerbLesson>();
        foreach (var tense in root.Children.Where(t => t.TenseInfo is { Stage: <= 3 }))
        {
            foreach (var unit in VerbTests.Units(tense))
            {
                if (!unit.AllForms.Any(f => VerbSettings.IsActive(f))) continue;
                lessons.Add(new VerbLesson(lessons.Count + 1, unit, tense));
            }
        }
        return lessons;
    }

    public static bool IsDone(VerbLesson lesson, IReadOnlyDictionary<string, LessonRecord> done,
                              IReadOnlyDictionary<string, VerbSkill> skills, IReadOnlyDictionary<string, VerbTestRecord> tests, DateTime nowUtc)
    {
        if (done.ContainsKey(lesson.Node.Id)) return true;
        if (VerbTests.IsMastered(lesson.Node, skills, tests)) return true;
        var stats = VerbTree.Stats(lesson.Node, skills, nowUtc);
        return stats.Started == stats.Total && stats.Total > 0 && stats.Percent >= KnownPercent;
    }

    /// <summary>The first lesson not done yet (null when the whole path is done).</summary>
    public static VerbLesson? Next(IReadOnlyList<VerbLesson> lessons, IReadOnlyDictionary<string, LessonRecord> done,
                                   IReadOnlyDictionary<string, VerbSkill> skills, IReadOnlyDictionary<string, VerbTestRecord> tests, DateTime nowUtc) =>
        lessons.FirstOrDefault(l => !IsDone(l, done, skills, tests, nowUtc));

    /// <summary>Verb forms due for review anywhere in the tree (only ones you've practised), oldest first.</summary>
    public static List<FormRef> Due(VerbNode root, IReadOnlyDictionary<string, VerbSkill> skills, DateTime nowUtc) =>
        root.AllForms
            .Where(f => VerbSettings.IsActive(f) && skills.TryGetValue(f.Key, out var s) && VerbSrs.IsDue(s, nowUtc))
            .OrderBy(f => skills[f.Key].Due)
            .ToList();

    /// <summary>One review round: the most overdue forms, up to a practice session's size.</summary>
    public static List<PracticeItem> DueSession(VerbNode root, IReadOnlyDictionary<string, VerbSkill> skills, DateTime nowUtc) =>
        Due(root, skills, nowUtc).Take(VerbQuiz.SessionSize).Select(f => new PracticeItem(f)).ToList();
}
