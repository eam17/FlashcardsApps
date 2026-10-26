namespace SpanishFlashcards.Models.Verbs;

/// <summary>What a unit asks of you: make and read its forms, read them first (make them later), or only read them.</summary>
public enum UnitGoal { Use, ReadFirst, ReadOnly }

/// <summary>
/// The kind of thing a unit teaches: a regular pattern (endings), a group of verbs that change the same way, verbs to
/// memorise whole, a choice between two tenses, or set phrases.
/// </summary>
public enum UnitKind { Pattern, Group, Memorise, Choose, Phrases }

/// <summary>
/// Groups of a tense in the Verbs tree that a unit takes its forms from. <see cref="Sub"/> is a group id ("ar",
/// "o-ue", "u-stem"…), "*" for every group of the tense, or "*x" for every group except the regular patterns.
/// <see cref="Persons"/> (person ids such as "usted") limits it to those persons; empty means all.
/// </summary>
public sealed record UnitSel(string Tense, string Sub, IReadOnlyList<string> Persons);

/// <summary>Whole verbs a unit teaches in these tenses (every person, the regular ones included).</summary>
public sealed record UnitWhole(IReadOnlyList<string> Verbs, IReadOnlyList<string> Tenses);

/// <summary>One unit on the list (see VerbUnits.Defs).</summary>
public sealed record UnitDef(
    string Id,
    int Tier,
    string Title,
    // The idea in a few lines (rules markup: *Spanish*, **bold**). Shown first on the idea screen.
    string Blurb,
    UnitGoal Goal,
    UnitKind Kind,
    IReadOnlyList<UnitSel> Sel,
    IReadOnlyList<UnitWhole> Whole,
    string? Tip = null,
    // Choose units: the tenses to choose between.
    IReadOnlyList<string>? Choose = null,
    // Choose and phrase units: the idea as a few lines (rules markup).
    IReadOnlyList<string>? Idea = null,
    // Vosotros forms to recognise when vosotros is off in Settings.
    bool VosotrosRead = false);

/// <summary>A verb in one of the example stories that belongs to a unit.</summary>
public sealed record StorySpot(Story Story, StorySentence Sentence, int Token, Verb Verb, string Tense, int Person);

/// <summary>
/// One unit, resolved against the verb data: the forms it covers, its verbs, and the sentences and story lines that
/// use them. Built once (see VerbKit).
/// </summary>
public sealed class VerbUnit
{
    public required UnitDef Def { get; init; }

    /// <summary>1-based position in the list.</summary>
    public required int Number { get; init; }

    public string Id => Def.Id;
    public string Title => Def.Title;
    public int Tier => Def.Tier;
    public UnitGoal Goal => Def.Goal;
    public UnitKind Kind => Def.Kind;

    /// <summary>
    /// The forms to make and read. A regular pattern form ("~ar|pres|yo") has no verb of its own: a different regular
    /// verb is used each time. A whole verb's regular forms carry the verb (tener: tenemos).
    /// </summary>
    public List<PracticeItem> Items { get; } = new();

    /// <summary>The tree groups it draws on (for the idea screen and the unit page).</summary>
    public List<VerbNode> Nodes { get; } = new();

    /// <summary>Its verbs, most common first (for a pattern: regular verbs that follow it).</summary>
    public List<Verb> Verbs { get; } = new();

    public HashSet<string> Tenses { get; } = new();

    /// <summary>Story verbs that belong to this unit.</summary>
    public List<StorySpot> Stories { get; } = new();

    /// <summary>Sentences with a gap for a verb of this unit (new sentences and clue sentences).</summary>
    public List<VerbSentence> Sentences { get; } = new();

    /// <summary>Items that count with the vosotros setting.</summary>
    public IEnumerable<PracticeItem> Active => Items.Where(i => VerbSettings.IsActive(i.Form));

    /// <summary>Reviews by verb (ser in the preterite) rather than for the unit as a whole: units of verbs to memorise.</summary>
    public bool ReviewByVerb => Kind == UnitKind.Memorise;

    public bool HasForms => Items.Count > 0 && Kind is UnitKind.Pattern or UnitKind.Group or UnitKind.Memorise;

    /// <summary>Some form can be built from a stem and an ending (one-word tenses, not reflexive).</summary>
    public bool HasBuild { get; set; }

    /// <summary>Some verb has English meanings (for meaning questions and English to Spanish).</summary>
    public bool HasMeaning { get; set; }

    /// <summary>There's a verb you haven't met yet to try the rule on.</summary>
    public bool HasPredict => Kind is UnitKind.Pattern or UnitKind.Group && Verbs.Count >= 2;

    public bool HasSentences => Stories.Count > 0 || Sentences.Count > 0;

    /// <summary>Some form can be decoded (Decode leaves out "going to", which is the same for every verb).</summary>
    public bool HasDecode => HasForms && Items.Any(i => i.Form.Tense != "near");

    /// <summary>There are sentences to read its forms in (with English for the meaning). Set by VerbKit.</summary>
    public bool HasReadSentences { get; set; }

    /// <summary>"Unit 7 · Now, plans, what happened"</summary>
    public string Kicker => $"Unit {Number} · {VerbUnits.TierTitle(Tier)}";
}

/// <summary>
/// The units in learning order, and how each one is resolved against the verb tree. Order is set up for Latin
/// American Spanish (travel in Peru): ser and estar and the past early, ir a before the future, usted as much as tú,
/// polite requests early as phrases, rare spelling changes late.
/// </summary>
public static partial class VerbUnits
{
    public static string TierTitle(int tier) => tier switch
    {
        1 => "Now, plans, what happened",
        2 => "Experiences, requests, wishes, later",
        3 => "Would, if, the past of the past",
        _ => "Read only",
    };

    /// <summary>
    /// Resolves the units against the tree. A form belongs to the first unit that takes it (whole verbs claim their
    /// forms before groups do), so nothing is taught twice.
    /// </summary>
    public static List<VerbUnit> Build(VerbBook book, VerbNode tree)
    {
        var claimed = new HashSet<string>();
        var units = new List<VerbUnit>();
        foreach (var def in Defs)
        {
            var u = new VerbUnit { Def = def, Number = units.Count + 1 };
            var verbs = new List<Verb>();

            foreach (var whole in def.Whole)
            {
                foreach (var inf in whole.Verbs)
                {
                    if (!book.ByInf.TryGetValue(inf, out var v)) continue;
                    foreach (var t in whole.Tenses)
                    {
                        if (!v.Has(t)) continue;
                        var persons = VerbGrammar.PersonsFor(t);
                        for (var p = 0; p < persons.Count; p++)
                        {
                            if (!v.HasForm(t, p)) continue;
                            if (!claimed.Add($"{v.Inf}|{t}|{persons[p].Id}")) continue;
                            u.Items.Add(v.Why(t, p) is null
                                ? new PracticeItem(new FormRef(FormRef.PatternKey(v.PatternIn(t)), t, p), v.Inf)
                                : new PracticeItem(new FormRef(v.Inf, t, p)));
                            u.Tenses.Add(t);
                            if (!verbs.Contains(v)) verbs.Add(v);
                        }
                    }
                }
            }

            foreach (var sel in def.Sel)
            {
                var tenseNode = tree.Children.FirstOrDefault(n => n.Tense == sel.Tense);
                if (tenseNode is null) continue;
                foreach (var node in Groups(tenseNode).Where(n => Matches(n, sel.Sub)))
                {
                    var added = false;
                    if (node.Pattern is not null)
                    {
                        var persons = VerbGrammar.PersonsFor(sel.Tense);
                        for (var p = 0; p < persons.Count; p++)
                        {
                            if (sel.Persons.Count > 0 && !sel.Persons.Contains(persons[p].Id)) continue;
                            var form = new FormRef(FormRef.PatternKey(node.Pattern), sel.Tense, p);
                            if (!claimed.Add(form.Key)) continue;
                            u.Items.Add(new PracticeItem(form));
                            added = true;
                        }
                        if (added)
                            foreach (var v in node.Members.Take(40))
                                if (!verbs.Contains(v)) verbs.Add(v);
                    }
                    else
                    {
                        foreach (var f in node.AllForms)
                        {
                            if (sel.Persons.Count > 0 && !sel.Persons.Contains(f.PersonId)) continue;
                            if (!claimed.Add(f.Key)) continue;
                            u.Items.Add(new PracticeItem(f));
                            added = true;
                            if (book.ByInf.TryGetValue(f.VerbKey, out var v) && !verbs.Contains(v)) verbs.Add(v);
                        }
                    }
                    if (!added) continue;
                    u.Nodes.Add(node);
                    u.Tenses.Add(sel.Tense);
                }
            }

            if (def.Choose is { } choose)
                foreach (var t in choose) u.Tenses.Add(t);

            u.Verbs.AddRange(verbs.OrderBy(v => v.Rank));
            u.HasBuild = u.Items.Any(i => !VerbGrammar.TenseById[i.Form.Tense].IsCompound && i.Form.Tense is not "near" and not "cmd")
                         && u.Verbs.Any(v => !v.Reflexive);
            u.HasMeaning = u.Verbs.Any(VerbEnglish.Has);
            units.Add(u);
        }
        return units;
    }

    /// <summary>Every group under a tense (the tree has one or two levels: type → group).</summary>
    private static IEnumerable<VerbNode> Groups(VerbNode tense)
    {
        foreach (var c in tense.Children)
        {
            if (c.Kind == NodeKind.Subtype) yield return c;
            else if (c.Kind == NodeKind.Type)
                foreach (var sub in c.Children.Where(x => x.Kind == NodeKind.Subtype))
                    yield return sub;
        }
    }

    private static bool Matches(VerbNode n, string sub) => sub switch
    {
        "*" => true,
        "*x" => n.TypeId != "reg",
        _ => n.SubtypeId == sub,
    };

    private static UnitSel S(string tense, string sub, params string[] persons) => new(tense, sub, persons);

    private static UnitWhole W(string[] verbs, params string[] tenses) => new(verbs, tenses);

    // ============================================================== the idea screen

    /// <summary>
    /// The idea in short, for the idea screen and the top of the unit page: the unit's summary, then for each of its
    /// groups the table or endings (and for a single group, how a form is built and its tip), the whole verbs' tables,
    /// or the lines and phrases of a choose or phrase unit.
    /// </summary>
    public static List<RuleBlock> Idea(VerbUnit u, VerbBook book)
    {
        var blocks = new List<RuleBlock> { new RuleSummary(u.Def.Blurb) };
        switch (u.Kind)
        {
            case UnitKind.Choose:
                if (u.Def.Idea is { } lines) blocks.Add(new RuleList(lines));
                var ex = u.Sentences.Take(4).Select(s => (Fill(s, book), s.En)).ToList();
                if (ex.Count > 0) blocks.Add(new RuleExamples(ex));
                break;

            case UnitKind.Phrases:
                if (u.Def.Idea is { } plines) blocks.Add(new RuleList(plines));
                var phrases = u.Sentences.Take(6).Select(s => (Fill(s, book), s.En)).ToList();
                if (phrases.Count > 0) blocks.Add(new RuleExamples(phrases));
                break;

            default:
                var several = u.Nodes.Count + u.Def.Whole.Sum(w => w.Verbs.Count * w.Tenses.Count) > 1;
                var multiTense = u.Tenses.Count > 1;
                foreach (var whole in u.Def.Whole)
                {
                    foreach (var inf in whole.Verbs)
                    {
                        if (!book.ByInf.TryGetValue(inf, out var v)) continue;
                        foreach (var t in whole.Tenses.Where(v.Has))
                            blocks.Add(new RuleTable(v, t, VerbCompare.ChangedPersons(v, t),
                                $"{v.Inf} · {VerbGrammar.TenseById[t].Name.ToLowerInvariant()} ({v.En})"));
                    }
                }
                foreach (var node in u.Nodes.Take(5))
                {
                    var page = VerbRules.For(node, book);
                    if (several) blocks.Add(new RuleHeading(NodeTitle(node, multiTense)));
                    var summary = page.Blocks.OfType<RuleSummary>().FirstOrDefault();
                    if (several && summary is not null) blocks.Add(new RulePara(summary.Text));
                    var table = page.Blocks.FirstOrDefault(b => b is RuleEndings or RuleTable);
                    if (table is not null) blocks.Add(table);
                    if (!several)
                    {
                        if (summary is not null) blocks.Add(new RulePara(summary.Text));
                        if (page.Blocks.OfType<RuleRecipe>().FirstOrDefault() is { } recipe) blocks.Add(recipe);
                        if (page.Blocks.OfType<RuleTip>().FirstOrDefault() is { } tip && u.Def.Tip is null) blocks.Add(tip);
                    }
                }
                break;
        }
        if (u.Def.Tip is { } t2) blocks.Add(new RuleTip("Tip", t2));
        return blocks;
    }

    /// <summary>"-ar verbs", or "Preterite: Spelling changes: c → qu" when the unit spans several tenses.</summary>
    public static string NodeTitle(VerbNode node, bool withTense)
    {
        var title = node.Parent is { Kind: NodeKind.Type } type ? $"{type.Title}: {node.Title}" : node.Title;
        return withTense && node.Tense is not null ? $"{VerbGrammar.TenseById[node.Tense].Name}: {title}" : title;
    }

    /// <summary>A gap sentence with its verb in place and marked: "Ayer [comí] paella".</summary>
    public static string Fill(VerbSentence s, VerbBook book)
    {
        var (before, after) = VerbSentences.Split(s.Es);
        return before + "[" + VerbSentences.Shown(VerbSentences.Answer(s, book), VerbSentences.AtStart(before)) + "]" + after;
    }
}
