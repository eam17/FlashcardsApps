namespace SpanishFlashcards.Models.Verbs;

// ---------------------------------------------------------------------------------------------
// Building blocks of a Rules page. Text uses *Spanish* (shown highlighted) and **bold** markup.
// ---------------------------------------------------------------------------------------------

public abstract record RuleBlock;

/// <summary>The "In short" box at the top of every page.</summary>
public sealed record RuleSummary(string Text) : RuleBlock;
public sealed record RuleHeading(string Text) : RuleBlock;
public sealed record RulePara(string Text) : RuleBlock;
public sealed record RuleList(IReadOnlyList<string> Items) : RuleBlock;

/// <summary>A folded section: tap the title to open it.</summary>
public sealed record RuleSection(string Title, IReadOnlyList<RuleBlock> Blocks, bool Open = false) : RuleBlock;

/// <summary>Example sentences. In <c>Es</c> the verb is marked with [square brackets].</summary>
public sealed record RuleExamples(IReadOnlyList<(string Es, string En)> Items) : RuleBlock;

/// <summary>Small chips such as clue words (ayer, anoche…).</summary>
public sealed record RuleChips(string Label, IReadOnlyList<string> Items) : RuleBlock;

/// <summary>A memory trick.</summary>
public sealed record RuleTip(string Title, string Text) : RuleBlock;

/// <summary>Two things side by side, row by row.</summary>
public sealed record RuleCompare(string Title, string Left, string Right, IReadOnlyList<(string Left, string Right)> Rows) : RuleBlock;

/// <summary>How a form is built, step by step: hablar → habl + o = hablo.</summary>
public sealed record RuleRecipe(IReadOnlyList<RecipeLine> Lines) : RuleBlock;
public sealed record RecipeLine(string? Label, IReadOnlyList<RecipeItem> Items);
public abstract record RecipeItem;
/// <summary>A piece of a word. Kind null = neutral (an infinitive or a starting form).</summary>
public sealed record RecipeChip(string Text, PartKind? Kind) : RecipeItem;
public sealed record RecipeOp(string Text) : RecipeItem;
/// <summary>The finished form, coloured.</summary>
public sealed record RecipeForm(Verb Verb, string Tense, int Person, string Form) : RecipeItem;

/// <summary>The endings of a regular pattern, shown on a real verb.</summary>
public sealed record RuleEndings(string Tense, string Pattern, Verb Sample) : RuleBlock;

/// <summary>One verb's table. Highlighted forms are the ones the page is about.</summary>
public sealed record RuleTable(Verb Verb, string Tense, IReadOnlySet<int>? Highlight, string? Caption = null) : RuleBlock;

/// <summary>Sentences from the example stories that use the forms a page is about.</summary>
public sealed record RuleStoryLines(IReadOnlyList<StoryLine> Lines) : RuleBlock;

/// <summary>"Compared with regular verbs": what's the same as the plain pattern and what's different (see VerbCompare).</summary>
public sealed record RuleVsRegular(string Title, IReadOnlyList<string> Lines) : RuleBlock;

/// <summary>What the colours in the tables mean.</summary>
public sealed record RuleColorKey : RuleBlock;

/// <summary>A list of verbs.</summary>
public sealed record RuleVerbs(string Label, IReadOnlyList<Verb> Verbs) : RuleBlock;

/// <summary>A one-tap question at the end of a page.</summary>
public sealed record RuleCheck(string Question, IReadOnlyList<string> Options, int Answer, string Explanation, bool SpanishOptions) : RuleBlock;

public sealed record RulePage(string Title, IReadOnlyList<RuleBlock> Blocks);

/// <summary>
/// Plain-English explanations for every item in the Verbs tree. Every page has the same shape:
/// a short summary, the coloured table, how the form is built, examples, then details folded away,
/// and a one-tap check. Each level only says what's new at that level.
/// </summary>
public static class VerbRules
{
    public static readonly IReadOnlyList<(string Term, string Meaning)> Glossary =
    [
        ("Infinitive", "the dictionary form of a verb: *hablar* (to speak). It ends in *-ar*, *-er* or *-ir*."),
        ("Stem", "the infinitive without *-ar*, *-er* or *-ir*: *habl-*. It carries the meaning."),
        ("Ending", "goes on the stem to show who and when: *habl* + *o* = *hablo* (I speak)."),
        ("Person", "who does it: *yo* (I), *tú* (you, informal), *él / ella / usted* (he, she, you formal), *nosotros* (we), *vosotros* (you all, Spain), *ellos / ellas / ustedes* (they, you all). *Usted* and *ustedes* use the *él* and *ellos* forms."),
        ("Tense", "when it happens. The subjunctive and commands are called moods: they're about wishes and orders rather than time."),
        ("Conjugate", "change the ending to match the person and the tense."),
    ];

    public static RulePage For(VerbNode node, VerbBook book) => node.Kind switch
    {
        NodeKind.Root => Root(book),
        NodeKind.Tense => TensePage(node, book),
        NodeKind.Type => TypePage(node, book),
        NodeKind.Verb => VerbPage(node, book),
        _ => node.Pattern is not null ? PatternPage(node, book) : GroupPage(node, book),
    };

    // ============================================================== root

    private static RulePage Root(VerbBook book)
    {
        var hablar = book.ByInf["hablar"];
        return new("How Spanish verbs work",
        [
            new RuleSummary("A Spanish verb changes its **ending** to show **who** does it and **when**. *Hablo* already means \"I speak\", so *yo* is usually left out. Most verbs follow a few patterns: learn a pattern once and it works for hundreds of verbs."),
            new RuleTable(hablar, "pres", null),
            new RuleRecipe([Recipe(hablar, "pres", 0)]),
            new RuleSection("Key words", [new RuleList(Glossary.Select(g => $"**{g.Term}**: {g.Meaning}").ToList())], Open: true),
            new RuleSection("Colours in the tables", [new RuleColorKey()]),
            new RuleSection("How this tab is organised",
            [
                new RuleList([
                    "**Tense**: when it happens, or a mood (subjunctive, commands).",
                    "**Type**: regular verbs, stem-changing verbs, unique verbs…",
                    "**Group**: verbs that change the same way, like *o → ue*.",
                    "**Verb**: one verb in that tense.",
                ]),
                new RulePara("Each item has Rules, Practice, a Test and a progress bar. Practice and tests cover everything inside the item."),
                new RulePara("**Decode** (the switch at the top) works the other way round: you meet a form like *tuve* and work out the verb, who and when. It also has a cheat sheet of endings and a table of strange stems (*tuv-* → *tener*)."),
            ]),
            new RuleSection("How practice works",
            [
                new RuleList([
                    "New forms: multiple choice, 6 look-alike options. Once you know a form, you spell it from letter tiles, with a few trap letters mixed in.",
                    "Picking a vowel without its accent counts as half a mistake: *hablo* (I speak) and *habló* (he spoke) are different words.",
                    "Regular verbs: you practise the endings, with a different verb each time.",
                    "Stem-changing and *-go* verbs: just the forms that change.",
                    "Unique verbs: every form.",
                    "Right answers come back less and less often; a mistake comes back in minutes.",
                ]),
                new RulePara("**Tests** check a whole tense or group in one go, spelling only. Groups you pass are marked as known (they come back once, about a week later); groups you miss show up under *Weak spots*. Score 90% or more and the item is **Mastered** for as long as you keep remembering it."),
            ]),
            new RuleCheck("*Hablo* means:", ["I speak", "he spoke", "we speak", "Speak!"], 0,
                "The ending *-o* means *yo* (I), so *hablo* is \"I speak\" with no *yo* needed.", SpanishOptions: false),
        ]);
    }

    // ============================================================== tenses

    private sealed record TenseDoc(
        string Summary,
        string How,
        (string Es, string En)[] Examples,
        string[] Clues,
        string Irregular,
        (string Title, string Text)[] Tips,
        string? Compare,
        RuleCheck Check,
        string[]? HowList = null);

    private static RuleCheck Q(string question, string[] options, string explanation) =>
        new(question, options, 0, explanation, SpanishOptions: true);

    private const string ParticipleHow = "The participle is the stem + *-ado* (*-ar* verbs) or *-ido* (*-er*, *-ir*): *hablado, comido, vivido*. It never changes; only *haber* does.";
    private static readonly (string, string) CompoundTip = ("Only haber changes", "Learn *haber* in this tense and you're done: the participle (*hablado, hecho*) is the same in every compound tense. Pronouns go in front: *lo he visto*.");

    private static TenseDoc Doc(string t) => t switch
    {
        "pres" => new(
            "What's true now, what you do regularly, and what's happening right now. One form covers both *I eat* and *I'm eating*.",
            "Drop *-ar*, *-er* or *-ir* and add the ending. *-ar* verbs use *a*; *-er* and *-ir* use *e* (they only differ in *nosotros* and *vosotros*).",
            [("[Vivo] en Madrid.", "I live in Madrid."), ("¿Qué [haces]?", "What are you doing?"), ("Mañana [voy] al cine.", "Tomorrow I'm going to the cinema.")],
            ["ahora", "siempre", "cada día", "normalmente"],
            "Many common verbs change a vowel (*quiero*), have a special *yo* form (*tengo*), or are unique (*soy*).",
            [("Skip the yo", "The ending already says who: *hablo* = I speak. Add *yo* only for emphasis.")],
            null,
            Q("How do you say \"I'm eating\"?", ["como", "estoy comer", "comí", "comeré"], "*Como* means both \"I eat\" and \"I'm eating\".")),
        "pret" => new(
            "Finished actions at a definite time: *I spoke, she ate*. Use it for the events of a story.",
            "Drop the ending and add these. *-er* and *-ir* verbs share the same endings.",
            [("Ayer [comí] paella.", "Yesterday I ate paella."), ("[Llegó], [abrió] la puerta y [salió].", "He arrived, opened the door and left.")],
            ["ayer", "anoche", "el año pasado", "una vez", "de repente"],
            "Spelling changes in *yo* (*busqué*), *-ir* stem changes in *él* and *ellos* (*pidió*), special stems (*tuve*), and *ser* and *ir* sharing *fui*.",
            [("The accent matters", "*hablo* = I speak, *habló* = he spoke. The accent is the only difference.")],
            "pret-impf",
            Q("Ayer ___ paella. (I ate)", ["comí", "comía", "como", "comeré"], "Finished, at a definite time (*ayer*): preterite.")),
        "impf" => new(
            "The past as background: what used to happen, what was going on, how things were. English says *used to* or *was ...ing*.",
            "*-ar* verbs add *-aba*; *-er* and *-ir* verbs add *-ía*.",
            [("De niño [jugaba] al fútbol.", "As a child I used to play football."), ("[Leía] cuando sonó el teléfono.", "I was reading when the phone rang."), ("[Era] tarde y [llovía].", "It was late and it was raining.")],
            ["siempre", "todos los días", "de niño", "mientras", "a menudo"],
            "Only *ser*, *ir* and *ver*.",
            [("Only three irregular verbs", "*ser → era*, *ir → iba*, *ver → veía*. Every other verb is regular.")],
            "pret-impf",
            Q("De niño ___ al fútbol. (I used to play)", ["jugaba", "jugué", "juego", "jugaría"], "A past habit: imperfect.")),
        "fut" => new(
            "What will happen: *I will speak*. Also a guess about now: *Serán las tres* (It must be about three).",
            "Keep the whole infinitive and add the ending. The endings are the same for *-ar*, *-er* and *-ir*.",
            [("Mañana [lloverá].", "It will rain tomorrow."), ("¿Dónde [estará] Ana?", "Where can Ana be?")],
            ["mañana", "la semana que viene", "pronto", "algún día"],
            "About a dozen verbs use a shorter stem: *tendré, podré, diré*. The endings stay the same.",
            [("Endings you already know", "They sound like *haber*: *he, has, ha, hemos, habéis, han* → *-é, -ás, -á, -emos, -éis, -án*. That's where they came from."),
             ("In conversation", "*Voy a comer* (I'm going to eat) is often used instead, like English \"going to\".")],
            "fut-cond",
            Q("Which means \"I will speak\"?", ["hablaré", "hablaría", "hablé", "hable"], "Infinitive + *-é*: *hablaré*.")),
        "cond" => new(
            "What would happen: *I would speak*. Also for polite requests: *¿Podrías ayudarme?* (Could you help me?)",
            "Keep the whole infinitive and add the *-ía* endings.",
            [("Yo no lo [haría].", "I wouldn't do it."), ("Me [gustaría] un café.", "I'd like a coffee."), ("Si tuviera tiempo, [viajaría].", "If I had time, I would travel.")],
            ["me gustaría", "podría", "yo que tú", "si…"],
            "The same short stems as the future: *tendría, podría, diría*.",
            [("Same stems as the future", "Know *tendré* and you know *tendría*.")],
            "fut-cond",
            Q("Me ___ un café. (I'd like)", ["gustaría", "gustará", "gustó", "gustaba"], "\"Would\": conditional.")),
        "perf" => new(
            "*I have spoken*: things done in a time that isn't over (today, this week) and life experiences.",
            "A form of *haber* + the participle. " + ParticipleHow,
            [("Hoy [he trabajado] mucho.", "I've worked a lot today."), ("¿[Has estado] en México?", "Have you been to Mexico?"), ("Todavía no [he comido].", "I haven't eaten yet.")],
            ["hoy", "esta semana", "ya", "todavía no", "alguna vez", "nunca"],
            "Only the participle can be irregular: *hecho, dicho, visto*.",
            [CompoundTip],
            "perf-pret",
            Q("Which means \"I have spoken\"?", ["he hablado", "había hablado", "hablé", "habré hablado"], "Present of *haber* + participle.")),
        "subj" => new(
            "Not a time but a mood: for wishes, feelings, doubts and things that aren't real yet. It usually comes after *que*.",
            "Take the *yo* form, drop the *-o*, and swap the vowel: *-ar* verbs take *e*, *-er* and *-ir* verbs take *a*.",
            [("Quiero que [vengas].", "I want you to come."), ("No creo que [llueva].", "I don't think it'll rain."), ("Ojalá [tengas] suerte.", "I hope you're lucky."), ("Cuando [llegues], llámame.", "When you arrive, call me.")],
            ["quiero que", "espero que", "ojalá", "para que", "no creo que", "cuando…"],
            "Anything odd in the *yo* form carries over (*tengo → tenga*), plus six unique verbs.",
            [("Swap the vowel", "*-ar* → *e*, *-er/-ir* → *a*: the \"opposite\" vowel. *hablas → hables*, *comes → comas*."),
             ("DISHES", "The six unique ones: **D**ar, **I**r, **S**aber, **H**aber, **E**star, **S**er (*dé, vaya, sepa, haya, esté, sea*).")],
            "pres-subj",
            Q("Quiero que tú ___. (I want you to come)", ["vengas", "vienes", "vendrás", "venir"], "A wish about someone else, after *que*: subjunctive.")),
        "cmd" => new(
            "Telling someone to do, or not to do, something. The form depends on who you're talking to, and on \"do\" or \"don't\".",
            "Each command borrows a form you already know:",
            [("¡[Ven] aquí!", "Come here!"), ("No [toques] eso.", "Don't touch that."), ("[Vamos].", "Let's go.")],
            [],
            "Eight short *tú* commands; anything irregular in the present or subjunctive carries over.",
            [("Same word, two meanings", "*pregunta* can be \"he asks\" or \"ask!\": the tú command is the él form of the present. Context tells you: a command has no subject and often an exclamation mark (*¡Pregunta!*); \"he asks\" usually has one (*Luis pregunta*)."),
             ("Where pronouns go", "On the end of \"do\" commands (*dímelo*, *siéntate*), in front of \"don't\" ones (*no me lo digas*)."),
             ("In practice", "A \"don't\" command shows as *no ___*: answer with just the verb.")],
            "cmd-neg",
            Q("Tell a friend \"Don't touch that!\"", ["No toques eso.", "No tocas eso.", "No toca eso.", "No tocaste eso."], "\"Don't\" with *tú* uses the subjunctive: *toques*."),
            HowList: ["**tú**: the *él* form: *habla*", "**tú, don't**: *no* + subjunctive: *no hables*", "**usted, ustedes, let's**: subjunctive: *hable, hablen, hablemos*", "**vosotros**: infinitive, *-r* → *-d*: *hablad* (don't: *no habléis*)"]),
        "impsubj" => new(
            "The past subjunctive: for \"if\" situations that aren't real (*Si tuviera dinero…*, If I had money…) and for wishes in the past.",
            "Take the *ellos* preterite, drop *-ron*, add *-ra, -ras, -ra, -ramos, -rais, -ran*. *Nosotros* gets an accent.",
            [("Si [tuviera] dinero, viajaría.", "If I had money, I would travel."), ("Quería que [vinieras].", "I wanted you to come."), ("[Quisiera] un café.", "I'd like a coffee (very polite).")],
            ["si", "quería que", "ojalá", "como si"],
            "The same as the preterite: *pidiera, durmiera, leyera, tuviera, fuera*.",
            [("Start from ellos", "Know *tuvieron* and you know *tuviera*. Every irregular preterite carries over.")],
            "ra-se",
            Q("Si ___ dinero, viajaría. (If I had)", ["tuviera", "tengo", "tenga", "tendría"], "An unreal \"if\": imperfect subjunctive. *Tuviese* is right too.")),
        "plup" => new(
            "*I had spoken*: something that happened before another moment in the past.",
            "*había, habías, había, habíamos, habíais, habían* + the participle. " + ParticipleHow,
            [("Cuando llegué, la película ya [había empezado].", "When I arrived, the film had already started.")],
            ["ya", "cuando llegué", "antes de", "nunca"],
            "Only the participle: *hecho, dicho, visto*.",
            [CompoundTip], null,
            Q("Cuando llegué, la película ya ___. (had started)", ["había empezado", "ha empezado", "habrá empezado", "empezaba"], "Before another past moment: pluperfect.")),
        "futperf" => new(
            "*I will have spoken*: done by a point in the future. Also a guess about the past: *Habrá salido* (He's probably gone out).",
            "*habré, habrás, habrá, habremos, habréis, habrán* + the participle. " + ParticipleHow,
            [("Para el lunes [habré terminado].", "By Monday I'll have finished.")],
            ["para el lunes", "dentro de un año", "ya"],
            "Only the participle: *hecho, dicho, visto*.",
            [CompoundTip], null,
            Q("Para el lunes ___. (I will have finished)", ["habré terminado", "habría terminado", "he terminado", "había terminado"], "Future of *haber* + participle.")),
        "condperf" => new(
            "*I would have spoken*: what would have happened, but didn't.",
            "*habría, habrías, habría, habríamos, habríais, habrían* + the participle. " + ParticipleHow,
            [("Yo no lo [habría hecho].", "I wouldn't have done it."), ("Si lo hubiera sabido, [habría venido].", "If I had known, I would have come.")],
            ["yo que tú", "en tu lugar", "si hubiera…"],
            "Only the participle: *hecho, dicho, visto*.",
            [CompoundTip], "if",
            Q("Yo no lo ___. (I wouldn't have done it)", ["habría hecho", "habré hecho", "había hecho", "haya hecho"], "Conditional of *haber* + participle.")),
        "subjperf" => new(
            "*(that) I have spoken*, where the sentence needs the subjunctive: *Espero que hayas dormido bien* (I hope you've slept well).",
            "*haya, hayas, haya, hayamos, hayáis, hayan* + the participle. " + ParticipleHow,
            [("Espero que [hayas dormido] bien.", "I hope you've slept well."), ("No creo que [haya llegado].", "I don't think she has arrived.")],
            ["espero que", "no creo que", "me alegro de que", "ojalá"],
            "Only the participle: *hecho, dicho, visto*.",
            [CompoundTip], null,
            Q("Espero que ___ bien. (I hope you've slept)", ["hayas dormido", "has dormido", "hubieras dormido", "habrás dormido"], "After *espero que*: subjunctive of *haber* + participle.")),
        "plupsubj" => new(
            "*(if) I had spoken*: \"if\" about the past, for things that didn't happen.",
            "*hubiera, hubieras, hubiera, hubiéramos, hubierais, hubieran* + the participle. *Hubiese* is also correct and accepted. " + ParticipleHow,
            [("Si [hubiera estudiado], habría aprobado.", "If I had studied, I would have passed."), ("Ojalá lo [hubiera sabido].", "If only I'd known.")],
            ["si", "ojalá", "como si"],
            "Only the participle: *hecho, dicho, visto*.",
            [CompoundTip], "if",
            Q("Si lo ___, habría venido. (If I had known)", ["hubiera sabido", "había sabido", "habría sabido", "haya sabido"], "An unreal \"if\" about the past: pluperfect subjunctive.")),
        _ => new("", "", [], [], "", [], null, Q("", [""], "")),
    };

    private static RuleCompare Comparison(string id) => id switch
    {
        "pret-impf" => new("Preterite or imperfect?", "Preterite", "Imperfect",
        [
            ("What happened, once: *Comí paella.* (I ate paella.)", "Background or habit: *Comía paella.* (I used to eat paella.)"),
            ("Events: *Llegó y abrió la puerta.*", "Scene: *Era tarde y llovía.*"),
            ("*ayer, anoche, una vez*", "*siempre, de niño, mientras*"),
        ]),
        "fut-cond" => new("Future or conditional?", "Future", "Conditional",
        [
            ("will: *hablaré*", "would: *hablaría*"),
            ("*-é, -ás, -á…*", "*-ía, -ías, -ía…*"),
            ("*Mañana lloverá.* (It will rain.)", "*Con tiempo, viajaría.* (With time, I'd travel.)"),
        ]),
        "pres-subj" => new("Present or subjunctive?", "Present (a fact)", "Subjunctive (a wish)",
        [
            ("*Sé que vienes.* (I know you're coming.)", "*Quiero que vengas.* (I want you to come.)"),
            ("*hablas*", "*hables*"),
            ("*comes*", "*comas*"),
        ]),
        "perf-pret" => new("Present perfect or preterite?", "Present perfect", "Preterite",
        [
            ("*Hoy he comido mucho.* (Today isn't over.)", "*Ayer comí mucho.* (Yesterday is over.)"),
            ("Spain: also for earlier today", "Latin America: often used instead"),
        ]),
        "ra-se" => new("-ra or -se?", "-ra", "-se",
        [
            ("*hablara, tuviera*", "*hablase, tuviese*"),
            ("Common everywhere", "Common in Spain and in writing"),
            ("Practised here", "Also accepted in your answers"),
        ]),
        "cmd-neg" => new("Do or don't (tú)?", "Do", "Don't",
        [
            ("*¡Habla!* (the *él* form)", "*¡No hables!* (subjunctive)"),
            ("*¡Ven!*", "*¡No vengas!*"),
            ("*¡Dímelo!* (pronoun on the end)", "*¡No me lo digas!* (pronoun in front)"),
        ]),
        _ => new("Unreal \"if\": now or in the past?", "Now", "In the past",
        [
            ("*Si tuviera tiempo, iría.*", "*Si hubiera tenido tiempo, habría ido.*"),
            ("If I had time, I'd go.", "If I'd had time, I'd have gone."),
            ("imperfect subjunctive + conditional", "pluperfect subjunctive + conditional perfect"),
        ]),
    };

    private static RulePage TensePage(VerbNode node, VerbBook book)
    {
        var t = node.Tense!;
        var info = node.TenseInfo!;
        var doc = Doc(t);
        var patterns = PatternNodes(node).ToList();
        var blocks = new List<RuleBlock> { new RuleSummary($"*{info.Spanish}*. " + doc.Summary) };

        // Table and recipe: a regular verb (the first pattern's first verb)
        var sample = patterns.FirstOrDefault()?.Members.FirstOrDefault() ?? book.ByInf["hablar"];
        blocks.Add(new RuleTable(sample, t, null));
        var lines = patterns.Select(p => p.Members.FirstOrDefault()).Where(v => v is not null)
            .Select(v => Recipe(v!, t, RecipePerson(v!, t))).OfType<RecipeLine>().ToList();
        if (lines.Count > 0) blocks.Add(new RuleRecipe(lines));

        blocks.Add(new RuleExamples(doc.Examples));
        if (doc.Clues.Length > 0) blocks.Add(new RuleChips("Clue words", doc.Clues));
        if (doc.Compare is not null) blocks.Add(Comparison(doc.Compare));
        foreach (var (title, text) in doc.Tips) blocks.Add(new RuleTip(title, text));

        var how = new List<RuleBlock> { new RulePara(doc.How) };
        if (doc.HowList is not null) how.Add(new RuleList(doc.HowList));
        foreach (var p in patterns.Skip(1))
            if (p.Members.Count > 0) how.Add(new RuleEndings(t, p.Pattern!, p.Members[0]));
        blocks.Add(new RuleSection("How to make it", how));

        var exceptions = node.Children.Where(c => c.TypeId != "reg").ToList();
        if (exceptions.Count > 0)
            blocks.Add(new RuleSection("What's irregular",
            [
                new RulePara(doc.Irregular),
                new RuleList(exceptions.Select(c => $"**{c.Title}**: {Spanishify(c)}").ToList()),
            ]));

        blocks.Add(doc.Check);
        return new RulePage(info.Name, blocks);
    }

    private static IEnumerable<VerbNode> PatternNodes(VerbNode tense) =>
        tense.Children.Where(c => c.TypeId == "reg")
            .SelectMany(c => c.Pattern is not null ? new List<VerbNode> { c } : c.Children)
            .Where(c => c.Pattern is not null && c.Members.Count > 0);

    /// <summary>Subtitle text for a list item: verb forms in italics, group names as they are.</summary>
    private static string Spanishify(VerbNode n) =>
        n.Kind == NodeKind.Type ? n.Subtitle ?? "" : $"*{n.Subtitle}*";

    // ============================================================== types

    private static string TypeSummary(string type, string t) => type switch
    {
        "reg" => t is "fut" or "cond"
            ? "Most verbs follow this pattern exactly, with the same endings for *-ar*, *-er* and *-ir*. Learn the endings once and they work for every regular verb."
            : "Most verbs follow these patterns exactly. Learn the endings once and they work for every regular verb.",
        "stem" => t switch
        {
            "pret" => "Only *-ir* verbs that change in the present change here, only in *él* and *ellos*, and by one small step: *e → i*, *o → u*.",
            "impsubj" => "The *-ir* stem change of the *ellos* preterite (*pidieron*) carries into every person: *pidiera, pidiéramos*.",
            "subj" => "Regular endings, but the stem's vowel changes, as in the present. *-ir* verbs also change in *nosotros* and *vosotros*.",
            "cmd" => "The same vowel changes as the present and the subjunctive: *cierra, no cierres, cierre*.",
            _ => "Regular endings, but the stem's vowel changes when it's stressed: in every form except *nosotros* and *vosotros*.",
        },
        "yo" => t == "pres"
            ? "Regular except the *yo* form."
            : "The special *yo* form of the present carries into every person: *tengo → tenga, tengamos*.",
        "spell" => "The sound is regular; only the spelling changes to keep the sound.",
        "stems" => t switch
        {
            "pret" => "A special stem, and endings without accents: *tuve, tuvo* (compare *hablé, habló*).",
            "impsubj" => "The special preterite stems carry over: *tuvieron → tuviera*.",
            _ => "Regular endings on a shorter stem: *tendré, podré, diré*.",
        },
        "tu" => "Eight verbs have a short *tú* command. Only the \"do\" form is short; \"don't\" uses the subjunctive (*no tengas*).",
        "pp" => "These verbs have their own participle. It's the same in every compound tense.",
        "refl" => "These verbs come with *me, te, se, nos, os, se*. Include it in your answer.",
        _ => "No pattern here: learn each form. These are the most common verbs, so it pays off.",
    };

    private static (string, string)? TypeTip(string type, string t) => (type, t) switch
    {
        ("stem", "pres" or "subj") => ("Boot verbs", "Draw a line around the forms that change and you get a boot: *nosotros* and *vosotros* stay outside it."),
        ("spell", _) => ("Spanish spells by sound", "Before *e*: *c* → *qu*, *g* → *gu*, *z* → *c*. Before *a* or *o*: *g* → *j*. The word still sounds regular."),
        ("refl", "cmd") => ("Pronoun on the end", "\"Do\" commands attach it: *acuérdate, siéntese*. *Nosotros* drops its *-s* (*acordémonos*), *vosotros* its *-d* (*acordaos*)."),
        ("refl", _) => ("Pronoun first", "*me acuerdo*, *me he acordado*: the pronoun comes before the verb, and before *haber*."),
        ("irr", "pres") => ("Two verbs for \"to be\"", "*Soy* for what you are (*soy alta*), *estoy* for how or where you are (*estoy cansada*)."),
        ("irr", "pret") => ("fui = went or was", "*Ser* and *ir* share the preterite: *Fui al cine* (I went), *Fui feliz* (I was happy)."),
        ("irr", "impf") => ("Only three", "*era, iba, veía*: every other verb is regular in the imperfect."),
        ("irr", "subj") => ("DISHES", "**D**ar, **I**r, **S**aber, **H**aber, **E**star, **S**er: *dé, vaya, sepa, haya, esté, sea*."),
        _ => null,
    };

    private static RulePage TypePage(VerbNode node, VerbBook book)
    {
        var t = node.Tense!;
        var blocks = new List<RuleBlock>
        {
            new RuleSummary(TypeSummary(node.TypeId!, t)),
            new RuleList(node.Children.Select(c => $"**{c.Title}**: {Spanishify(c)}").ToList()),
        };
        if (TypeTip(node.TypeId!, t) is { } tip) blocks.Add(new RuleTip(tip.Item1, tip.Item2));
        var group = node.Children.LastOrDefault();
        if (group is not null && GroupCheck(group, book) is { } check) blocks.Add(check);
        return new RulePage($"{node.TenseInfo!.Name}: {node.Title}", blocks);
    }

    // ============================================================== groups

    private static string GroupSummary(string sub, string t) => sub switch
    {
        "e-ie" => "The *e* of the stem becomes *ie*: *quiero, piensas*.",
        "o-ue" => "The *o* of the stem becomes *ue*: *puedo, vuelves*.",
        "e-i" => t switch
        {
            "pret" => "The *e* becomes *i*, in *él* and *ellos* only: *pidió, pidieron*.",
            "subj" => "*-ir* verbs only. The *e* becomes *i* in every person: *pida, pidamos*.",
            "impsubj" => "The *e* becomes *i* in every person: *pidiera*.",
            _ => "*-ir* verbs only. The *e* becomes *i*: *pido, sigues*.",
        },
        "u-ue" => "Only *jugar*: the *u* becomes *ue* (*juego*). It also adds a *u* after the *g* before *e* (*juguemos*).",
        "o-u" => t == "impsubj" ? "The *o* becomes *u* in every person: *durmiera*." : "The *o* becomes *u* in *él* and *ellos*: *durmió, murieron*.",
        "ir-nos" => "*-ir* stem-changers also change in *nosotros* and *vosotros*, by one step: *sintamos, durmamos*.",
        "go" => t == "pres" ? "The *yo* form ends in *-go*: *tengo, hago, pongo*." : "The *-g-* of *tengo* goes into every person: *tenga, tengamos*.",
        "zco" => t == "pres" ? "Vowel + *-cer* or *-cir*: the *yo* form adds a *z*: *conozco*." : "The *-zc-* of *conozco* goes into every person: *conozca*.",
        "g-j" => "*g* becomes *j* before *a* or *o*, to keep the soft sound: *cojo, proteja*.",
        "gu-g" => "The *u* is dropped before *a* or *o*: *sigo, siga*.",
        "uir-y" => "*-uir* verbs add a *y*: *construyo, construyes* (but *construimos*).",
        "accent" => "The *i* or *u* is stressed, so it gets an accent: *envío, continúas*.",
        "car-que" => t == "pret" ? "*yo* form only: *c* becomes *qu* before *é*: *busqué*." : "*c* becomes *qu* before *e*: *busque*.",
        "gar-gue" => t == "pret" ? "*yo* form only: *g* becomes *gu* before *é*: *llegué*." : "*g* becomes *gu* before *e*: *llegue*.",
        "zar-ce" => t == "pret" ? "*yo* form only: *z* becomes *c* before *é*: *empecé*." : "*z* becomes *c* before *e*: *empiece*.",
        "y" => t == "impsubj" ? "The *y* of *leyeron* goes into every person: *leyera*." : "A vowel before the ending turns *i* into *y*: *leyó, leyeron*; the other endings get an accent: *leíste*.",
        "u-stem" => t == "impsubj" ? "From the *u* stem: *tuviera, pudiera*." : "The stem has a *u*: *tuve, estuve, pude, puse, supe*.",
        "i-stem" => t == "impsubj" ? "From the *i* stem: *hiciera, quisiera*." : "The stem has an *i*: *hice, quise, vine* (*hizo* for *él*).",
        "j-stem" => t == "impsubj" ? "From the *j* stem: *dijera, trajera*." : "The stem ends in *j*, and *ellos* takes *-eron*: *dije, dijeron*.",
        "drop-e" => "The *e* of the infinitive drops: *podré, sabré*.",
        "d-stem" => "The *e* or *i* of the infinitive becomes *d*: *tendré, saldré*.",
        "unique-fut" => "*decir → diré*, *hacer → haré*.",
        "tu-irr" => "Eight short *tú* commands: *di, haz, ve, pon, sal, sé, ten, ven*.",
        "pp-irr" => "Participles ending in *-to* or *-cho*: *abierto, hecho, dicho*.",
        "pp-accent" => "Stem ends in a vowel: *-ído* gets an accent: *leído, oído*.",
        _ => "",
    };

    private static (string, string)? GroupTip(string sub, string t) => sub switch
    {
        "e-ie" or "o-ue" or "e-i" or "u-ue" when t is "pres" or "subj" => ("Boot verbs", "The forms that change make a boot shape in the table. *Nosotros* and *vosotros* stay outside it."),
        "go" when t == "pres" => ("yo goes -go", "*tengo, vengo, digo, hago, pongo, salgo, traigo, caigo*."),
        "u-stem" when t == "pret" => ("No accents", "Special-stem preterites have no written accents: *tuve, tuvo* (not *tuvé*)."),
        "d-stem" or "drop-e" or "unique-fut" => ("Two tenses for one", "The future and the conditional share these stems: *tendré, tendría*."),
        "tu-irr" => ("Four plus four", "Four just drop their ending: *ten, pon, ven, sal*. Four are tiny: *di* (decir), *haz* (hacer), *ve* (ir), *sé* (ser)."),
        "pp-irr" => ("-to and -cho", "Most end in *-to* (*abierto, escrito, puesto, visto, vuelto, muerto, roto*); two end in *-cho* (*hecho, dicho*)."),
        _ => null,
    };

    private static RulePage GroupPage(VerbNode node, VerbBook book)
    {
        var t = node.Tense!;
        var sub = node.SubtypeId!;
        var merged = node.Parent?.Kind == NodeKind.Tense; // a type with a single group
        var summary = GroupSummary(sub, t);
        if (merged) summary = summary.Length == 0 ? TypeSummary(node.TypeId!, t) : TypeSummary(node.TypeId!, t) + " " + summary;
        var blocks = new List<RuleBlock> { new RuleSummary(summary) };

        var first = ExampleFor(node);
        Verb? example = first?.Verb;
        if (first is not null && example is not null)
        {
            var highlight = first.Forms.Select(f => f.Person).ToHashSet();
            AddComparison(blocks, example, t, node.Children.Count > 1);
            blocks.Add(new RuleTable(example, t, highlight));
            if (first.Forms.FirstOrDefault(x => VerbSettings.IsActive(x)) is { } rf && Recipe(example, t, rf.Person) is { } line) blocks.Add(new RuleRecipe([line]));
        }
        var tip = GroupTip(sub, t) ?? (merged ? TypeTip(node.TypeId!, t) : null);
        if (tip is { } tp) blocks.Add(new RuleTip(tp.Item1, tp.Item2));
        if (first is not null && example is not null) AddSideBySide(blocks, example, t, first.Forms.Select(f => f.Person).ToHashSet(), book);
        if (node.Members.Count > 0)
            blocks.Add(new RuleSection($"Verbs in this group ({node.Members.Count})", [new RuleVerbs("", node.Members)], Open: node.Members.Count <= 12));
        AddStories(blocks, node, book);
        if (GroupCheck(node, book) is { } check) blocks.Add(check);
        return new RulePage($"{node.TenseInfo!.Name}: {node.Title}", blocks);
    }

    // ============================================================== patterns

    private static string PatternSummary(string t, string pattern)
    {
        var drop = pattern switch
        {
            "ar" => "Drop *-ar*",
            "er" => "Drop *-er*",
            "ir" => "Drop *-ir*",
            _ => "Drop *-er* or *-ir* (they share these endings)",
        };
        return t switch
        {
            "fut" or "cond" => "Keep the whole infinitive and add the ending. Same endings for every verb.",
            "perf" or "plup" or "futperf" or "condperf" or "subjperf" or "plupsubj" =>
                $"*haber* + the participle: the stem + {(pattern == "ar" ? "*-ado*" : "*-ido*")}. Only *haber* changes.",
            "cmd" => $"{drop}. *tú* = the *él* form; *vosotros* = infinitive with *-d*; the rest come from the subjunctive.",
            "subj" => $"{drop} and add the swapped vowel: {(pattern == "ar" ? "*-ar* verbs take *e*" : "*-er* and *-ir* verbs take *a*")}.",
            "impsubj" => "From the *ellos* preterite: drop *-ron*, add *-ra, -ras, -ra, -ramos, -rais, -ran*.",
            _ => $"{drop} and add the ending.",
        };
    }

    private static RulePage PatternPage(VerbNode node, VerbBook book)
    {
        var t = node.Tense!;
        var blocks = new List<RuleBlock> { new RuleSummary(PatternSummary(t, node.Pattern!)) };
        if (node.Members.Count > 0)
        {
            var v = node.Members[0];
            blocks.Add(new RuleEndings(t, node.Pattern!, v));
            if (Recipe(v, t, RecipePerson(v, t)) is { } line) blocks.Add(new RuleRecipe([line]));
            blocks.Add(new RuleSection($"Practised with these verbs ({node.Members.Count})", [new RuleVerbs("", node.Members)]));
            AddStories(blocks, node, book);
            var other = node.Members[Math.Min(1, node.Members.Count - 1)];
            if (FormCheck(other, t, CheckPerson(other, t), book, transfer: true) is { } check) blocks.Add(check);
        }
        return new RulePage($"{node.TenseInfo!.Name}: {node.Title}", blocks);
    }

    // ============================================================== verbs

    private static RulePage VerbPage(VerbNode node, VerbBook book)
    {
        var v = node.Verb!;
        var t = node.Tense!;
        var what = node.SubtypeId is null ? "" : GroupSummary(node.SubtypeId, t);
        var all = Enumerable.Range(0, VerbGrammar.PersonsFor(t).Count).Count(p => v.HasForm(t, p));
        var tracked = node.Forms.Count == all ? "Every form is practised." : "The highlighted forms are practised here; the rest follow the pattern.";
        var blocks = new List<RuleBlock>
        {
            new RuleSummary($"*{v.Inf}*: {v.En}. {what} {tracked}".Replace("  ", " ")),
        };
        AddComparison(blocks, v, t, example: false);
        blocks.Add(new RuleTable(v, t, node.Forms.Select(f => f.Person).ToHashSet()));
        if (node.Forms.FirstOrDefault(x => VerbSettings.IsActive(x)) is { } rf && Recipe(v, t, rf.Person) is { } line) blocks.Add(new RuleRecipe([line]));
        AddSideBySide(blocks, v, t, node.Forms.Select(f => f.Person).ToHashSet(), book);
        AddStories(blocks, node, book);
        var activeForms = node.Forms.Where(x => VerbSettings.IsActive(x)).ToList();
        if (activeForms.Count > 0 && FormCheck(v, t, activeForms[^1].Person, book, transfer: false) is { } check) blocks.Add(check);
        return new RulePage($"{v.Inf}: {node.TenseInfo!.Name}", blocks);
    }

    // ============================================================== compared with regular verbs

    /// <summary>
    /// The verb a group page uses as its example: the most common one whose only differences from the plain
    /// pattern are the ones this group is about (for -go verbs that's hacer, not tener, which also changes
    /// tienes, tiene, tienen). Falls back to the most common verb.
    /// </summary>
    private static VerbNode? ExampleFor(VerbNode group)
    {
        foreach (var leaf in group.Children)
        {
            if (leaf.Verb is not { } v) continue;
            var changed = VerbCompare.ChangedPersons(v, group.Tense!).Where(p => VerbSettings.IsActive(group.Tense!, p)).ToHashSet();
            var mine = leaf.Forms.Where(x => VerbSettings.IsActive(x)).Select(f => f.Person).ToHashSet();
            if (changed.SetEquals(mine)) return leaf;
        }
        return group.Children.FirstOrDefault();
    }

    private static void AddComparison(List<RuleBlock> blocks, Verb v, string t, bool example)
    {
        var lines = VerbCompare.Describe(v, t);
        if (lines.Count == 0) return;
        var title = example
            ? $"Compared with {VerbCompare.RegularName(v, t)} (example: *{v.Inf}*)"
            : $"Compared with {VerbCompare.RegularName(v, t)}";
        blocks.Add(new RuleVsRegular(title, lines));
    }

    private static void AddStories(List<RuleBlock> blocks, VerbNode node, VerbBook book)
    {
        var lines = VerbStories.LinesFor(node, book);
        if (lines.Count > 0) blocks.Add(new RuleSection($"In the stories ({lines.Count})", [new RuleStoryLines(lines)], Open: true));
    }

    private static void AddSideBySide(List<RuleBlock> blocks, Verb v, string t, IReadOnlySet<int> highlight, VerbBook book)
    {
        if (VerbCompare.RegularPartner(v, t, book) is not { } partner) return;
        blocks.Add(new RuleSection("Side by side with a regular verb",
        [
            new RuleTable(v, t, highlight, Caption: v.Inf),
            new RuleTable(partner, t, null, Caption: $"{partner.Inf} (regular)"),
        ]));
    }

    // ============================================================== recipes

    /// <summary>
    /// How one form is built, from things you already know:
    /// hablar → habl + o = hablo · tengo → teng + a = tenga · hablaron → habla + ra = hablara · he + hablado.
    /// "=" when the pieces add up exactly; "→" when the result changes something (shown in orange).
    /// </summary>
    public static RecipeLine? Recipe(Verb v, string t, int p)
    {
        var form = v.Form(t, p);
        if (form is null) return null;
        var info = VerbGrammar.TenseById[t];
        var items = new List<RecipeItem>();
        var bare = v.Reflexive ? v.Inf[..^2] : v.Inf;

        if (v.Reflexive && !info.IsCommand)
        {
            var space = form.IndexOf(' ');
            if (space < 0) return null;
            items.Add(new RecipeChip(form[..space], PartKind.Pronoun));
            items.Add(new RecipeOp("+"));
            items.Add(new RecipeChip(form[(space + 1)..], null));
            items.Add(new RecipeOp("="));
            items.Add(new RecipeForm(v, t, p, form));
            return new RecipeLine(VerbGrammar.Persons[p].Label, items);
        }

        if (info.IsCompound)
        {
            var space = form.LastIndexOf(' ');
            var aux = form[..space];
            var pp = form[(space + 1)..];
            var regPp = v.PatternStem("perf") + (v.Class == "ar" ? "ado" : "ido");
            items.Add(new RecipeChip(aux, PartKind.Helper));
            items.Add(new RecipeOp("+"));
            if (pp == regPp)
            {
                items.Add(new RecipeChip(v.PatternStem("perf"), PartKind.Stem));
                items.Add(new RecipeChip(pp[v.PatternStem("perf").Length..], PartKind.Ending));
            }
            else items.Add(new RecipeChip(pp, PartKind.Change));
            items.Add(new RecipeOp("="));
            items.Add(new RecipeForm(v, t, p, form));
            return new RecipeLine(VerbGrammar.Persons[p].Label, items);
        }

        string start;
        string stem;
        string ending;
        switch (t)
        {
            case "subj":
            {
                var yo = v.Form("pres", 0);
                if (yo is null || !yo.EndsWith('o')) return null;
                start = yo;
                stem = yo[..^1];
                ending = VerbParts.RegularEnding(v, t, p);
                break;
            }
            case "impsubj":
            {
                var ellos = v.Form("pret", 5);
                if (ellos is null || !ellos.EndsWith("ron", StringComparison.Ordinal)) return null;
                start = ellos;
                stem = ellos[..^3];
                ending = new[] { "ra", "ras", "ra", "ramos", "rais", "ran" }[p];
                break;
            }
            case "cmd":
            {
                var person = VerbGrammar.CommandPersons[p];
                var (srcTense, srcPerson, label) = person.Id switch
                {
                    "tu" => ("pres", 2, "él form"),
                    "tu-neg" => ("subj", 1, "subjunctive"),
                    "usted" => ("subj", 2, "subjunctive"),
                    "nos" => ("subj", 3, "subjunctive"),
                    "vos-neg" => ("subj", 4, "subjunctive"),
                    "ustedes" => ("subj", 5, "subjunctive"),
                    _ => ("inf", 0, "infinitive"),
                };
                var src = srcTense == "inf" ? bare : v.Form(srcTense, srcPerson);
                if (src is null || v.Reflexive) return null;
                items.Add(new RecipeChip($"{label}: {src}", null));
                items.Add(new RecipeOp(person.Id == "vos" ? "→" : src == form ? "=" : "→"));
                if (person.Negative) items.Add(new RecipeChip("no", null));
                items.Add(new RecipeForm(v, t, p, form));
                return new RecipeLine(person.Label, items);
            }
            default:
                start = bare;
                stem = v.PatternStem(t);
                ending = VerbParts.RegularEnding(v, t, p);
                break;
        }

        if (t is not ("fut" or "cond"))
        {
            items.Add(new RecipeChip(start, null));
            items.Add(new RecipeOp("→"));
        }
        items.Add(new RecipeChip(stem, PartKind.Stem));
        items.Add(new RecipeOp("+"));
        items.Add(new RecipeChip(ending, PartKind.Ending));
        items.Add(new RecipeOp(stem + ending == form ? "=" : "→"));
        items.Add(new RecipeForm(v, t, p, form));
        return new RecipeLine(VerbGrammar.PersonsFor(t)[p].Label, items);
    }

    /// <summary>A person that shows the pattern well: yo, or the first person the verb has.</summary>
    private static int RecipePerson(Verb v, string t)
    {
        for (var p = 0; p < VerbGrammar.PersonsFor(t).Count; p++)
            if (v.HasForm(t, p)) return p;
        return 0;
    }

    // ============================================================== checks

    /// <summary>Check for a group: a different verb from the one in the table when there is one.</summary>
    private static RuleCheck? GroupCheck(VerbNode group, VerbBook book)
    {
        if (group.Pattern is not null)
        {
            var other = group.Members[Math.Min(1, group.Members.Count - 1)];
            return FormCheck(other, group.Tense!, CheckPerson(other, group.Tense!), book, transfer: true);
        }
        var leaf = group.Children.Count > 1 ? group.Children[1] : group.Children.FirstOrDefault();
        if (leaf?.Verb is null || leaf.Forms.Count == 0) return null;
        var active = leaf.Forms.Where(x => VerbSettings.IsActive(x)).ToList();
        if (active.Count == 0) return null;
        return FormCheck(leaf.Verb, group.Tense!, active[^1].Person, book, transfer: group.Children.Count > 1);
    }

    private static int CheckPerson(Verb v, string t)
    {
        var preferred = t == "cmd" ? 0 : 3; // nosotros shows the -amos / -emos / -imos difference
        return v.HasForm(t, preferred) ? preferred : RecipePerson(v, t);
    }

    /// <summary>"Which is the yo form of querer in the present?" with 3 look-alike wrong answers.</summary>
    private static RuleCheck? FormCheck(Verb v, string t, int p, VerbBook book, bool transfer)
    {
        var answers = v.Answers(t, p);
        if (answers.Count == 0) return null;
        var right = answers[0];
        var wrong = VerbQuiz.Options(v, t, p, answers, book).Where(o => o != right).Take(3).ToList();
        if (wrong.Count < 2) return null;
        var person = VerbGrammar.PersonsFor(t)[p];
        var tense = VerbGrammar.TenseById[t].Name.ToLowerInvariant();
        var lead = transfer ? "Try another verb. " : "";
        var question = person.Negative
            ? $"{lead}*{v.Inf}*, {tense}, *{person.Label.Replace(", negative", "")}*: *no* ___"
            : $"{lead}*{v.Inf}*, {tense}: the *{person.Label}* form?";
        return new RuleCheck(question, [right, .. wrong], 0, $"It's *{(person.Negative ? "no " : "")}{right}*.", SpanishOptions: true);
    }
}
