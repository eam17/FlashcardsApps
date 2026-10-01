using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// One practice round and everything about where you are in it: the questions, the current one,
/// letters tapped so far, results. It lives outside the practice screen (see VerbsHub), so you can
/// look at Rules or Overview, open other items or switch app tabs and come back to exactly the same
/// spot, half-spelled answer included. Answers are saved to your progress as soon as you give them.
/// </summary>
public sealed class PracticeRound : IVerbRound
{
    public sealed record Result(FormRef Form, Grade Grade);

    public PracticeRound(VerbNode node, VerbBook book, Progress progress)
    {
        Node = node;
        Book = book;
        Progress = progress;
        Start();
    }

    public VerbNode Node { get; }
    private VerbBook Book { get; }
    private Progress Progress { get; }

    public List<PracticeItem> Session { get; private set; } = new();
    public List<Result> Results { get; } = new();
    private readonly HashSet<string> requeued = new();

    public int Index { get; private set; }
    public VerbQuestion? Question { get; private set; }
    public bool Answered { get; private set; }
    public bool Done { get; private set; }
    public Grade Grade { get; private set; }

    /// <summary>Multiple choice: the option picked.</summary>
    public string? Picked { get; private set; }

    /// <summary>The accepted answer the reply matched (or the right answer after a mistake).</summary>
    public string? Matched { get; private set; }

    /// <summary>Letter tiles for spelling the current answer, and the letters tapped so far.</summary>
    public TileState Spell { get; private set; } = TileState.Empty;

    private string? lastVerb;

    /// <summary>When this round was last touched (the Resume bar shows the most recent one).</summary>
    public DateTime LastActive { get; private set; } = DateTime.UtcNow;

    public bool InProgress => Session.Count > 0 && !Done;

    public bool IsTest => false;

    public void Start()
    {
        Session = VerbQuiz.BuildSession(Node, Progress.VerbSkills, DateTime.UtcNow);
        Results.Clear();
        requeued.Clear();
        Index = 0;
        Done = false;
        lastVerb = null;
        Ask();
    }

    private void Ask()
    {
        LastActive = DateTime.UtcNow;
        Answered = false;
        Picked = null;
        Matched = null;
        Spell = TileState.Empty;
        if (Index >= Session.Count)
        {
            Done = true;
            Question = null;
            return;
        }
        Question = VerbQuiz.Make(Session[Index], Book, Progress.VerbSkills, Progress.VerbRotation, lastVerb);
        lastVerb = Question.Verb.Inf;
        Spell = Question.Typing ? new TileState(VerbQuiz.LetterTiles(Question, Book)) : TileState.Empty;
    }

    public bool IsAnswer(string option) =>
        Question!.Answers.Any(a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase));

    /// <summary>Multiple choice. Returns true when an answer was recorded (progress needs saving).</summary>
    public bool Pick(string option)
    {
        if (Answered || Question is null) return false;
        Picked = option;
        Grade = IsAnswer(option) ? Grade.Right : Grade.Wrong;
        Matched = Grade == Grade.Right ? option : Question.Answer;
        Record(spelled: false);
        return true;
    }

    /// <summary>Check the spelled answer. Returns true when an answer was recorded.</summary>
    public bool Check()
    {
        if (Answered || Question is null || !Spell.Any) return false;
        (Grade, Matched) = AnswerCheck.Check(Spell.Typed, Question.Answers, Question.Person.Negative);
        Record(spelled: true);
        return true;
    }

    private void Record(bool spelled)
    {
        LastActive = DateTime.UtcNow;
        Answered = true;
        var key = Question!.Form.Key;
        Progress.VerbSkills.TryGetValue(key, out var before);
        Progress.VerbSkills[key] = VerbSrs.Apply(before, Grade, spelled, DateTime.UtcNow);
        Results.Add(new Result(Question.Form, Grade));

        // A form you missed comes back once more, a few questions later (sooner only if the round is ending).
        if (Grade != Grade.Right && requeued.Add(key))
            Session.Insert(Math.Min(Session.Count, Index + 3), Session[Index]);
    }

    public void Next()
    {
        Index++;
        Ask();
    }

    public void Touch() => LastActive = DateTime.UtcNow;

    public List<(string Label, double Mistakes)> MistakesByPerson()
    {
        var totals = VerbGrammar.PersonGroups.ToDictionary(g => g.Group, _ => 0.0);
        foreach (var r in Results)
            totals[r.Form.PersonInfo.Group] += r.Grade switch { Grade.Wrong => 1, Grade.AccentSlip => 0.5, _ => 0 };
        return VerbSettings.PersonGroups.Select(g => (g.Label, totals[g.Group])).ToList();
    }

    /// <summary>"3 of 6": the question you're on (or the last one, once answered).</summary>
    public string PositionText => $"{Math.Min(Index + 1, Session.Count)} of {Session.Count}";
}
