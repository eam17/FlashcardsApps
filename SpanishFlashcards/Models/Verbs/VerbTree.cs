namespace SpanishFlashcards.Models.Verbs;

public enum NodeKind { Root, Tense, Type, Subtype, Verb }

/// <summary>
/// One item in the Verbs tree: tense → type → subtype → verbs.
/// Every node owns a set of tracked forms (its own plus everything underneath); its score rolls up from them.
/// </summary>
public sealed class VerbNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; set; }
    public required NodeKind Kind { get; init; }
    public string? Tense { get; init; }
    public string? TypeId { get; set; }
    public string? SubtypeId { get; set; }

    /// <summary>Regular pattern ("ar", "er-ir"…) when this node teaches a pattern rather than verbs.</summary>
    public string? Pattern { get; set; }

    /// <summary>Verb nodes: the verb.</summary>
    public Verb? Verb { get; init; }

    public VerbNode? Parent { get; set; }
    public List<VerbNode> Children { get; } = new();

    /// <summary>Forms tracked directly by this node (leaves).</summary>
    public List<FormRef> Forms { get; } = new();

    /// <summary>This node's forms plus all forms underneath, in tree order, without duplicates.</summary>
    public IReadOnlyList<FormRef> AllForms { get; private set; } = [];

    /// <summary>Verbs listed under this node (subtype members, or pattern sample verbs).</summary>
    public List<Verb> Members { get; } = new();

    public TenseInfo? TenseInfo => Tense is null ? null : VerbGrammar.TenseById[Tense];

    public IEnumerable<VerbNode> Ancestors()
    {
        for (var n = Parent; n is not null; n = n.Parent) yield return n;
    }

    internal void Seal()
    {
        foreach (var c in Children) c.Seal();
        var seen = new HashSet<string>();
        var all = new List<FormRef>();
        foreach (var f in Forms.Concat(Children.SelectMany(c => c.AllForms)))
            if (seen.Add(f.Key)) all.Add(f);
        AllForms = all;
    }
}

public sealed record NodeStats(double Score, int Total, int Started, int Due, int Strong)
{
    public int Percent => (int)Math.Round(Score * 100);
}

public static class VerbTree
{
    private static readonly string[] TypeOrder = ["reg", "stem", "yo", "spell", "stems", "tu", "pp", "irr", "refl"];

    private static readonly string[] SubtypeOrder =
    [
        "ar", "er", "ir", "er-ir", "inf",
        "e-ie", "o-ue", "e-i", "u-ue", "o-u", "ir-nos",
        "go", "zco",
        "g-j", "gu-g", "uir-y", "accent", "car-que", "gar-gue", "zar-ce", "y",
        "u-stem", "i-stem", "j-stem", "drop-e", "d-stem", "unique-fut",
        "tu-irr", "pp-irr", "pp-accent", "irr", "refl",
    ];

    private static readonly string[] Patterns = ["ar", "er", "ir", "er-ir", "inf"];

    public static string TypeOf(string why) => why switch
    {
        "e-ie" or "o-ue" or "e-i" or "u-ue" or "o-u" or "ir-nos" => "stem",
        "go" or "zco" => "yo",
        "g-j" or "gu-g" or "uir-y" or "accent" or "car-que" or "gar-gue" or "zar-ce" or "y" => "spell",
        "u-stem" or "i-stem" or "j-stem" or "drop-e" or "d-stem" or "unique-fut" => "stems",
        "tu-irr" => "tu",
        "pp-irr" or "pp-accent" => "pp",
        "refl" => "refl",
        _ => "irr",
    };

    public static string TypeTitle(string type, string tense) => type switch
    {
        "reg" => tense == "near" ? "Every verb" : "Regular verbs",
        "stem" => tense is "pret" or "impsubj" ? "Stem-changing -ir verbs" : "Stem-changing verbs",
        "yo" => tense == "pres" ? "Irregular yo form" : "From the yo form",
        "spell" => "Spelling changes",
        "stems" => "Irregular stems",
        "tu" => "Short tú commands",
        "pp" => "Irregular participles",
        "refl" => "Reflexive verbs",
        _ => "Unique verbs",
    };

    public static string SubtypeTitle(string sub) => sub switch
    {
        "ar" => "-ar verbs",
        "er" => "-er verbs",
        "ir" => "-ir verbs",
        "er-ir" => "-er and -ir verbs",
        "inf" => "All verbs",
        "e-ie" => "e → ie",
        "o-ue" => "o → ue",
        "e-i" => "e → i",
        "u-ue" => "u → ue",
        "o-u" => "o → u",
        "ir-nos" => "-ir verbs: nosotros and vosotros",
        "go" => "-go verbs",
        "zco" => "-zco verbs",
        "g-j" => "g → j",
        "gu-g" => "gu → g",
        "uir-y" => "-uir verbs add y",
        "accent" => "Accent on í or ú",
        "car-que" => "c → qu",
        "gar-gue" => "g → gu",
        "zar-ce" => "z → c",
        "y" => "i → y",
        "u-stem" => "u-stem",
        "i-stem" => "i-stem",
        "j-stem" => "j-stem",
        "drop-e" => "Drop the e",
        "d-stem" => "Add a d",
        "unique-fut" => "decir and hacer",
        "tu-irr" => "Short tú commands",
        "pp-irr" => "-to and -cho",
        "pp-accent" => "-ído with an accent",
        "refl" => "Reflexive verbs",
        _ => "Unique verbs",
    };

    public static VerbNode Build(VerbBook book)
    {
        var root = new VerbNode { Id = "root", Title = "Verbs", Kind = NodeKind.Root };

        foreach (var tense in VerbGrammar.Tenses)
        {
            var t = tense.Id;
            var tNode = new VerbNode
            {
                Id = $"t:{t}", Title = tense.Name, Subtitle = tense.English, Kind = NodeKind.Tense, Tense = t,
                Parent = root,
            };
            var persons = VerbGrammar.PersonsFor(t).Count;

            // (type, subtype) → verb → forms
            var groups = new Dictionary<(string Type, string Sub), Dictionary<Verb, List<FormRef>>>();
            var patterns = new HashSet<string>();

            foreach (var v in book.Verbs)
            {
                if (!v.Has(t)) continue;
                for (var p = 0; p < persons; p++)
                {
                    if (!v.HasForm(t, p)) continue;
                    var why = v.Why(t, p);
                    if (why is null)
                    {
                        patterns.Add(v.PatternIn(t));
                        continue;
                    }
                    var key = (TypeOf(why), why);
                    if (!groups.TryGetValue(key, out var byVerb)) groups[key] = byVerb = new();
                    if (!byVerb.TryGetValue(v, out var list)) byVerb[v] = list = new();
                    list.Add(new FormRef(v.Inf, t, p));
                }
            }

            // Regular patterns
            var subsByType = new Dictionary<string, List<VerbNode>>();
            foreach (var pat in Patterns.Where(patterns.Contains))
            {
                var samples = book.PatternSamples(t, pat);
                if (samples.Count == 0) continue;
                var node = new VerbNode
                {
                    Id = $"t:{t}/reg/{pat}", Title = SubtypeTitle(pat), Kind = NodeKind.Subtype, Tense = t,
                    TypeId = "reg", SubtypeId = pat, Pattern = pat,
                };
                for (var p = 0; p < persons; p++) node.Forms.Add(new FormRef(FormRef.PatternKey(pat), t, p));
                node.Members.AddRange(samples);
                node.Subtitle = Examples(samples[0], t, Enumerable.Range(0, Math.Min(3, persons)));
                Add(subsByType, "reg", node);
            }

            // Groups of verbs that share a change
            foreach (var ((type, sub), byVerb) in groups.OrderBy(g => Array.IndexOf(SubtypeOrder, g.Key.Sub)))
            {
                var node = new VerbNode
                {
                    Id = $"t:{t}/{type}/{sub}", Title = SubtypeTitle(sub), Kind = NodeKind.Subtype, Tense = t,
                    TypeId = type, SubtypeId = sub,
                };
                foreach (var (v, forms) in byVerb.OrderBy(x => x.Key.Rank))
                {
                    var vNode = new VerbNode
                    {
                        Id = $"{node.Id}/{v.Inf}", Title = v.Inf, Subtitle = v.En, Kind = NodeKind.Verb, Tense = t,
                        TypeId = type, SubtypeId = sub, Verb = v, Parent = node,
                    };
                    vNode.Forms.AddRange(forms);
                    node.Children.Add(vNode);
                    node.Members.Add(v);
                }
                node.Subtitle = string.Join(", ", byVerb.OrderBy(x => x.Key.Rank).Take(3)
                    .Select(x => x.Key.Form(t, x.Value[0].Person)));
                Add(subsByType, type, node);
            }

            foreach (var type in TypeOrder.Where(subsByType.ContainsKey))
            {
                var subs = subsByType[type];
                VerbNode typeNode;
                if (subs.Count == 1)
                {
                    // Only one group in this type: the type node is that group (no extra level).
                    var only = subs[0];
                    typeNode = new VerbNode
                    {
                        Id = $"t:{t}/{type}", Title = TypeTitle(type, t), Kind = NodeKind.Subtype, Tense = t,
                        TypeId = type, SubtypeId = only.SubtypeId, Pattern = only.Pattern, Subtitle = only.Subtitle,
                    };
                    typeNode.Forms.AddRange(only.Forms);
                    typeNode.Members.AddRange(only.Members);
                    foreach (var c in only.Children)
                    {
                        c.Parent = typeNode;
                        typeNode.Children.Add(c);
                    }
                }
                else
                {
                    typeNode = new VerbNode
                    {
                        Id = $"t:{t}/{type}", Title = TypeTitle(type, t), Kind = NodeKind.Type, Tense = t, TypeId = type,
                        Subtitle = string.Join(" · ", subs.Select(s => s.Title)),
                    };
                    foreach (var s in subs)
                    {
                        s.Parent = typeNode;
                        typeNode.Children.Add(s);
                    }
                }
                typeNode.Parent = tNode;
                tNode.Children.Add(typeNode);
            }
            root.Children.Add(tNode);
        }

        root.Seal();
        return root;
    }

    private static void Add(Dictionary<string, List<VerbNode>> d, string key, VerbNode n)
    {
        if (!d.TryGetValue(key, out var list)) d[key] = list = new();
        list.Add(n);
    }

    private static string Examples(Verb v, string tense, IEnumerable<int> persons) =>
        string.Join(", ", persons.Select(p => v.Form(tense, p)).Where(f => f is not null));

    public static VerbNode? Find(VerbNode root, string id)
    {
        if (root.Id == id) return root;
        foreach (var c in root.Children)
        {
            if (!id.StartsWith(c.Id, StringComparison.Ordinal) && c.Kind != NodeKind.Root) continue;
            var hit = Find(c, id);
            if (hit is not null) return hit;
        }
        return null;
    }

    public static NodeStats Stats(VerbNode node, IReadOnlyDictionary<string, VerbSkill> skills, DateTime nowUtc)
    {
        double sum = 0;
        int started = 0, due = 0, strong = 0, total = 0;
        foreach (var f in node.AllForms)
        {
            if (!VerbSettings.IsActive(f)) continue;
            total++;
            if (!skills.TryGetValue(f.Key, out var s) || !VerbSrs.IsSeen(s)) continue;
            started++;
            sum += s.Strength;
            if (s.Due <= nowUtc) due++;
            if (s.Strength >= VerbSrs.Strong) strong++;
        }
        return new NodeStats(total == 0 ? 0 : sum / total, total, started, due, strong);
    }

    /// <summary>Mistakes per person under this node (a missing accent counts as half).</summary>
    public static List<(string Label, double Mistakes)> MistakesByPerson(VerbNode node, IReadOnlyDictionary<string, VerbSkill> skills)
    {
        var totals = VerbGrammar.PersonGroups.ToDictionary(g => g.Group, _ => 0.0);
        foreach (var f in node.AllForms)
        {
            if (!skills.TryGetValue(f.Key, out var s)) continue;
            totals[f.PersonInfo.Group] += s.MistakeScore;
        }
        return VerbSettings.PersonGroups.Select(g => (g.Label, totals[g.Group])).ToList();
    }
}
