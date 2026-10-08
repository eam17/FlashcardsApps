using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// The placement check: quick multiple-choice questions along the path, to skip the lessons you already
/// know. Two questions on a tense's first lesson (its regular forms), one on each of the others. A lesson
/// you get fully right is marked done (and its forms count as practised); a tense whose first lesson you
/// miss, or where you miss two lessons, is left for the path. Two tenses in a row like that and the check
/// ends: that's where your path starts. You can stop any time; what's checked so far is kept.
/// </summary>
public sealed class PlacementRound
{
    private readonly List<VerbLesson> lessons;
    private readonly VerbBook book;
    private readonly Progress progress;

    public PlacementRound(List<VerbLesson> lessons, VerbBook book, Progress progress)
    {
        this.lessons = lessons;
        this.book = book;
        this.progress = progress;
    }

    public bool Started { get; private set; }
    public bool Done { get; private set; }

    public int LessonIndex { get; private set; }
    public VerbLesson? Lesson => LessonIndex < lessons.Count ? lessons[LessonIndex] : null;

    public VerbQuestion? Question { get; private set; }
    public string? Picked { get; private set; }
    public bool Answered => Picked is not null;
    public bool Right { get; private set; }

    public int Asked { get; private set; }
    public List<VerbLesson> Placed { get; } = new();

    private int askedInLesson;
    private bool missedInLesson;
    private int missesInTense;
    private int failedTensesInARow;
    private string? lastVerb;

    public void Start()
    {
        Started = true;
        LessonIndex = 0;
        SkipFinished();
        Ask();
    }

    /// <summary>Lessons already done (passed, or known from practice) aren't asked again.</summary>
    private void SkipFinished()
    {
        var now = DateTime.UtcNow;
        while (Lesson is { } l && VerbPath.IsDone(l, progress.VerbLessons, progress.VerbSkills, progress.VerbTests, now))
            LessonIndex++;
    }

    private bool FirstOfTense(VerbLesson l) => lessons.First(x => x.Tense == l.Tense) == l;

    private int QuestionsFor(VerbLesson l) => FirstOfTense(l) ? 2 : 1;

    private void Ask()
    {
        Picked = null;
        if (Lesson is not { } lesson)
        {
            Finish();
            return;
        }
        var items = VerbQuiz.ItemsFor(lesson.Node);
        if (items.Count == 0)
        {
            NextLesson(passed: false);
            return;
        }
        var item = items[Random.Shared.Next(items.Count)];
        var q = VerbQuiz.Make(item, book, progress.VerbSkills, progress.VerbRotation, lastVerb);
        lastVerb = q.Verb.Inf;
        // Always multiple choice: it's a quick check, not spelling practice.
        Question = q.Typing
            ? q with { Typing = false, Options = VerbQuiz.Options(q.Verb, q.Form.Tense, q.Form.Person, q.Answers, book) }
            : q;
    }

    public void Pick(string option)
    {
        if (Answered || Question is not { } q) return;
        Picked = option;
        Right = q.Answers.Any(a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase));
        Asked++;
        askedInLesson++;
        if (Right)
        {
            // A right answer counts as practice of that form (like a multiple-choice question).
            progress.VerbSkills.TryGetValue(q.Form.Key, out var before);
            progress.VerbSkills[q.Form.Key] = VerbSrs.Apply(before, Grade.Right, typed: false, DateTime.UtcNow);
        }
        else missedInLesson = true;
    }

    public void Next()
    {
        if (!Answered || Lesson is not { } lesson) return;
        if (missedInLesson || askedInLesson >= QuestionsFor(lesson))
        {
            NextLesson(passed: !missedInLesson);
            return;
        }
        Ask();
    }

    private void NextLesson(bool passed)
    {
        var lesson = Lesson!;
        var first = FirstOfTense(lesson);
        askedInLesson = 0;
        missedInLesson = false;
        var leftTense = false;
        if (passed)
        {
            progress.VerbLessons[lesson.Node.Id] = new LessonRecord { Done = DateTime.UtcNow, Placed = true };
            Placed.Add(lesson);
            LessonIndex++;
        }
        else
        {
            missesInTense++;
            if (first || missesInTense >= 2)
            {
                // Leave the rest of this tense for the path.
                leftTense = true;
                while (Lesson is { } l && l.Tense == lesson.Tense) LessonIndex++;
            }
            else LessonIndex++;
        }
        SkipFinished();
        if (Lesson?.Tense != lesson.Tense)
        {
            // Moving on to another tense: two left unfinished in a row ends the check.
            missesInTense = 0;
            failedTensesInARow = leftTense ? failedTensesInARow + 1 : 0;
            if (failedTensesInARow >= 2)
            {
                Finish();
                return;
            }
        }
        Ask();
    }

    public void Finish()
    {
        Done = true;
        Question = null;
        progress.VerbPlacementDone = true;
    }

    /// <summary>"12 of 58"</summary>
    public string PositionText => $"{Math.Min(LessonIndex + 1, lessons.Count)} of {lessons.Count}";

    public int LessonCount => lessons.Count;
}
