using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Components.Games;

/// <summary>
/// Ping-pong, the classroom game: the teacher calls a person ("nosotros") and you answer with the form
/// (hablamos). One verb at a time, every person in random order, then the next verb. A person you miss
/// comes back two calls later. You answer by tapping one of four forms of the same verb.
/// </summary>
public sealed class PingPongGame
{
    public enum VerbChoice { Common, Irregular, All }

    /// <summary>One call: the person as said aloud ("ella"), the right form and four options.</summary>
    public sealed record Call(int Id, Verb Verb, int Person, string Said, string Right, IReadOnlyList<string> Options, bool FirstOfVerb);

    public sealed record Miss(Verb Verb, string Said, string Right, string Picked);

    /// <summary>How many of the most common verbs "Common" uses.</summary>
    public const int CommonCount = 30;

    private readonly string tense;
    private readonly List<Verb> pool;
    private readonly List<int> queue = new();
    private readonly Queue<string> recentVerbs = new();
    private Verb? verb;
    private int nextId;

    public PingPongGame(VerbBook book, string tense, VerbChoice choice)
    {
        this.tense = tense;
        pool = PoolFor(book, tense, choice);
    }

    public string Tense => tense;
    public bool CanPlay => pool.Count > 0;
    public Call? Now { get; private set; }

    /// <summary>
    /// Verbs that can be played in this tense: at least four different forms among the persons in use
    /// (so there are four options to tap). Weather verbs are left out.
    /// </summary>
    public static List<Verb> PoolFor(VerbBook book, string tense, VerbChoice choice)
    {
        var playable = book.Verbs.Where(v => !v.OnlyThird && v.Has(tense) && Persons(v, tense).Select(p => Shown(v, tense, p)).Distinct().Count() >= 4);
        return choice switch
        {
            VerbChoice.Common => playable.OrderBy(v => v.Rank).Take(CommonCount).ToList(),
            VerbChoice.Irregular => playable.Where(v => Persons(v, tense).Any(p => v.Why(tense, p) is not null && v.Why(tense, p) != "refl")).ToList(),
            _ => playable.ToList(),
        };
    }

    private static IEnumerable<int> Persons(Verb v, string tense) =>
        Enumerable.Range(0, VerbGrammar.PersonsFor(tense).Count).Where(p => v.HasForm(tense, p) && VerbSettings.IsActive(tense, p));

    /// <summary>The form as shown on a button: "no hables" for a "don't" command.</summary>
    private static string Shown(Verb v, string tense, int p) =>
        (VerbGrammar.PersonsFor(tense)[p].Negative ? "no " : "") + v.Form(tense, p);

    /// <summary>What the teacher says: él, ella or usted for the él form; "tú, no" for a "don't" command.</summary>
    public static string Said(string tense, int p)
    {
        var person = VerbGrammar.PersonsFor(tense)[p];
        if (tense == "cmd")
            return person.Id switch
            {
                "tu" => "tú", "tu-neg" => "tú, no", "usted" => "usted", "nos" => "nosotros",
                "vos" => "vosotros", "vos-neg" => "vosotros, no", _ => "ustedes",
            };
        string Any(params string[] options) => options[Random.Shared.Next(options.Length)];
        return person.Group switch
        {
            "yo" => "yo",
            "tu" => "tú",
            "el" => Any("él", "ella", "usted"),
            "nos" => "nosotros",
            "vos" => "vosotros",
            _ => Any("ellos", "ellas", "ustedes"),
        };
    }

    /// <summary>The next call: the next person for this verb, or a new verb once every person has had a turn.</summary>
    public void Next()
    {
        var first = false;
        if (verb is null || queue.Count == 0)
        {
            verb = PickVerb();
            if (verb is null) { Now = null; return; }
            queue.Clear();
            queue.AddRange(Persons(verb, tense).OrderBy(_ => Random.Shared.Next()));
            first = true;
        }
        var p = queue[0];
        queue.RemoveAt(0);
        var right = Shown(verb, tense, p);
        var others = Persons(verb, tense).Where(q => q != p).Select(q => Shown(verb, tense, q))
            .Where(f => f != right).Distinct().OrderBy(_ => Random.Shared.Next()).Take(3);
        var options = others.Append(right).OrderBy(_ => Random.Shared.Next()).ToList();
        Now = new Call(nextId++, verb, p, Said(tense, p), right, options, first);
    }

    /// <summary>True if right. A miss puts the person back in the queue, two calls later.</summary>
    public bool Answer(string picked)
    {
        if (Now is not { } call) return false;
        if (picked == call.Right) return true;
        queue.Insert(Math.Min(2, queue.Count), call.Person);
        return false;
    }

    /// <summary>
    /// The tracked form a call belongs to (the same one the Verbs tree uses): regular forms count towards
    /// their pattern ("-ar verbs"), changed forms towards the verb itself.
    /// </summary>
    public FormRef SkillFor(Call call) => call.Verb.Why(tense, call.Person) is null
        ? new FormRef(FormRef.PatternKey(call.Verb.PatternIn(tense)), tense, call.Person)
        : new FormRef(call.Verb.Inf, tense, call.Person);

    private Verb? PickVerb()
    {
        if (pool.Count == 0) return null;
        var fresh = pool.Where(v => !recentVerbs.Contains(v.Inf) && v != verb).ToList();
        if (fresh.Count == 0) fresh = pool.Where(v => v != verb).ToList();
        if (fresh.Count == 0) fresh = pool;
        var pick = fresh[Random.Shared.Next(fresh.Count)];
        recentVerbs.Enqueue(pick.Inf);
        while (recentVerbs.Count > Math.Min(8, pool.Count - 1)) recentVerbs.Dequeue();
        return pick;
    }
}
