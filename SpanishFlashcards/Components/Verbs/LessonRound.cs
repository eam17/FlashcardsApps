using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>"Who is it?": a form to recognise, and the persons it can be.</summary>
public sealed record WhoQuestion(Verb Verb, string Tense, int Person, string Form, IReadOnlyList<int> Options, IReadOnlySet<int> Right);

/// <summary>
/// One lesson on the path (see VerbPath) and where you are in it, kept in memory like a practice round so
/// you can leave and come back. Steps: the idea (a short rule with its table), real sentences that use it,
/// recognising forms (who is it?), making them (a practice round, which counts towards your verb skills),
/// then the result. Pass (80%) and the lesson is done; otherwise it goes over the idea again, with your
/// mistakes explained, and you practise once more.
/// </summary>
public sealed class LessonRound
{
    public enum Step { Learn, Spot, Recognise, Make, Result }

    private const int RecogniseCount = 4;

    public LessonRound(VerbLesson lesson, VerbBook book, Progress progress)
    {
        Lesson = lesson;
        Book = book;
        Progress = progress;
        Intro = IntroBlocks(lesson.Node, book);
        Spot = VerbStories.LinesFor(lesson.Node, book, 3);
        Recognise = WhoQuestions(lesson.Node, book, progress);
    }

    public VerbLesson Lesson { get; }
    private VerbBook Book { get; }
    private Progress Progress { get; }

    public Step Current { get; private set; } = Step.Learn;

    /// <summary>1 the first time; 2 and up when going over it again.</summary>
    public int Attempt { get; private set; } = 1;

    public IReadOnlyList<RuleBlock> Intro { get; }
    public List<StoryLine> Spot { get; }
    public List<WhoQuestion> Recognise { get; private set; }

    public int RecIndex { get; private set; }
    public int? RecPicked { get; private set; }
    public int RecRight { get; private set; }

    public PracticeRound? Practice { get; private set; }

    /// <summary>The practice score, 0 to 1 (a missing accent is half a point).</summary>
    public double Score { get; private set; }
    public bool Passed { get; private set; }

    /// <summary>Wrong answers from the last practice, to go over before trying again.</summary>
    public List<PracticeRound.Result> Mistakes { get; private set; } = new();

    public DateTime LastActive { get; private set; } = DateTime.UtcNow;

    /// <summary>The steps this lesson has (Spot only when the stories have sentences for it).</summary>
    public IReadOnlyList<Step> Steps
    {
        get
        {
            if (Attempt > 1) return [Step.Learn, Step.Make, Step.Result];
            var steps = new List<Step> { Step.Learn };
            if (Spot.Count > 0) steps.Add(Step.Spot);
            if (Recognise.Count > 0) steps.Add(Step.Recognise);
            steps.Add(Step.Make);
            steps.Add(Step.Result);
            return steps;
        }
    }

    /// <summary>Verb reviews also come up on the Cards tab (Settings → Verbs).</summary>
    public bool VerbsInCards => Progress.VerbsInCards;

    public void Continue()
    {
        LastActive = DateTime.UtcNow;
        var steps = Steps;
        var i = steps.ToList().IndexOf(Current);
        if (i < 0 || i >= steps.Count - 1) return;
        Current = steps[i + 1];
        if (Current == Step.Recognise && Recognise.Count == 0) Current = Step.Make;
        if (Current == Step.Make) Practice = new PracticeRound(Lesson.Node, Book, Progress);
    }

    // ---- recognise: who is it?

    public void PickWho(int person)
    {
        if (RecPicked is not null || RecIndex >= Recognise.Count) return;
        RecPicked = person;
        if (Recognise[RecIndex].Right.Contains(person)) RecRight++;
        LastActive = DateTime.UtcNow;
    }

    public void NextWho()
    {
        RecPicked = null;
        RecIndex++;
        if (RecIndex >= Recognise.Count) Continue();
    }

    /// <summary>"*-amos* = we." for the current recognition answer.</summary>
    public string WhoClue(WhoQuestion q)
    {
        var p = VerbGrammar.PersonsFor(q.Tense)[q.Person];
        var labels = string.Join(" or ", q.Right.Select(r => $"*{VerbCompare.Short(q.Tense, r)}*"));
        var info = VerbGrammar.TenseById[q.Tense];
        if (!info.IsCompound && q.Tense != "cmd")
        {
            var shape = VerbParts.Analyse(q.Verb, q.Tense, q.Person, q.Form);
            if (!shape.Whole && shape.Ending.Length > 0)
                return q.Right.Count > 1
                    ? $"*-{shape.Ending}* is {labels}: in this tense they share a form."
                    : $"*-{shape.Ending}* = {p.English}: {labels}.";
        }
        if (info.IsCompound)
        {
            var words = q.Form.Split(' ');
            return $"*{words[0]}* says who: {labels} ({p.English}).";
        }
        return $"It's {labels} ({p.English}).";
    }

    // ---- make it, then the result

    /// <summary>The practice round finished: score it, and record the lesson if it's passed.</summary>
    public void FinishPractice()
    {
        if (Practice is null) return;
        // Each form's first answer counts (a missed form comes back once in the round; getting it right then
        // is good practice, but doesn't make up for the miss).
        var results = Practice.Results.GroupBy(r => r.Form.Key).Select(g => g.First()).ToList();
        Score = results.Count == 0 ? 1 : results.Sum(r => VerbTests.Points(r.Grade)) / results.Count;
        Passed = Score >= VerbPath.PassShare;
        Mistakes = results.Where(r => r.Grade != Grade.Right).GroupBy(r => r.Form.Key).Select(g => g.First()).ToList();
        if (Passed)
        {
            Progress.VerbLessons[Lesson.Node.Id] = new LessonRecord
            {
                Done = DateTime.UtcNow,
                Score = (int)Math.Round(Score * 100),
            };
        }
        Current = Step.Result;
        LastActive = DateTime.UtcNow;
    }

    /// <summary>Not passed: go over the idea again (with your mistakes) and practise once more.</summary>
    public void Retry()
    {
        Attempt++;
        Current = Step.Learn;
        Practice = null;
        LastActive = DateTime.UtcNow;
    }

    // ---- building it

    /// <summary>
    /// The idea in short: the rule's summary, the endings or table, how a form is built and the tip, from the
    /// item's Rules page (the folded details stay on the Rules page).
    /// </summary>
    private static List<RuleBlock> IntroBlocks(VerbNode node, VerbBook book)
    {
        var page = VerbRules.For(node, book);
        var picked = new List<RuleBlock>();
        bool table = false, recipe = false, tip = false, summary = false;
        foreach (var b in page.Blocks)
        {
            switch (b)
            {
                case RuleSummary when !summary: summary = true; picked.Add(b); break;
                case RuleEndings or RuleTable when !table: table = true; picked.Add(b); break;
                case RuleRecipe when !recipe: recipe = true; picked.Add(b); break;
                case RuleTip when !tip: tip = true; picked.Add(b); break;
            }
        }
        return picked;
    }

    /// <summary>A few forms from this lesson to recognise, each for a different person where possible.</summary>
    private static List<WhoQuestion> WhoQuestions(VerbNode node, VerbBook book, Progress progress)
    {
        var rng = Random.Shared;
        // One form per person first (in random order), then more if there aren't enough persons.
        var shuffled = VerbQuiz.ItemsFor(node).OrderBy(_ => rng.Next()).ToList();
        var onePerPerson = shuffled.GroupBy(i => i.Form.Person).Select(g => g.First()).ToList();
        var items = onePerPerson.Concat(shuffled.Except(onePerPerson)).ToList();
        var list = new List<WhoQuestion>();
        foreach (var item in items)
        {
            if (list.Count >= RecogniseCount) break;
            var q = VerbQuiz.Make(item, book, progress.VerbSkills, progress.VerbRotation);
            var t = item.Form.Tense;
            var persons = VerbGrammar.PersonsFor(t);
            var options = Enumerable.Range(0, persons.Count)
                .Where(p => VerbSettings.IsActive(t, p) && q.Verb.HasForm(t, p))
                .ToList();
            var right = options.Where(p => string.Equals(q.Verb.Form(t, p), q.Answer, StringComparison.OrdinalIgnoreCase)).ToHashSet();
            if (right.Count == 0 || options.Count < 2) continue;
            list.Add(new WhoQuestion(q.Verb, t, item.Form.Person, q.Answer, options, right));
        }
        return list;
    }
}
