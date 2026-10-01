using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// One test on a tree item: spelling only, no feedback until the end. Like a practice round it lives
/// outside the screen, so you can leave mid-test and come back. Each answer updates your progress as you
/// give it; the group results (tested out, weak) are applied when the test ends.
/// </summary>
public sealed class TestRound : IVerbRound
{
    public TestRound(VerbNode node, VerbBook book, Progress progress)
    {
        Node = node;
        Book = book;
        Progress = progress;
        Items = VerbTests.Plan(node, book);
        Ask();
    }

    public VerbNode Node { get; }
    private VerbBook Book { get; }
    private Progress Progress { get; }

    public List<TestItem> Items { get; }
    public List<TestAnswer> Answers { get; } = new();
    public int Index { get; private set; }
    public VerbQuestion? Question { get; private set; }
    public TileState Spell { get; private set; } = TileState.Empty;
    public DateTime LastActive { get; private set; } = DateTime.UtcNow;

    public bool Done { get; private set; }
    public int Percent { get; private set; }
    public bool Passed { get; private set; }
    public List<UnitResult> Units { get; private set; } = new();

    public bool InProgress => Items.Count > 0 && !Done;
    public bool IsTest => true;
    public string PositionText => $"{Math.Min(Index + 1, Items.Count)} of {Items.Count}";

    private void Ask()
    {
        LastActive = DateTime.UtcNow;
        if (Index >= Items.Count)
        {
            Finish();
            return;
        }
        var q = VerbQuiz.Make(Items[Index].Item, Book, Progress.VerbSkills, Progress.VerbRotation);
        Question = q with { Typing = true, Options = Array.Empty<string>() }; // tests are always spelled
        Spell = new TileState(VerbQuiz.LetterTiles(Question, Book));
    }

    /// <summary>Check the spelled answer (or give up with <paramref name="dontKnow"/>) and move on.</summary>
    public void Answer(bool dontKnow = false)
    {
        if (Done || Question is null || (!dontKnow && !Spell.Any)) return;
        var given = dontKnow ? "" : Spell.Typed;
        var grade = dontKnow ? Grade.Wrong : AnswerCheck.Check(given, Question.Answers, Question.Person.Negative).Grade;
        var key = Question.Form.Key;
        Progress.VerbSkills.TryGetValue(key, out var before);
        Progress.VerbSkills[key] = VerbSrs.Apply(before, grade, typed: true, DateTime.UtcNow);
        Answers.Add(new TestAnswer(Items[Index], Question, given, grade));
        Index++;
        Ask();
    }

    public void Touch() => LastActive = DateTime.UtcNow;

    private void Finish()
    {
        Done = true;
        Question = null;
        Spell = TileState.Empty;
        (Percent, Passed, Units) = VerbTests.Finish(Node, Answers, Progress.VerbSkills, Progress.VerbTests, DateTime.UtcNow);
    }

    public List<(string Label, double Mistakes)> MistakesByPerson()
    {
        var totals = VerbGrammar.PersonGroups.ToDictionary(g => g.Group, _ => 0.0);
        foreach (var a in Answers)
            totals[a.Question.Form.PersonInfo.Group] += 1 - VerbTests.Points(a.Grade);
        return VerbSettings.PersonGroups.Select(g => (g.Label, totals[g.Group])).ToList();
    }
}
