namespace SpanishFlashcards.Models.Verbs;

/// <summary>One tense (or mood) the Verbs tab teaches.</summary>
public sealed record TenseInfo(
    string Id,
    string Name,
    string Spanish,
    string English,
    // Learning stage: 1 = start here, 2 = next, 3 = later, 4 = recognise only (see VerbGrammar.Stages).
    int Stage,
    // How often you meet it: 4 = everyday, 3 = very common, 2 = common, 1 = rare.
    int Frequency,
    // For compound tenses: the tense of the helper verb (e.g. "pres" for he hablado, voy a hablar).
    string? HaberTense = null)
{
    /// <summary>Two words: a helper verb + the main verb (he hablado, voy a hablar).</summary>
    public bool IsCompound => HaberTense is not null;
    public bool IsCommand => Id == "cmd";

    /// <summary>ir a + infinitive (voy a hablar): the helper is ir, not haber, and the main verb stays as it is.</summary>
    public bool IsGoingTo => Id == "near";

    /// <summary>"Everyday", "Very common", "Common" or "Rare".</summary>
    public string FrequencyLabel => VerbGrammar.FrequencyLabel(Frequency);
}

/// <summary>A group of tenses in the suggested learning order.</summary>
public sealed record TenseStage(int Number, string Title, string Blurb);

/// <summary>A person slot in a conjugation table (yo, tú… or a command form).</summary>
public sealed record PersonInfo(string Id, string Label, string English, string Group, bool Negative = false);

/// <summary>
/// The vosotros setting (Settings → Verbs). Off: vosotros forms are hidden from tables and left out of
/// practice, tests, scores, mistakes by person, weak spots and the grid. Set from Progress.ShowVosotros.
/// </summary>
public static class VerbSettings
{
    public static bool Vosotros { get; set; } = true;

    public static bool IsActive(PersonInfo p) => Vosotros || p.Group != "vos";

    public static bool IsActive(FormRef f) => IsActive(f.PersonInfo);

    public static bool IsActive(string tense, int person) => IsActive(VerbGrammar.PersonsFor(tense)[person]);

    /// <summary>The six people for "mistakes by person", without vosotros when it's off.</summary>
    public static IEnumerable<(string Group, string Label)> PersonGroups =>
        VerbGrammar.PersonGroups.Where(g => Vosotros || g.Group != "vos");
}

public static class VerbGrammar
{
    /// <summary>
    /// Tenses in the suggested learning order: the ones you meet most first. Not taught: future subjunctive,
    /// preterite perfect (hube hablado).
    /// </summary>
    public static readonly IReadOnlyList<TenseInfo> Tenses =
    [
        new("pres", "Present", "presente", "I speak, I'm speaking", 1, 4),
        new("near", "Going to (ir a)", "ir a + infinitivo", "I'm going to speak", 1, 3, HaberTense: "pres"),
        new("pret", "Preterite", "pretérito indefinido", "I spoke", 1, 4),
        new("impf", "Imperfect", "pretérito imperfecto", "I used to speak, I was speaking", 1, 3),
        new("perf", "Present perfect", "pretérito perfecto", "I have spoken", 2, 3, HaberTense: "pres"),
        new("cmd", "Commands", "imperativo", "Speak! Don't speak!", 2, 3),
        new("subj", "Present subjunctive", "presente de subjuntivo", "(that) I speak", 2, 3),
        new("fut", "Future", "futuro", "I will speak", 2, 2),
        new("cond", "Conditional", "condicional", "I would speak", 3, 2),
        new("impsubj", "Imperfect subjunctive", "imperfecto de subjuntivo", "(if) I spoke, (that) I would speak", 3, 2),
        new("plup", "Pluperfect", "pluscuamperfecto", "I had spoken", 3, 2, HaberTense: "impf"),
        new("futperf", "Future perfect", "futuro perfecto", "I will have spoken", 4, 1, HaberTense: "fut"),
        new("condperf", "Conditional perfect", "condicional perfecto", "I would have spoken", 4, 1, HaberTense: "cond"),
        new("subjperf", "Present perfect subjunctive", "pretérito perfecto de subjuntivo", "(that) I have spoken", 4, 1, HaberTense: "subj"),
        new("plupsubj", "Pluperfect subjunctive", "pluscuamperfecto de subjuntivo", "(if) I had spoken", 4, 1, HaberTense: "impsubj"),
    ];

    public static readonly IReadOnlyList<TenseStage> Stages =
    [
        new(1, "Start here", "What you'll hear in almost every conversation."),
        new(2, "Next", "Common in conversation: experiences, orders, wishes, plans."),
        new(3, "Later", "For \"would\", \"if\" and stories about the past."),
        new(4, "Recognise only", "Rare in conversation. Being able to recognise them when you read is enough."),
    ];

    public static string FrequencyLabel(int frequency) => frequency switch
    {
        >= 4 => "Everyday",
        3 => "Very common",
        2 => "Common",
        _ => "Rare",
    };

    /// <summary>Tenses with a column in the grid (not "going to": it's the same for every verb).</summary>
    public static IEnumerable<TenseInfo> GridTenses => Tenses.Where(t => !t.IsGoingTo);

    /// <summary>Present of ir, the helper in "going to".</summary>
    public static readonly string[] IrPresent = ["voy", "vas", "va", "vamos", "vais", "van"];

    public static readonly IReadOnlyDictionary<string, TenseInfo> TenseById = Tenses.ToDictionary(t => t.Id);

    public static readonly IReadOnlyList<PersonInfo> Persons =
    [
        new("yo", "yo", "I", "yo"),
        new("tu", "tú", "you (one person, informal)", "tu"),
        new("el", "él / ella / usted", "he, she, you (formal)", "el"),
        new("nos", "nosotros", "we", "nos"),
        new("vos", "vosotros", "you all (informal, Spain)", "vos"),
        new("ellos", "ellos / ellas / ustedes", "they, you all", "ellos"),
    ];

    public static readonly IReadOnlyList<PersonInfo> CommandPersons =
    [
        new("tu", "tú", "you (informal)", "tu"),
        new("tu-neg", "tú, negative", "you (informal), don't", "tu", Negative: true),
        new("usted", "usted", "you (formal)", "el"),
        new("nos", "nosotros", "let's", "nos"),
        new("vos", "vosotros", "you all (informal)", "vos"),
        new("vos-neg", "vosotros, negative", "you all (informal), don't", "vos", Negative: true),
        new("ustedes", "ustedes", "you all", "ellos"),
    ];

    public static IReadOnlyList<PersonInfo> PersonsFor(string tense) => tense == "cmd" ? CommandPersons : Persons;

    /// <summary>The six people used for "mistakes by person" (commands fold into these).</summary>
    public static readonly IReadOnlyList<(string Group, string Label)> PersonGroups =
    [
        ("yo", "yo"), ("tu", "tú"), ("el", "él / usted"), ("nos", "nosotros"), ("vos", "vosotros"), ("ellos", "ellos / ustedes"),
    ];

    public static readonly string[] ReflexivePronouns = ["me", "te", "se", "nos", "os", "se"];
}

/// <summary>Shape of one entry in wwwroot/data/verbs.json (made by tools/verb-data/gen.py).</summary>
public sealed class VerbDto
{
    public string Inf { get; set; } = "";
    public string En { get; set; } = "";
    public bool Refl { get; set; }
    public bool Only3 { get; set; }
    /// <summary>Simple tenses: tense id → forms (null = no such form). "a|b" = both accepted.</summary>
    public Dictionary<string, string?[]> F { get; set; } = new();
    public string Pp { get; set; } = "";
    public string? PpWhy { get; set; }
    /// <summary>Why each form isn't the plain pattern (null = it is). See gen.py for the codes.</summary>
    public Dictionary<string, string?[]> Why { get; set; } = new();
    /// <summary>What the plain pattern would give, where that's different (used for near-miss options).</summary>
    public Dictionary<string, string?[]>? Reg { get; set; }
    public string? RegPp { get; set; }
}

public sealed class VerbFile
{
    public List<VerbDto> Verbs { get; set; } = new();
}

/// <summary>A verb with all its forms (simple and compound tenses).</summary>
public sealed class Verb
{
    public required string Inf { get; init; }
    public required string En { get; init; }
    public bool Reflexive { get; init; }
    /// <summary>Weather verbs (llover, nevar): only the "it" form exists.</summary>
    public bool OnlyThird { get; init; }
    /// <summary>Position in the word list (1 = most common).</summary>
    public int Rank { get; init; }
    public required string Participle { get; init; }

    /// <summary>"ar", "er" or "ir".</summary>
    public required string Class { get; init; }

    internal Dictionary<string, string?[]> Forms { get; } = new();
    internal Dictionary<string, string?[]> Whys { get; } = new();
    internal Dictionary<string, string?[]> Regs { get; } = new();

    public bool Has(string tense) => Forms.ContainsKey(tense);

    /// <summary>The form shown as the answer (first alternative), or null.</summary>
    public string? Form(string tense, int person)
    {
        if (!Forms.TryGetValue(tense, out var f) || person < 0 || person >= f.Length) return null;
        return f[person]?.Split('|')[0];
    }

    public bool HasForm(string tense, int person) => Form(tense, person) is not null;

    /// <summary>Why this form isn't the plain pattern (null = regular).</summary>
    public string? Why(string tense, int person) =>
        Whys.TryGetValue(tense, out var w) && person >= 0 && person < w.Length ? w[person] : null;

    /// <summary>The plain-pattern version where it differs from the real form, for near-miss options.</summary>
    public string? Regularised(string tense, int person) =>
        Regs.TryGetValue(tense, out var r) && person >= 0 && person < r.Length ? r[person] : null;

    /// <summary>Every answer accepted for a form: listed alternatives, plus the -se versions of the imperfect subjunctive.</summary>
    public IReadOnlyList<string> Answers(string tense, int person)
    {
        if (!Forms.TryGetValue(tense, out var f) || person < 0 || person >= f.Length || f[person] is null) return [];
        var list = f[person]!.Split('|').ToList();
        if (tense is "impsubj" or "plupsubj")
        {
            foreach (var a in list.ToList())
            {
                var se = SeForm(a, tense);
                if (se != a && !list.Contains(se)) list.Add(se);
            }
        }
        return list;
    }

    /// <summary>
    /// The -se imperfect subjunctive (hablase) for a -ra form (hablara). In the pluperfect subjunctive the
    /// change is on haber (hubiera hablado → hubiese hablado).
    /// </summary>
    public static string SeForm(string form, string tense)
    {
        var words = form.Split(' ');
        var i = tense == "plupsubj" ? Array.FindIndex(words, w => w.StartsWith("hubier", StringComparison.Ordinal)) : words.Length - 1;
        if (i < 0) return form;
        words[i] = RaToSe(words[i]);
        return string.Join(' ', words);
    }

    public static string RaToSe(string word)
    {
        foreach (var (ra, se) in new[] { ("ramos", "semos"), ("rais", "seis"), ("ras", "ses"), ("ran", "sen"), ("ra", "se") })
            if (word.EndsWith(ra, StringComparison.Ordinal)) return word[..^ra.Length] + se;
        return word;
    }

    /// <summary>The pattern a verb follows in a tense: "ar", "er", "ir", "er-ir" or "inf" (whole infinitive).</summary>
    public string PatternIn(string tense) => tense switch
    {
        "fut" or "cond" or "near" => "inf",
        "pres" or "cmd" => Class,
        _ => Class == "ar" ? "ar" : "er-ir",
    };

    /// <summary>Stem used for the pattern: the infinitive without -ar/-er/-ir (or the whole infinitive for fut/cond).</summary>
    public string PatternStem(string tense)
    {
        var bare = Reflexive ? Inf[..^2] : Inf;
        return tense is "fut" or "cond" or "near" ? bare : bare[..^2];
    }

    public static Verb FromDto(VerbDto d, int rank, Verb? haber)
    {
        var bare = d.Refl ? d.Inf[..^2] : d.Inf;
        var cls = bare[^2..] switch { "ar" => "ar", "er" => "er", _ => "ir" };
        var v = new Verb
        {
            Inf = d.Inf,
            En = d.En,
            Reflexive = d.Refl,
            OnlyThird = d.Only3,
            Rank = rank,
            Participle = d.Pp,
            Class = cls,
        };
        foreach (var (t, f) in d.F) v.Forms[t] = f;
        foreach (var (t, w) in d.Why) v.Whys[t] = w;
        if (d.Reg is not null) foreach (var (t, r) in d.Reg) v.Regs[t] = r;

        // Compound tenses: (pronoun) + haber + participle. The participle never changes.
        var h = haber ?? (d.Inf == "haber" ? v : null);
        if (h is not null)
        {
            foreach (var tense in VerbGrammar.Tenses.Where(t => t.IsCompound && !t.IsGoingTo))
            {
                var forms = new string?[6];
                var whys = new string?[6];
                var regs = new string?[6];
                for (var p = 0; p < 6; p++)
                {
                    if (d.Only3 && p != 2) continue;
                    var aux = h.Form(tense.HaberTense!, p);
                    if (aux is null) continue;
                    var pron = d.Refl ? VerbGrammar.ReflexivePronouns[p] + " " : "";
                    forms[p] = $"{pron}{aux} {d.Pp}";
                    whys[p] = d.Refl ? "refl" : d.PpWhy;
                    if (d.RegPp is not null) regs[p] = $"{pron}{aux} {d.RegPp}";
                }
                v.Forms[tense.Id] = forms;
                if (whys.Any(w => w is not null)) v.Whys[tense.Id] = whys;
                if (regs.Any(r => r is not null)) v.Regs[tense.Id] = regs;
            }
        }

        // Going to: (pronoun) + voy a + infinitive. Same for every verb; reflexive verbs can also put the
        // pronoun on the end (voy a levantarme), and both are accepted.
        {
            var forms = new string?[6];
            var whys = new string?[6];
            for (var p = 0; p < 6; p++)
            {
                if (d.Only3 && p != 2) continue;
                var ir = VerbGrammar.IrPresent[p];
                if (d.Refl)
                {
                    var pron = VerbGrammar.ReflexivePronouns[p];
                    forms[p] = $"{pron} {ir} a {bare}|{ir} a {bare}{pron}";
                    whys[p] = "refl";
                }
                else forms[p] = $"{ir} a {bare}";
            }
            v.Forms["near"] = forms;
            if (whys.Any(w => w is not null)) v.Whys["near"] = whys;
        }
        return v;
    }
}

/// <summary>All verbs, loaded once.</summary>
public sealed class VerbBook
{
    public IReadOnlyList<Verb> Verbs { get; }
    public IReadOnlyDictionary<string, Verb> ByInf { get; }

    /// <summary>The example stories, one per tense (wwwroot/data/stories.json). Empty if the file is missing.</summary>
    public IReadOnlyList<Story> Stories { get; set; } = [];

    public Story? StoryFor(string tense) => Stories.FirstOrDefault(s => s.Tense == tense);

    public VerbBook(IReadOnlyList<Verb> verbs)
    {
        Verbs = verbs;
        ByInf = verbs.ToDictionary(v => v.Inf);
    }

    /// <summary>
    /// Verbs that follow a pattern in every person of a tense (used to practise that pattern):
    /// no irregular forms, not reflexive, not a weather verb. Most common first.
    /// </summary>
    public IReadOnlyList<Verb> PatternSamples(string tense, string pattern)
    {
        var key = tense + "|" + pattern;
        if (_samples.TryGetValue(key, out var cached)) return cached;
        var persons = VerbGrammar.PersonsFor(tense).Count;
        var list = Verbs
            .Where(v => !v.Reflexive && !v.OnlyThird && v.Has(tense) && v.PatternIn(tense) == pattern)
            .Where(v => Enumerable.Range(0, persons).All(p => v.HasForm(tense, p) && v.Why(tense, p) is null))
            .OrderBy(v => v.Rank)
            .ToList();
        _samples[key] = list;
        return list;
    }

    private readonly Dictionary<string, IReadOnlyList<Verb>> _samples = new();
}
