using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Games;

/// <summary>Shared pieces for the verb games: picking verbs and forms, readings of a form, recording misses.</summary>
public static class VerbGameKit
{
    /// <summary>The verb games mostly use this many of the most common verbs.</summary>
    public const int CommonVerbs = 120;

    /// <summary>Who-buttons in the usual order (vosotros left out when it's off in Settings).</summary>
    public static IEnumerable<(string Group, string Label)> WhoButtons =>
        new[]
        {
            ("yo", "yo"), ("tu", "tú"), ("el", "él / ella / usted"), ("nos", "nosotros"),
            ("vos", "vosotros"), ("ellos", "ellos / ellas / ustedes"),
        }.Where(x => VerbSettings.Vosotros || x.Item1 != "vos");

    public static string WhoShort(string group) => group switch
    {
        "yo" => "yo", "tu" => "tú", "el" => "él / ella / usted", "nos" => "nosotros", "vos" => "vosotros", _ => "ellos / ustedes",
    };

    /// <summary>A random common verb (more common ones a little more often) that passes the filter.</summary>
    public static Verb? PickVerb(VerbBook book, Func<Verb, bool> fits, int top = CommonVerbs)
    {
        var pool = book.Verbs.Take(top).Where(fits).ToList();
        if (pool.Count == 0) pool = book.Verbs.Where(fits).ToList();
        if (pool.Count == 0) return null;
        // Rank-weighted: the first verbs about three times as likely as the last.
        var weights = pool.Select((_, i) => 3.0 - 2.0 * i / Math.Max(1, pool.Count - 1)).ToArray();
        var roll = Random.Shared.NextDouble() * weights.Sum();
        for (var i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return pool[i];
        }
        return pool[^1];
    }

    /// <summary>A person this verb has in this tense, respecting the vosotros setting.</summary>
    public static int? PickPerson(Verb v, string tense, Func<int, bool>? fits = null)
    {
        var persons = Enumerable.Range(0, VerbGrammar.PersonsFor(tense).Count)
            .Where(p => v.HasForm(tense, p) && VerbSettings.IsActive(tense, p) && (fits?.Invoke(p) ?? true)).ToList();
        return persons.Count == 0 ? null : persons[Random.Shared.Next(persons.Count)];
    }

    /// <summary>The form as shown: "no hables" for a "don't" command.</summary>
    public static string Shown(Verb v, string tense, int p) =>
        (VerbGrammar.PersonsFor(tense)[p].Negative ? "no " : "") + v.Form(tense, p);

    // ------------------------------------------------------------------ readings

    private static VerbBook? indexedBook;
    private static Dictionary<string, List<(Verb Verb, string Tense, int Person)>> index = new();

    /// <summary>Every (verb, tense, person) a form can be: fue is ser or ir; hablamos is present or preterite.</summary>
    public static IReadOnlyList<(Verb Verb, string Tense, int Person)> Readings(VerbBook book, string form)
    {
        if (indexedBook != book)
        {
            var d = new Dictionary<string, List<(Verb, string, int)>>();
            foreach (var v in book.Verbs)
                foreach (var t in VerbGrammar.Tenses)
                    for (var p = 0; p < VerbGrammar.PersonsFor(t.Id).Count; p++)
                        if (v.HasForm(t.Id, p) && VerbSettings.IsActive(t.Id, p))
                        {
                            var key = Shown(v, t.Id, p).ToLowerInvariant();
                            if (!d.TryGetValue(key, out var list)) d[key] = list = new();
                            list.Add((v, t.Id, p));
                        }
            index = d;
            indexedBook = book;
        }
        if (index.TryGetValue(form.ToLowerInvariant(), out var hits)) return hits;
        return Array.Empty<(Verb, string, int)>();
    }

    // ------------------------------------------------------------------ progress

    /// <summary>The practice skill a form belongs to in the Verbs tree (regular forms count towards their pattern).</summary>
    public static string PracticeKey(Verb v, string t, int p) => (v.Why(t, p) is null
        ? new FormRef(FormRef.PatternKey(v.PatternIn(t)), t, p)
        : new FormRef(v.Inf, t, p)).Key;

    /// <summary>The Decode (recognition) skill for a form.</summary>
    public static string DecodeKey(Verb v, string t, int p) => $"dec:{v.Inf}|{t}|{VerbGrammar.PersonsFor(t)[p].Id}";

    /// <summary>
    /// A miss in a game counts as a wrong answer for that form, so it comes up again soon in the Verbs tab.
    /// Right answers in games aren't recorded: a quick game shouldn't make a form look learned.
    /// </summary>
    public static void RecordMiss(Progress progress, string key)
    {
        progress.VerbSkills.TryGetValue(key, out var before);
        progress.VerbSkills[key] = VerbSrs.Apply(before, Grade.Wrong, typed: false, DateTime.UtcNow);
    }
}
