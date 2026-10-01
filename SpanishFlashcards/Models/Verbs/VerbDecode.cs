namespace SpanishFlashcards.Models.Verbs;

/// <summary>One way of reading a form: the verb, the tense and the person group (yo, tu, el, nos, vos, ellos).</summary>
public sealed record DecodeReading(string Inf, string Tense, string Group);

/// <summary>
/// A form to decode: shown alone, heard, or inside a story sentence. Readings are every correct answer
/// (fue is ser or ir; hablaba is yo or él; in a story, the context decides).
/// </summary>
public sealed record DecodeItem(Verb Verb, string Tense, int Person, string Form, IReadOnlyList<DecodeReading> Readings,
                                double Weight, bool Strange, StorySentence? Sentence = null, int Token = -1, Story? Story = null)
{
    public string Key => $"dec:{Verb.Inf}|{Tense}|{VerbGrammar.PersonsFor(Tense)[Person].Id}";

    /// <summary>What's shown: "no hables" for a "don't" command.</summary>
    public string Display => VerbGrammar.PersonsFor(Tense)[Person].Negative ? "no " + Form : Form;
}

/// <summary>An entry in the strange stems table: a stem (or a few whole forms) that doesn't look like its verb.</summary>
public sealed record StrangeStem(string Label, string[] Stems, bool Whole, string[] Verbs, string[] Tenses, string? Note = null);

/// <summary>A row of the endings cheat sheet: a signal, what it means, and examples (verb, tense, person).</summary>
public sealed record GuideRow(string Signal, string Meaning, (string Inf, string Tense, int Person)[] Examples);

public sealed record GuideSection(string Title, string Intro, IReadOnlyList<GuideRow> Rows);

/// <summary>
/// Decoding: going from a form you meet (tuve) back to the verb, the person and the tense (tener · yo · preterite).
/// Builds the question pool, the answer options and the clues, and holds the strange stems table and the
/// endings cheat sheet.
/// </summary>
public sealed class VerbDecode
{
    // ------------------------------------------------------------------ static content

    public static readonly IReadOnlyList<(string Title, IReadOnlyList<StrangeStem> Stems)> StrangeStems =
    [
        ("Past: preterite and past subjunctive", [
            new("tuv-", ["tuv"], false, ["tener"], ["pret", "impsubj"]),
            new("estuv-", ["estuv"], false, ["estar"], ["pret", "impsubj"]),
            new("anduv-", ["anduv"], false, ["andar"], ["pret", "impsubj"]),
            new("pud-", ["pud"], false, ["poder"], ["pret", "impsubj"]),
            new("pus-", ["pus"], false, ["poner"], ["pret", "impsubj"]),
            new("sup-", ["sup"], false, ["saber"], ["pret", "impsubj"]),
            new("hub-", ["hub"], false, ["haber"], ["pret", "impsubj"], "*hubo* = there was; *hubiera* is the helper in *hubiera hecho*."),
            new("quis-", ["quis"], false, ["querer"], ["pret", "impsubj"]),
            new("hic- / hiz-", ["hic", "hiz"], false, ["hacer"], ["pret", "impsubj"]),
            new("vin-", ["vin"], false, ["venir"], ["pret", "impsubj"]),
            new("dij-", ["dij"], false, ["decir"], ["pret", "impsubj"]),
            new("traj-", ["traj"], false, ["traer"], ["pret", "impsubj"]),
            new("fu-", ["fu"], false, ["ser", "ir"], ["pret", "impsubj"], "Two verbs, the same forms: *fue* is \"he was\" or \"he went\". The sentence tells you which."),
            new("di- / dio", ["di", "dio"], false, ["dar"], ["pret", "impsubj"], "*di* = I gave. (*¡Di!* is also \"say!\", from *decir*.)"),
            new("vi- / vio", ["vi"], false, ["ver"], ["pret", "impsubj"]),
        ]),
        ("Present", [
            new("teng- / tien-", ["teng", "tien"], false, ["tener"], ["pres", "subj"]),
            new("veng- / vien-", ["veng", "vien"], false, ["venir"], ["pres", "subj"]),
            new("dig- / dic-", ["dig", "dic"], false, ["decir"], ["pres", "subj"]),
            new("hag-", ["hag"], false, ["hacer"], ["pres", "subj"]),
            new("pong-", ["pong"], false, ["poner"], ["pres", "subj"]),
            new("salg-", ["salg"], false, ["salir"], ["pres", "subj"]),
            new("traig-", ["traig"], false, ["traer"], ["pres", "subj"]),
            new("caig-", ["caig"], false, ["caer"], ["pres", "subj"]),
            new("oig- / oy-", ["oig", "oy"], false, ["oír"], ["pres", "subj"]),
            new("voy, vas, va", ["voy", "vas", "va", "vamos", "vais", "van"], true, ["ir"], ["pres"]),
            new("soy, eres, es", ["soy", "eres", "es", "somos", "sois", "son"], true, ["ser"], ["pres"]),
            new("sé", ["sé"], true, ["saber"], ["pres"], "*sé* = I know. (*¡Sé!* is also \"be!\", from *ser*.)"),
            new("he, has, ha", ["he", "has", "ha", "hemos", "habéis", "han"], true, ["haber"], ["pres"], "The helper in *he hablado*: I have spoken."),
            new("quier-", ["quier"], false, ["querer"], ["pres", "subj"]),
            new("pued-", ["pued"], false, ["poder"], ["pres", "subj"]),
            new("duerm- / durm-", ["duerm", "durm"], false, ["dormir"], ["pres", "subj", "pret"]),
            new("pid-", ["pid"], false, ["pedir"], ["pres", "subj", "pret"]),
            new("sig-", ["sig"], false, ["seguir"], ["pres", "subj", "pret"]),
            new("jueg-", ["jueg"], false, ["jugar"], ["pres", "subj"]),
            new("vuelv-", ["vuelv"], false, ["volver"], ["pres", "subj"]),
        ]),
        ("Imperfect", [
            new("ib-", ["ib"], false, ["ir"], ["impf"], "*iba* = I/he was going, used to go."),
            new("er-", ["er"], false, ["ser"], ["impf"], "*era* = I/he was."),
            new("ve-", ["ve"], false, ["ver"], ["impf"], "*veía* = I/he was seeing, used to see."),
        ]),
        ("Future and conditional", [
            new("tendr-", ["tendr"], false, ["tener"], ["fut", "cond"]),
            new("vendr-", ["vendr"], false, ["venir"], ["fut", "cond"]),
            new("pondr-", ["pondr"], false, ["poner"], ["fut", "cond"]),
            new("saldr-", ["saldr"], false, ["salir"], ["fut", "cond"]),
            new("podr-", ["podr"], false, ["poder"], ["fut", "cond"]),
            new("sabr-", ["sabr"], false, ["saber"], ["fut", "cond"]),
            new("querr-", ["querr"], false, ["querer"], ["fut", "cond"]),
            new("habr-", ["habr"], false, ["haber"], ["fut", "cond"], "*habrá* = there will be; also the helper in *habré hecho*."),
            new("dir-", ["dir"], false, ["decir"], ["fut", "cond"]),
            new("har-", ["har"], false, ["hacer"], ["fut", "cond"]),
        ]),
        ("Present subjunctive", [
            new("vay-", ["vay"], false, ["ir"], ["subj"]),
            new("se-", ["se"], false, ["ser"], ["subj"], "*sea* = (that) it be."),
            new("sep-", ["sep"], false, ["saber"], ["subj"]),
            new("hay-", ["hay"], false, ["haber"], ["subj"], "*haya* = (that) there be; also the helper in *haya hecho*."),
            new("dé, des, den", ["dé", "des", "demos", "deis", "den"], true, ["dar"], ["subj"]),
        ]),
        ("Short commands", [
            new("di", ["di"], true, ["decir"], ["cmd"], "Say! (Also *di* = I gave, from *dar*.)"),
            new("haz", ["haz"], true, ["hacer"], ["cmd"]),
            new("ve", ["ve"], true, ["ir"], ["cmd"], "Go! (Also *ve* = he sees, from *ver*.)"),
            new("pon", ["pon"], true, ["poner"], ["cmd"]),
            new("sal", ["sal"], true, ["salir"], ["cmd"]),
            new("sé", ["sé"], true, ["ser"], ["cmd"], "Be! (Also *sé* = I know, from *saber*.)"),
            new("ten", ["ten"], true, ["tener"], ["cmd"]),
            new("ven", ["ven"], true, ["venir"], ["cmd"], "Come! (Also *ven* = they see, from *ver*.)"),
        ]),
        ("Participles (he …, había …)", [
            new("hecho", ["hech"], false, ["hacer"], ["perf"]),
            new("dicho", ["dich"], false, ["decir"], ["perf"]),
            new("puesto", ["puest"], false, ["poner"], ["perf"]),
            new("visto", ["vist"], false, ["ver"], ["perf"]),
            new("vuelto", ["vuelt"], false, ["volver"], ["perf"]),
            new("muerto", ["muert"], false, ["morir"], ["perf"]),
            new("escrito", ["escrit"], false, ["escribir"], ["perf"]),
            new("abierto", ["abiert"], false, ["abrir"], ["perf"]),
            new("roto", ["rot"], false, ["romper"], ["perf"]),
        ]),
    ];

    public static readonly IReadOnlyList<GuideSection> Guide =
    [
        new("Who: look at the end", "The last letters tell you who's doing it, in almost every tense.",
        [
            new("-mos", "we (*nosotros*), in every tense", [("hablar", "pres", 3), ("comer", "impf", 3), ("ir", "fut", 3)]),
            new("-n", "they, or you all (*ellos, ustedes*)", [("hablar", "pres", 5), ("comer", "pret", 5), ("ir", "fut", 5)]),
            new("-s", "you (*tú*), in most tenses", [("hablar", "pres", 1), ("comer", "impf", 1), ("ir", "fut", 1)]),
            new("-ste", "you (*tú*) in the preterite: no *-s* at the end!", [("hablar", "pret", 1), ("comer", "pret", 1)]),
            new("-is (-áis, -éis, -ís)", "you all (*vosotros*, Spain)", [("hablar", "pres", 4), ("comer", "pres", 4)]),
            new("-o", "I (*yo*), present", [("hablar", "pres", 0), ("comer", "pres", 0)]),
            new("-é / -í", "I (*yo*), preterite (special stems use *-e*: *tuve*)", [("hablar", "pret", 0), ("comer", "pret", 0), ("tener", "pret", 0)]),
            new("-ó / -ió", "he, she, you formal, preterite (special stems use *-o*: *tuvo*)", [("hablar", "pret", 2), ("comer", "pret", 2), ("tener", "pret", 2)]),
            new("no extra ending", "he, she, you formal. In the imperfect, conditional and subjunctives it's also I: *hablaba* = I/he was speaking", [("hablar", "pres", 2), ("hablar", "impf", 0), ("hablar", "cond", 0)]),
        ]),
        new("When: look just before the end", "Most tenses have a sign between the stem and the person ending.",
        [
            new("-aba- / -ía-", "was …ing, used to (imperfect)", [("hablar", "impf", 0), ("comer", "impf", 3)]),
            new("whole infinitive + -é, -ás, -á…", "will (future)", [("hablar", "fut", 0), ("comer", "fut", 2)]),
            new("whole infinitive + -ía…", "would (conditional)", [("hablar", "cond", 0), ("comer", "cond", 3)]),
            new("-ra- / -se-", "past subjunctive: if … did, (that) … would", [("hablar", "impsubj", 0), ("comer", "impsubj", 2)]),
            new("the \"wrong\" vowel", "present subjunctive: *-ar* verbs take *e*, *-er/-ir* verbs take *a*. Comes after *que*, *ojalá*…", [("hablar", "subj", 2), ("comer", "subj", 2)]),
            new("-é, -aste, -ó / -í, -iste, -ió", "did (preterite)", [("hablar", "pret", 2), ("comer", "pret", 1)]),
        ]),
        new("Two words: haber + -ado / -ido", "The first word says who and when; the second (the participle) says which verb.",
        [
            new("he, has, ha… + -ado", "have done (present perfect)", [("hablar", "perf", 0), ("comer", "perf", 2)]),
            new("había… + -ado", "had done", [("hablar", "plup", 0)]),
            new("habré… / habría… + -ado", "will have done / would have done", [("hablar", "futperf", 0), ("hablar", "condperf", 0)]),
            new("haya… / hubiera… + -ado", "(that) … has done / (if) … had done", [("hablar", "subjperf", 0), ("hablar", "plupsubj", 0)]),
        ]),
        new("Look-alikes", "Small differences that change the meaning.",
        [
            new("hablo / habló", "I speak / he spoke: only the accent differs", [("hablar", "pres", 0), ("hablar", "pret", 2)]),
            new("hablamos", "we speak or we spoke: the same form for *-ar* and *-ir* verbs. The sentence tells you", [("hablar", "pres", 3), ("vivir", "pret", 3)]),
            new("hablara / hablará", "if he spoke / he will speak", [("hablar", "impsubj", 2), ("hablar", "fut", 2)]),
            new("habla / hable", "he speaks / (that) he speak", [("hablar", "pres", 2), ("hablar", "subj", 2)]),
        ]),
    ];

    public static string TenseLabel(string t) => t switch
    {
        "pres" => "Present (does)",
        "pret" => "Preterite (did)",
        "impf" => "Imperfect (was doing, used to)",
        "fut" => "Future (will)",
        "cond" => "Conditional (would)",
        "subj" => "Present subjunctive (that … do)",
        "impsubj" => "Past subjunctive (if … did)",
        "cmd" => "Command (do it!)",
        "perf" => "Present perfect (has done)",
        "plup" => "Pluperfect (had done)",
        "futperf" => "Future perfect (will have done)",
        "condperf" => "Conditional perfect (would have done)",
        "subjperf" => "Perfect subjunctive (that … has done)",
        _ => "Pluperfect subjunctive (if … had done)",
    };

    public static readonly IReadOnlyList<(string Group, string Label)> WhoOptions =
    [
        ("yo", "yo (I)"), ("tu", "tú (you)"), ("el", "él / ella / usted"), ("nos", "nosotros (we)"),
        ("vos", "vosotros (you all, Spain)"), ("ellos", "ellos / ustedes (they)"),
    ];

    private static readonly string[] SimpleTenses = ["pres", "pret", "impf", "fut", "cond", "subj", "impsubj", "cmd"];
    private static readonly string[] CompoundTenses = ["perf", "plup", "futperf", "condperf", "subjperf", "plupsubj"];

    // ------------------------------------------------------------------ the pool

    private readonly VerbBook book;
    private readonly Dictionary<string, List<DecodeReading>> index = new();
    public IReadOnlyList<DecodeItem> Items { get; }
    public IReadOnlyList<DecodeItem> StoryItems { get; }

    public VerbDecode(VerbBook book)
    {
        this.book = book;
        foreach (var v in book.Verbs)
            foreach (var t in VerbGrammar.Tenses)
                for (var p = 0; p < VerbGrammar.PersonsFor(t.Id).Count; p++)
                    foreach (var f in v.Answers(t.Id, p).Take(1).Concat(AltForms(v, t.Id, p)))
                        AddReading(f, new DecodeReading(v.Inf, t.Id, VerbGrammar.PersonsFor(t.Id)[p].Group));

        var items = new List<DecodeItem>();
        foreach (var v in book.Verbs)
        {
            foreach (var t in SimpleTenses.Concat(CompoundTenses))
            {
                var compound = VerbGrammar.TenseById[t].IsCompound;
                if (compound && v.Participle == RegularParticiple(v)) continue; // nothing to decode
                for (var p = 0; p < VerbGrammar.PersonsFor(t).Count; p++)
                {
                    var form = v.Form(t, p);
                    if (form is null) continue;
                    var strange = IsStrange(v, t, form);
                    items.Add(new DecodeItem(v, t, p, form, ReadingsOf(form), Weight(v, t, p, form, strange) * (compound ? 0.5 : 1), strange));
                }
            }
        }
        Items = items;

        var storyItems = new List<DecodeItem>();
        foreach (var story in book.Stories)
        {
            foreach (var s in story.Sentences)
            {
                for (var i = 0; i < s.Tokens.Count; i++)
                {
                    var tok = s.Tokens[i];
                    if (!tok.IsVerb || !book.ByInf.TryGetValue(tok.V!, out var v)) continue;
                    var readings = tok.P!.Select(p => new DecodeReading(v.Inf, tok.Te!, VerbGrammar.PersonsFor(tok.Te!)[p].Group)).ToList();
                    var strange = IsStrange(v, tok.Te!, tok.X.ToLowerInvariant());
                    storyItems.Add(new DecodeItem(v, tok.Te!, tok.Person, tok.X, readings,
                        Weight(v, tok.Te!, tok.Person, tok.X.ToLowerInvariant(), strange), strange, s, i, story));
                }
            }
        }
        StoryItems = storyItems;
    }

    private static IEnumerable<string> AltForms(Verb v, string t, int p) => v.Answers(t, p).Skip(1);

    private void AddReading(string form, DecodeReading r)
    {
        var key = form.ToLowerInvariant();
        if (!index.TryGetValue(key, out var list)) index[key] = list = new();
        if (!list.Contains(r)) list.Add(r);
    }

    /// <summary>Every way of reading a form (lower case, without "no").</summary>
    public IReadOnlyList<DecodeReading> ReadingsOf(string form) =>
        index.TryGetValue(form.ToLowerInvariant(), out var list) ? list : [];

    private static string RegularParticiple(Verb v) => v.PatternStem("perf") + (v.Class == "ar" ? "ado" : "ido");

    /// <summary>The verb word of a form: the participle in "he hecho", the verb in "me acuerdo".</summary>
    private static string Word(string form) => form.Split(' ')[^1];

    public static StrangeStem? StemFor(Verb v, string t, string form)
    {
        var word = AnswerCheck.StripAccents(Word(form).ToLowerInvariant());
        var tense = VerbGrammar.TenseById[t].IsCompound ? "perf" : t;
        var inf = v.Reflexive ? v.Inf[..^2] : v.Inf;
        foreach (var (_, stems) in StrangeStems)
            foreach (var s in stems)
            {
                if (!s.Verbs.Contains(inf) || !s.Tenses.Contains(tense)) continue;
                var hit = s.Stems.Select(x => AnswerCheck.StripAccents(x)).Any(x => s.Whole ? word == x : word.StartsWith(x, StringComparison.Ordinal));
                if (hit) return s;
            }
        return null;
    }

    private static bool IsStrange(Verb v, string t, string form) => StemFor(v, t, form) is not null;

    /// <summary>
    /// How likely a form is to be asked: the less it looks like its infinitive, the more. With these weights
    /// a round is about half forms that don't start like their verb (fue, tuvo, iba), a third forms with a
    /// change (quiero, busqué) and a fifth regular forms (for reading the endings).
    /// </summary>
    private static double Weight(Verb v, string t, int p, string form, bool strange)
    {
        var w = 1.0;
        if (v.Why(t, p) is not null) w = 8;
        var inf = AnswerCheck.StripAccents(v.Reflexive ? v.Inf[..^2] : v.Inf);
        var word = AnswerCheck.StripAccents(Word(form).ToLowerInvariant());
        if (inf.Length >= 2 && !word.StartsWith(inf[..2], StringComparison.Ordinal)) w = Math.Max(w, 30);
        if (strange) w = Math.Max(w, 20);
        return w;
    }

    // ------------------------------------------------------------------ sessions

    public enum Mode { Read, Sentence, Listen }

    /// <summary>
    /// Ten forms: due ones first, then a few you got wrong or found hard, then new ones chosen at random but
    /// leaning heavily towards forms that don't look like their verb. No verb twice in a round.
    /// </summary>
    public List<DecodeItem> BuildSession(Mode mode, bool strangeOnly, IReadOnlyDictionary<string, VerbSkill> skills, int size = 10)
    {
        var rng = Random.Shared;
        var pool = (mode == Mode.Sentence ? StoryItems : Items)
            .Where(i => VerbSettings.IsActive(i.Tense, i.Person) && (!strangeOnly || i.Strange))
            .ToList();
        VerbSkill? S(DecodeItem i) => skills.TryGetValue(i.Key, out var s) ? s : null;
        var now = DateTime.UtcNow;
        var picks = new List<DecodeItem>();
        var verbs = new HashSet<string>();

        void Take(IEnumerable<DecodeItem> items, int max)
        {
            foreach (var i in items)
            {
                if (picks.Count >= size || max <= 0) return;
                if (verbs.Contains(i.Verb.Inf) || picks.Any(x => x.Key == i.Key)) continue;
                picks.Add(i);
                verbs.Add(i.Verb.Inf);
                max--;
            }
        }

        Take(pool.Where(i => VerbSrs.IsDue(S(i), now)).OrderBy(i => S(i)!.Due), size);
        Take(pool.Where(i => VerbSrs.IsSeen(S(i)) && !VerbSrs.IsDue(S(i), now)).OrderBy(i => S(i)!.Strength).Take(20).OrderBy(_ => rng.Next()), 3);

        // New forms: weighted random (each draw removes the item)
        var fresh = pool.Where(i => !VerbSrs.IsSeen(S(i))).ToList();
        var guard = 0;
        while (picks.Count < size && fresh.Count > 0 && guard++ < 500)
        {
            var total = fresh.Sum(i => i.Weight);
            var roll = rng.NextDouble() * total;
            var k = 0;
            for (; k < fresh.Count - 1; k++)
            {
                roll -= fresh[k].Weight;
                if (roll <= 0) break;
            }
            var item = fresh[k];
            fresh.RemoveAt(k);
            Take([item], 1);
        }
        // Everything seen and nothing new left: fill with the weakest.
        Take(pool.Where(i => VerbSrs.IsSeen(S(i))).OrderBy(i => S(i)!.Strength), size);
        return picks.OrderBy(_ => rng.Next()).ToList();
    }

    // ------------------------------------------------------------------ options and grading

    /// <summary>4 verbs: every correct one (fue: ser and ir) plus look-alikes that start the same way.</summary>
    public List<Verb> VerbOptions(DecodeItem item)
    {
        var rng = Random.Shared;
        var right = item.Readings.Select(r => r.Inf).Distinct().Select(i => book.ByInf[i]).ToList();
        var word = AnswerCheck.StripAccents(Word(item.Form).ToLowerInvariant());
        int Shared(Verb v)
        {
            var inf = AnswerCheck.StripAccents(v.Reflexive ? v.Inf[..^2] : v.Inf);
            var n = 0;
            while (n < inf.Length && n < word.Length && inf[n] == word[n]) n++;
            return n;
        }
        var others = book.Verbs.Where(v => !right.Contains(v) && v.Reflexive == item.Verb.Reflexive)
            .Select(v => (v, score: Shared(v) * 2 + rng.NextDouble() * 2.5))
            .OrderByDescending(x => x.score).Take(4 - right.Count).Select(x => x.v);
        return right.Concat(others).OrderBy(_ => rng.Next()).ToList();
    }

    /// <summary>6 tenses: the right one(s), look-alikes and common ones, in the usual order.</summary>
    public static List<string> WhenOptions(DecodeItem item)
    {
        var rng = Random.Shared;
        var set = item.Readings.Select(r => r.Tense).Distinct().ToList();
        var compound = VerbGrammar.TenseById[item.Tense].IsCompound;
        var fill = (compound ? CompoundTenses : SimpleTenses).OrderBy(_ => rng.Next());
        foreach (var t in fill)
        {
            if (set.Count >= 6) break;
            if (!set.Contains(t)) set.Add(t);
        }
        return VerbGrammar.Tenses.Select(t => t.Id).Where(set.Contains).ToList();
    }

    /// <summary>
    /// All three right: Right. Right verb with one of who / when wrong: half (shown like a missing accent).
    /// Otherwise: Wrong. Any correct reading counts.
    /// </summary>
    public static Grade GradeOf(DecodeItem item, string inf, string group, string tense)
    {
        var mine = item.Readings.Where(r => r.Inf == inf).ToList();
        if (mine.Count == 0) return Grade.Wrong;
        var best = mine.Max(r => (r.Group == group ? 1 : 0) + (r.Tense == tense ? 1 : 0));
        return best switch { 2 => Grade.Right, 1 => Grade.AccentSlip, _ => Grade.Wrong };
    }

    // ------------------------------------------------------------------ clues

    /// <summary>Short reasons the answer is what it is: the stem, the person ending, the tense sign.</summary>
    public static List<string> Clues(DecodeItem item)
    {
        var lines = new List<string>();
        var t = item.Tense;
        var v = item.Verb;
        var info = VerbGrammar.TenseById[t];
        var group = VerbGrammar.PersonsFor(t)[item.Person].Group;

        if (StemFor(v, t, item.Form.ToLowerInvariant()) is { } stem)
            lines.Add($"*{stem.Label}* → *{string.Join(" / ", stem.Verbs)}*" + (stem.Note is null ? "" : $". {stem.Note}"));

        if (info.IsCompound)
        {
            var words = item.Form.Split(' ');
            var aux = words.Length >= 2 ? words[^2] : "";
            lines.Add($"*{aux}* (a form of *haber*) says who and when; *{words[^1]}* says which verb.");
            lines.Add($"{TenseLabel(t)}.");
            return lines;
        }

        var person = (group, t) switch
        {
            ("nos", _) => "*-mos* = we",
            ("ellos", _) => "*-n* = they / you all",
            ("vos", _) => "*-is* = you all (vosotros)",
            ("tu", "pret") => "*-ste* = you (past)",
            ("tu", "cmd") => "an order to *tú*",
            ("tu", _) => "*-s* = you",
            ("yo", "pres") => "*-o* = I",
            ("yo", "pret") => "*-é / -í* (or *-e* on a special stem) = I",
            ("yo", "fut") => "*-é* = I",
            ("yo", _) => "same form as *él*: the sentence tells you who",
            ("el", "pret") => "*-ó / -ió* (or *-o* on a special stem) = he / she",
            ("el", "fut") => "*-á* = he / she",
            ("el", "cmd") => "an order to *usted*",
            ("el", "pres") => "no extra ending = he / she / you formal",
            _ => "no extra ending = he / she (or I)",
        };
        lines.Add(person + ".");

        lines.Add(t switch
        {
            "impf" => "*-aba-* / *-ía-* = was …ing, used to.",
            "fut" => "Whole infinitive (or a short stem) + ending = will.",
            "cond" => "Whole infinitive (or a short stem) + *-ía* = would.",
            "subj" => "The \"wrong\" vowel (*-ar* → *e*, *-er/-ir* → *a*) = present subjunctive.",
            "impsubj" => "*-ra-* / *-se-* = past subjunctive (if … did).",
            "cmd" => "No subject, often with *¡!*: a command.",
            "pret" => "Past endings = did (preterite).",
            _ => "Plain present endings = does / is doing.",
        });
        return lines;
    }
}
