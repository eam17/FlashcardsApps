using SpanishFlashcards.Components.Games;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>A meaning question's options: the right English, every English that counts as right, and the options shown.</summary>
public sealed record MeaningChoices(string Right, IReadOnlySet<string> AllRight, IReadOnlyList<string> Options);

/// <summary>The stem and ending of a form, with decoys from the same verb (for building a form).</summary>
public sealed record BuildChunks(string Stem, string Ending, IReadOnlyList<string> Stems, IReadOnlyList<string> Endings);

/// <summary>
/// Options for meaning questions (tuvimos → "we had") and for building a form from its pieces, shared by the verb
/// sessions. The wrong options are near misses: the same verb in a tense that's easy to mix up, or another person.
/// </summary>
public static class VerbMeanings
{
    // Tenses whose English is easy to confuse with each tense: used for the wrong options.
    private static readonly Dictionary<string, string[]> LookAlike = new()
    {
        ["pres"] = ["pret", "impf", "fut", "subj"],
        ["near"] = ["fut", "pres", "cond"],
        ["pret"] = ["impf", "pres", "perf", "plup"],
        ["impf"] = ["pret", "cond", "pres"],
        ["fut"] = ["cond", "pres", "near"],
        ["cond"] = ["fut", "impf", "condperf"],
        ["perf"] = ["pret", "plup", "pres"],
        ["subj"] = ["pres", "impsubj", "fut"],
        ["cmd"] = ["pres", "subj"],
        ["impsubj"] = ["subj", "pret", "cond"],
        ["plup"] = ["perf", "pret", "condperf"],
        ["futperf"] = ["condperf", "fut", "perf"],
        ["condperf"] = ["futperf", "cond", "plup"],
        ["subjperf"] = ["perf", "plupsubj", "subj"],
        ["plupsubj"] = ["subjperf", "plup", "condperf"],
    };

    /// <summary>
    /// The English for a form and 3 wrong options. <paramref name="readings"/> are the readings that count as right
    /// (default: every reading of the form, so hablamos is "we speak" or "we spoke"; in a sentence, pass the one the
    /// sentence means). <paramref name="rivals"/> replaces the usual look-alike tenses (a choose unit's other tense).
    /// Null when the verb has no English.
    /// </summary>
    public static MeaningChoices? For(VerbBook book, Verb v, string t, int p,
                                      IEnumerable<(Verb Verb, string Tense, int Person)>? readings = null,
                                      IReadOnlyList<string>? rivals = null)
    {
        var right = VerbEnglish.Meaning(v, t, p);
        if (right is null) return null;
        var allRight = new HashSet<string> { right };
        foreach (var r in readings ?? VerbGameKit.Readings(book, VerbGameKit.Shown(v, t, p)))
            if (VerbEnglish.Meaning(r.Verb, r.Tense, r.Person) is { } m) allRight.Add(m);

        var rng = Random.Shared;
        var wrong = new List<string>();
        void Add(string? s)
        {
            if (s is null || allRight.Contains(s) || wrong.Contains(s) || wrong.Count >= 3) return;
            wrong.Add(s);
        }

        var look = (rivals ?? LookAlike.GetValueOrDefault(t, [])).Where(x => x != t).OrderBy(_ => rng.Next()).ToList();
        foreach (var lt in look.Take(2)) Add(SamePerson(v, t, p, lt));

        // Not "you" for an él form or "you all" for an ellos form: usted and ustedes use those forms.
        var persons = VerbGrammar.PersonsFor(t);
        var group = persons[p].Group;
        bool Confusable(string g) => (group, g) is ("el", "tu") or ("tu", "el") or ("ellos", "vos") or ("vos", "ellos");
        foreach (var q in Enumerable.Range(0, persons.Count)
                     .Where(q => q != p && VerbSettings.IsActive(t, q) && !Confusable(persons[q].Group))
                     .OrderBy(_ => rng.Next()))
            Add(VerbEnglish.Meaning(v, t, q));
        foreach (var lt in look) Add(SamePerson(v, t, p, lt));
        foreach (var lt in new[] { "pres", "pret", "impf", "fut", "cond", "perf" }) Add(SamePerson(v, t, p, lt));
        if (wrong.Count < 2) return null;

        return new MeaningChoices(right, allRight, wrong.Append(right).OrderBy(_ => rng.Next()).ToList());
    }

    /// <summary>The same person in another tense (a command person maps to the matching ordinary person).</summary>
    private static string? SamePerson(Verb v, string t, int p, string other)
    {
        var group = VerbGrammar.PersonsFor(t)[p].Group;
        var persons = VerbGrammar.PersonsFor(other);
        for (var q = 0; q < persons.Count; q++)
            if (persons[q].Group == group && !persons[q].Negative) return VerbEnglish.Meaning(v, other, q);
        return null;
    }

    /// <summary>
    /// A one-word form split into stem + ending, with 2 decoy stems and 3 decoy endings from the same verb (other
    /// persons, other tenses, the plain stem). Null when the form can't be split that way.
    /// </summary>
    public static BuildChunks? Build(Verb v, string t, int p)
    {
        if (v.Reflexive || VerbGrammar.TenseById[t].IsCompound || t is "near" or "cmd") return null;
        var form = v.Form(t, p);
        if (form is null) return null;
        var shape = VerbParts.Analyse(v, t, p, form);
        if (shape.Whole || shape.Ending.Length == 0 || shape.Stem.Length == 0) return null;
        var answers = v.Answers(t, p);

        var stemPool = new List<string>();
        var endingPool = new List<string>();
        void Collect(string tt, int q)
        {
            if (!v.HasForm(tt, q)) return;
            var f = v.Form(tt, q)!;
            var sh = VerbParts.Analyse(v, tt, q, f);
            if (sh.Whole || sh.Ending.Length == 0) return;
            stemPool.Add(sh.Stem);
            endingPool.Add(sh.Ending);
        }
        for (var q = 0; q < VerbGrammar.Persons.Count; q++) if (q != p && VerbSettings.IsActive(t, q)) Collect(t, q);
        foreach (var tt in new[] { "pres", "pret", "impf", "fut", "cond", "subj", "impsubj" }) if (tt != t) Collect(tt, p);
        stemPool.Add(v.PatternStem(t));
        stemPool.Add(v.Inf.Length > 2 ? v.Inf[..^2] : v.Inf);
        stemPool.Add(v.Inf);

        var rng = Random.Shared;
        var stems = stemPool.Where(s => s != shape.Stem).Distinct().OrderBy(_ => rng.Next()).Take(2).ToList();
        // No decoy ending that would also make a right answer with the right stem.
        var endings = endingPool.Where(e => e != shape.Ending && !answers.Contains(shape.Stem + e))
            .Distinct().OrderBy(_ => rng.Next()).Take(3).ToList();
        if (stems.Count < 1 || endings.Count < 2) return null;
        stems.Add(shape.Stem);
        endings.Add(shape.Ending);
        return new BuildChunks(shape.Stem, shape.Ending,
            stems.OrderBy(_ => rng.Next()).ToList(), endings.OrderBy(_ => rng.Next()).ToList());
    }
}
