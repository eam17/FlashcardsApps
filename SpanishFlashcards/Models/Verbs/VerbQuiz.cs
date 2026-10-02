namespace SpanishFlashcards.Models.Verbs;

/// <summary>One practice question.</summary>
public sealed record VerbQuestion(
    FormRef Form,
    Verb Verb,
    IReadOnlyList<string> Answers,
    bool Typing,
    // Multiple choice only: the right answer plus 5 near misses, shuffled.
    IReadOnlyList<string> Options)
{
    public string Answer => Answers[0];
    public PersonInfo Person => Form.PersonInfo;
    public TenseInfo Tense => VerbGrammar.TenseById[Form.Tense];
}

/// <summary>
/// One question slot in a session: the form whose skill is updated, and (for a regular form asked
/// while practising one verb) the verb to ask it with: tenemos updates the "-er endings" skill.
/// </summary>
public sealed record PracticeItem(FormRef Form, string? Verb = null);

/// <summary>Builds practice sessions and questions.</summary>
public static class VerbQuiz
{
    public const int SessionSize = 10;

    /// <summary>At most this many never-seen forms per session (more only if nothing else is left).</summary>
    public const int NewPerSession = 4;

    /// <summary>
    /// What practising a node covers. For a group or a tense: every form tracked underneath it.
    /// For a single verb: the whole verb in that tense (tener: tengo, tienes, tiene, tenemos, tenéis, tienen),
    /// each form counted where it belongs: tengo under -go verbs, tienes under e → ie, tenemos under the
    /// regular -er endings.
    /// </summary>
    public static List<PracticeItem> ItemsFor(VerbNode node)
    {
        if (node.Kind != NodeKind.Verb || node.Verb is not { } v)
            return node.AllForms.Where(x => VerbSettings.IsActive(x)).Select(f => new PracticeItem(f)).ToList();

        var t = node.Tense!;
        var items = new List<PracticeItem>();
        for (var p = 0; p < VerbGrammar.PersonsFor(t).Count; p++)
        {
            if (!v.HasForm(t, p) || !VerbSettings.IsActive(t, p)) continue;
            items.Add(v.Why(t, p) is null
                ? new PracticeItem(new FormRef(FormRef.PatternKey(v.PatternIn(t)), t, p), v.Inf)
                : new PracticeItem(new FormRef(v.Inf, t, p)));
        }
        return items;
    }

    /// <summary>
    /// One session: everything due first (oldest first), then a few new forms (in tree order, so regular
    /// patterns come before the exceptions), then the weakest of the rest. Each form appears once;
    /// a form only comes back in the same round if you get it wrong.
    /// </summary>
    public static List<PracticeItem> BuildSession(VerbNode node, IReadOnlyDictionary<string, VerbSkill> skills, DateTime nowUtc)
    {
        VerbSkill? S(PracticeItem f) => skills.TryGetValue(f.Form.Key, out var s) ? s : null;
        var forms = ItemsFor(node);

        var due = forms.Where(f => VerbSrs.IsDue(S(f), nowUtc))
            .OrderBy(f => S(f)!.Due).ThenBy(f => S(f)!.Strength).ToList();
        var fresh = forms.Where(f => !VerbSrs.IsSeen(S(f))).ToList();
        var rest = forms.Where(f => VerbSrs.IsSeen(S(f)) && !VerbSrs.IsDue(S(f), nowUtc))
            .OrderBy(f => S(f)!.Strength).ThenBy(f => S(f)!.Last ?? DateTime.MinValue).ToList();

        var picks = new List<PracticeItem>();
        picks.AddRange(due.Take(SessionSize));
        var newOnes = fresh.Take(Math.Min(NewPerSession, SessionSize - picks.Count)).ToList();
        var others = rest.Take(SessionSize - picks.Count - newOnes.Count).ToList();
        // Nothing seen yet to fill the session: take more new forms.
        var moreNew = fresh.Skip(newOnes.Count).Take(SessionSize - picks.Count - newOnes.Count - others.Count).ToList();

        // Old material first (warm-up), new forms spread through the session.
        var old = picks.Concat(others).ToList();
        var news = newOnes.Concat(moreNew).ToList();
        var session = new List<PracticeItem>();
        int i = 0, j = 0;
        while (i < old.Count || j < news.Count)
        {
            if (i < old.Count) session.Add(old[i++]);
            if (i < old.Count && j < news.Count && session.Count % 3 == 2) session.Add(old[i++]);
            if (j < news.Count) session.Add(news[j++]);
        }
        return session;
    }

    /// <summary>
    /// The question for a session item. Pattern forms ("~ar") use a real regular verb: the item's own verb
    /// when practising one verb, otherwise taking turns through the verbs that follow the pattern
    /// (<paramref name="rotation"/> remembers whose turn it is).
    /// </summary>
    public static VerbQuestion Make(PracticeItem item, VerbBook book, IReadOnlyDictionary<string, VerbSkill> skills,
                                    Dictionary<string, int> rotation, string? avoidVerb = null)
    {
        var form = item.Form;
        Verb verb;
        if (item.Verb is not null)
        {
            verb = book.ByInf[item.Verb];
        }
        else if (form.IsPattern)
        {
            var samples = book.PatternSamples(form.Tense, form.Pattern!);
            var key = $"{form.Pattern}|{form.Tense}";
            var idx = rotation.GetValueOrDefault(key);
            verb = samples[idx % samples.Count];
            if (verb.Inf == avoidVerb && samples.Count > 1) verb = samples[++idx % samples.Count];
            rotation[key] = idx + 1;
        }
        else
        {
            verb = book.ByInf[form.VerbKey];
        }

        var answers = verb.Answers(form.Tense, form.Person);
        skills.TryGetValue(form.Key, out var skill);
        var typing = VerbSrs.UsesTyping(skill);
        IReadOnlyList<string> options = typing ? Array.Empty<string>() : Options(verb, form.Tense, form.Person, answers, book);
        return new VerbQuestion(form, verb, answers, typing, options);
    }

    private static readonly Dictionary<string, string[]> LookAlike = new()
    {
        ["pres"] = ["subj", "impf", "pret"],
        ["near"] = ["fut", "pres"],
        ["pret"] = ["impf", "pres", "impsubj"],
        ["impf"] = ["pret", "cond", "pres"],
        ["fut"] = ["cond", "pres", "subj"],
        ["cond"] = ["fut", "impf", "impsubj"],
        ["subj"] = ["pres", "impsubj", "fut"],
        ["cmd"] = ["pres", "subj"],
        ["impsubj"] = ["subj", "pret", "cond"],
        ["perf"] = ["plup", "subjperf", "futperf"],
        ["plup"] = ["perf", "condperf", "plupsubj"],
        ["futperf"] = ["condperf", "perf"],
        ["condperf"] = ["futperf", "plup", "plupsubj"],
        ["subjperf"] = ["perf", "plupsubj"],
        ["plupsubj"] = ["subjperf", "plup", "condperf"],
    };

    /// <summary>A command slot mapped to the matching person in the other tenses.</summary>
    private static int PersonIn(string fromTense, int person, string toTense)
    {
        if (fromTense == toTense || fromTense != "cmd") return person;
        return VerbGrammar.CommandPersons[person].Group switch
        {
            "tu" => 1, "el" => 2, "nos" => 3, "vos" => 4, _ => 5,
        };
    }

    /// <summary>
    /// The right answer plus 5 near misses: the "regularised" form, a stem change used where it doesn't
    /// belong, the form without its accent, other persons of the same tense and the same person in a
    /// look-alike tense. Nothing that's also a right answer.
    /// </summary>
    public static List<string> Options(Verb v, string t, int p, IReadOnlyList<string> answers, VerbBook book)
    {
        var rng = Random.Shared;
        var right = answers[0];
        var wrongKeys = new HashSet<string>(answers.Select(Key));
        var picked = new List<string>();

        bool Add(string? s)
        {
            if (string.IsNullOrWhiteSpace(s) || picked.Count >= 5) return false;
            s = s.Split('|')[0];
            if (wrongKeys.Contains(Key(s))) return false;
            wrongKeys.Add(Key(s));
            picked.Add(s);
            return true;
        }

        Add(v.Regularised(t, p));
        Add(OverApplied(v, t, p));
        if (t == "near")
        {
            // The usual slips: forgetting the "a", conjugating the second verb, using the wrong ir.
            Add(right.Replace(" a ", " "));
            if (v.Form("pres", p) is { } conj && !v.Reflexive) Add(right[..(right.LastIndexOf(' ') + 1)] + conj);
            Add(right.Replace(VerbGrammar.IrPresent[p] + " a", new[] { "iba", "ibas", "iba", "íbamos", "ibais", "iban" }[p] + " a"));
        }
        var bare = AnswerCheck.StripAccents(right);
        if (bare != right) Add(bare);

        var persons = VerbGrammar.PersonsFor(t).Count;
        var others = Enumerable.Range(0, persons).Where(q => q != p && VerbSettings.IsActive(t, q))
            .OrderBy(q => Math.Abs(q - p)).ThenBy(_ => rng.Next())
            .Select(q => v.Form(t, q)).Where(f => f is not null).ToList();
        var tenses = LookAlike.GetValueOrDefault(t, [])
            .Select(ct => v.Form(ct, PersonIn(t, p, ct))).Where(f => f is not null).ToList();
        for (var i = 0; picked.Count < 5 && (i < others.Count || i < tenses.Count); i++)
        {
            if (i < others.Count) Add(others[i]);
            if (i < tenses.Count) Add(tenses[i]);
        }

        // Still short (weather verbs, for example): the same slot from other verbs.
        if (picked.Count < 5)
        {
            foreach (var other in book.Verbs.Where(o => o != v && o.Reflexive == v.Reflexive).OrderBy(_ => rng.Next()))
            {
                if (picked.Count >= 5) break;
                Add(other.Form(t, p));
            }
        }

        picked.Add(right);
        return picked.OrderBy(_ => rng.Next()).ToList();
    }

    private static string Key(string s) => s.Trim().ToLowerInvariant();

    private const string Accented = "áéíóú";
    private const string Plain = "aeiou";

    /// <summary>
    /// Letter tiles for spelling the answer: every letter of it (and a space tile per space), plus a few
    /// trap letters: the accent twin of a vowel (ó for hablo, o for habló), letters from the near-miss
    /// forms (the e and n of "tení" when the answer is tuve), then a common letter. Shuffled.
    /// </summary>
    public static List<string> LetterTiles(VerbQuestion q, VerbBook book)
    {
        var answer = q.Answer;
        var tiles = answer.Select(c => c.ToString()).ToList();
        var letters = answer.Replace(" ", "");
        var have = letters.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        // 0. Letters any other accepted answer needs, so it can be spelled too (hablase for hablara, rió for rio)
        var forAlternatives = new List<string>();
        foreach (var alt in q.Answers.Skip(1))
            foreach (var g in alt.Replace(" ", "").GroupBy(c => c))
                for (var n = have.GetValueOrDefault(g.Key) + forAlternatives.Count(x => x == g.Key.ToString()); n < g.Count(); n++)
                    forAlternatives.Add(g.Key.ToString());

        var want = (letters.Length <= 5 ? 3 : 4) + forAlternatives.Count;
        var extras = new List<string>(forAlternatives);

        void Add(string ch)
        {
            if (extras.Count >= want || ch == " " || extras.Contains(ch)) return;
            extras.Add(ch);
        }

        // 1. Accent traps
        foreach (var c in letters)
        {
            var i = Accented.IndexOf(c);
            if (i >= 0) Add(Plain[i].ToString());
        }
        if (!letters.Any(c => Accented.Contains(c)))
        {
            var last = letters.LastOrDefault(c => Plain.Contains(c));
            if (last != default) Add(Accented[Plain.IndexOf(last)].ToString());
        }

        // 2. Letters that would spell a near miss
        foreach (var near in Options(q.Verb, q.Form.Tense, q.Form.Person, q.Answers, book).Where(o => o != answer))
        {
            foreach (var g in near.Replace(" ", "").GroupBy(c => c))
            {
                if (g.Count() > have.GetValueOrDefault(g.Key)) Add(g.Key.ToString());
            }
        }

        // 3. Fill up with common letters
        foreach (var c in "eaosnrildtcm".OrderBy(_ => Random.Shared.Next()))
        {
            if (extras.Count >= want) break;
            Add(c.ToString());
        }

        tiles.AddRange(extras);
        // Shuffle until the tiles don't already spell the answer.
        List<string> shuffled;
        var tries = 0;
        do shuffled = tiles.OrderBy(_ => Random.Shared.Next()).ToList();
        while (string.Concat(shuffled).StartsWith(answer, StringComparison.Ordinal) && ++tries < 10);
        return shuffled;
    }

    /// <summary>
    /// For a form that follows the pattern, borrow the changed stem from a form that doesn't:
    /// podemos → *puedemos*, pedimos → *pidimos*, tenemos → *tengemos*.
    /// </summary>
    private static string? OverApplied(Verb v, string t, int p)
    {
        if (v.Reflexive || VerbGrammar.TenseById[t].IsCompound || t is "fut" or "cond") return null;
        if (v.Regularised(t, p) is not null) return null;
        var target = v.Form(t, p);
        var stem = v.PatternStem(t);
        if (target is null || stem.Length == 0 || !target.StartsWith(stem, StringComparison.Ordinal)) return null;
        var persons = VerbGrammar.PersonsFor(t).Count;
        for (var q = 0; q < persons; q++)
        {
            if (q == p) continue;
            var reg = v.Regularised(t, q);
            var real = v.Form(t, q);
            if (reg is null || real is null || !reg.StartsWith(stem, StringComparison.Ordinal)) continue;
            var ending = reg[stem.Length..];
            if (!real.EndsWith(ending, StringComparison.Ordinal)) continue;
            var changed = real[..^ending.Length];
            if (changed == stem || changed.Length == 0) continue;
            var guess = changed + target[stem.Length..];
            if (guess != target) return guess;
        }
        return null;
    }
}
