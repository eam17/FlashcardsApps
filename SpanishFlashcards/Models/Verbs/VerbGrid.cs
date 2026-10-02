namespace SpanishFlashcards.Models.Verbs;

/// <summary>One row of the grid: a regular pattern (-ar, -er, -ir) or a verb with irregular forms.</summary>
public sealed record GridRow(string Id, string Title, string Subtitle, string? Class, Verb? Verb)
{
    public bool IsPattern => Class is not null;
}

/// <summary>One cell: how well you know that row in that tense.</summary>
public sealed record GridCell(GridRow Row, string Tense, IReadOnlyList<FormRef> Forms, VerbNode? Node, bool RegularHere)
{
    /// <summary>The verb shown when the cell is tapped.</summary>
    public Verb? SampleVerb => Row.Verb ?? Node?.Members.FirstOrDefault();
}

public enum GridLevel { NotStarted, Weak, Learning, Good, Strong, Regular }

/// <summary>
/// The colour grid: tenses as columns; rows are the three regular patterns (tracked by their endings)
/// and every verb that's irregular anywhere (tracked form by form). A verb's cell is "regular here" when it
/// follows the pattern in that tense.
/// </summary>
public sealed class VerbGrid
{
    public IReadOnlyList<GridRow> Patterns { get; }
    public IReadOnlyList<GridRow> Verbs { get; }
    private readonly Dictionary<(string Row, string Tense), GridCell> cells = new();

    public VerbGrid(VerbNode tree, VerbBook book)
    {
        // Where each pattern and each verb lives in the tree, per tense (the first place it appears).
        var patternNodes = new Dictionary<(string Tense, string Pattern), VerbNode>();
        var verbNodes = new Dictionary<(string Tense, string Inf), VerbNode>();
        void Walk(VerbNode n)
        {
            if (n.Pattern is not null && n.Tense is not null) patternNodes.TryAdd((n.Tense, n.Pattern), n);
            if (n.Kind == NodeKind.Verb && n.Verb is not null) verbNodes.TryAdd((n.Tense!, n.Verb.Inf), n);
            foreach (var c in n.Children) Walk(c);
        }
        Walk(tree);

        var patterns = new List<GridRow>
        {
            new("~ar", "-ar verbs", "regular: hablar", "ar", null),
            new("~er", "-er verbs", "regular: comer", "er", null),
            new("~ir", "-ir verbs", "regular: vivir", "ir", null),
        };
        foreach (var row in patterns)
        {
            foreach (var t in VerbGrammar.GridTenses)
            {
                var pat = t.Id is "fut" or "cond" ? "inf"
                    : t.Id is "pres" or "cmd" ? row.Class!
                    : row.Class == "ar" ? "ar" : "er-ir";
                patternNodes.TryGetValue((t.Id, pat), out var node);
                cells[(row.Id, t.Id)] = new GridCell(row, t.Id, node?.Forms ?? [], node, RegularHere: false);
            }
        }
        Patterns = patterns;

        var verbs = new List<GridRow>();
        foreach (var v in book.Verbs)
        {
            var row = new GridRow(v.Inf, v.Inf, v.En, null, v);
            var any = false;
            foreach (var t in VerbGrammar.GridTenses)
            {
                var forms = new List<FormRef>();
                for (var p = 0; p < VerbGrammar.PersonsFor(t.Id).Count; p++)
                    if (v.HasForm(t.Id, p) && v.Why(t.Id, p) is not null) forms.Add(new FormRef(v.Inf, t.Id, p));
                any |= forms.Count > 0;
                verbNodes.TryGetValue((t.Id, v.Inf), out var node);
                var regularHere = forms.Count == 0 && v.Has(t.Id);
                if (regularHere) patternNodes.TryGetValue((t.Id, v.PatternIn(t.Id)), out node);
                cells[(row.Id, t.Id)] = new GridCell(row, t.Id, forms, node, regularHere);
            }
            if (any) verbs.Add(row);
        }
        Verbs = verbs;
    }

    public GridCell Cell(GridRow row, string tense) => cells[(row.Id, tense)];

    /// <summary>Average strength of the cell's forms (not started = 0), and whether any were started.</summary>
    public static (double Score, bool Started) Score(GridCell cell, IReadOnlyDictionary<string, VerbSkill> skills)
    {
        var forms = cell.Forms.Where(f => VerbSettings.IsActive(f)).ToList();
        if (forms.Count == 0) return (0, false);
        double sum = 0;
        var started = false;
        foreach (var f in forms)
        {
            if (!skills.TryGetValue(f.Key, out var s) || !VerbSrs.IsSeen(s)) continue;
            started = true;
            sum += s.Strength;
        }
        return (sum / forms.Count, started);
    }

    public static GridLevel Level(GridCell cell, IReadOnlyDictionary<string, VerbSkill> skills)
    {
        if (cell.RegularHere) return GridLevel.Regular;
        var (score, started) = Score(cell, skills);
        if (!started) return GridLevel.NotStarted;
        return score switch
        {
            < 0.25 => GridLevel.Weak,
            < 0.5 => GridLevel.Learning,
            < VerbSrs.Strong => GridLevel.Good,
            _ => GridLevel.Strong,
        };
    }

    /// <summary>A row's average over the tenses where it has tracked forms you've started (for "weakest first").</summary>
    public double RowScore(GridRow row, IReadOnlyDictionary<string, VerbSkill> skills)
    {
        var scores = VerbGrammar.GridTenses.Select(t => Score(Cell(row, t.Id), skills)).Where(x => x.Started).Select(x => x.Score).ToList();
        return scores.Count == 0 ? double.MaxValue : scores.Average();
    }

    public static string Label(GridLevel l) => l switch
    {
        GridLevel.NotStarted => "not started",
        GridLevel.Weak => "weak",
        GridLevel.Learning => "learning",
        GridLevel.Good => "good",
        GridLevel.Strong => "strong",
        _ => "regular here",
    };

    /// <summary>Short column headings.</summary>
    public static string Short(string tense) => tense switch
    {
        "pres" => "Pres",
        "pret" => "Pret",
        "impf" => "Impf",
        "fut" => "Fut",
        "cond" => "Cond",
        "perf" => "Perf",
        "subj" => "Subj",
        "cmd" => "Cmd",
        "impsubj" => "Imp S",
        "plup" => "Plup",
        "futperf" => "F Pf",
        "condperf" => "C Pf",
        "subjperf" => "Pf S",
        _ => "Pl S",
    };
}
