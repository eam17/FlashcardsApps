namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// "Compared with regular verbs": what a verb has in common with the plain pattern in a tense and what's
/// different, worked out from the data (so it's right for every verb). For example, enviar in the present:
/// same endings as regular -ar verbs; envi- becomes enví- in yo, tú, él and ellos; nosotros and vosotros
/// are completely regular.
/// </summary>
public static class VerbCompare
{
    /// <summary>"regular -ar verbs", "regular -er and -ir verbs"… for this verb in this tense.</summary>
    public static string RegularName(Verb v, string t) => v.PatternIn(t) switch
    {
        "inf" => "regular verbs",
        "er-ir" => "regular -er and -ir verbs",
        var p => $"regular -{p} verbs",
    };

    /// <summary>Short person names: yo, tú, él, nosotros, vosotros, ellos (commands: tú, no + tú…).</summary>
    public static string Short(string t, int p) => t == "cmd"
        ? VerbGrammar.CommandPersons[p].Id switch
        {
            "tu" => "tú", "tu-neg" => "no + tú", "usted" => "usted", "nos" => "nosotros",
            "vos" => "vosotros", "vos-neg" => "no + vosotros", _ => "ustedes",
        }
        : new[] { "yo", "tú", "él", "nosotros", "vosotros", "ellos" }[p];

    private static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    /// <summary>The comparison as a few short lines (rules markup: *Spanish*, **bold**).</summary>
    public static List<string> Describe(Verb v, string t)
    {
        var info = VerbGrammar.TenseById[t];
        var persons = Enumerable.Range(0, VerbGrammar.PersonsFor(t).Count)
            .Where(p => v.HasForm(t, p) && VerbSettings.IsActive(t, p)).ToList();
        if (persons.Count == 0) return [];
        var reg = RegularName(v, t);
        var lines = new List<string>();

        if (info.IsGoingTo)
        {
            lines.Add("The same for every verb: *voy a* + the infinitive.");
            if (v.Reflexive) lines.Add($"The pronoun goes in front (*{v.Form(t, 0)}*) or on the end (*voy a {v.Inf[..^2]}me*). Both are right.");
            return lines;
        }

        if (info.IsCompound)
        {
            var regPp = v.PatternStem("perf") + (v.Class == "ar" ? "ado" : "ido");
            lines.Add("Same *haber* forms as every verb.");
            if (v.Participle == regPp)
                lines.Add($"Regular participle: *{v.Participle}*.");
            else if (AnswerCheck.StripAccents(v.Participle) == regPp)
                lines.Add($"The participle just gets an accent: *{v.Participle}* (not *{regPp}*).");
            else
                lines.Add($"Only the participle is different: *{v.Participle}* (not *{regPp}*).");
            if (v.Reflexive) lines.Add("The pronoun goes in front of *haber*: *" + v.Form(t, 0) + "*.");
            return lines;
        }

        var shapes = persons.ToDictionary(p => p, p => VerbParts.Analyse(v, t, p, v.Form(t, p)!));
        var regular = persons.Where(p => shapes[p].Regular).ToList();
        var own = persons.Where(p => shapes[p].Whole || (shapes[p].StemChanged && shapes[p].EndingChanged && shapes[p].Stem.Length <= 2)).ToList();

        if (regular.Count == persons.Count)
        {
            lines.Add($"Completely regular in this tense: the same as {reg}.");
            if (v.Reflexive) lines.Add(PronounLine(t));
            return lines;
        }

        // Mostly its own forms (ser, ir…): no point listing changes one by one.
        if (own.Count * 2 >= persons.Count)
        {
            var irregular = persons.Except(regular).ToList();
            lines.Add($"Mostly its own forms: *{string.Join(", ", irregular.Select(p => Display(v, t, p)))}*.");
            if (regular.Count > 0)
                lines.Add($"Follows {reg} in: {Join(regular.Select(p => Short(t, p)).ToList())}.");
            if (v.Reflexive) lines.Add(PronounLine(t));
            return lines;
        }

        var rest = persons.Except(own).ToList();

        // Endings
        var endChanged = rest.Where(p => shapes[p].EndingChanged).ToList();
        if (endChanged.Count == 0)
            lines.Add($"Same endings as {reg}.");
        else if (endChanged.Count == rest.Count && rest.Count >= 4)
            lines.Add($"Different endings from {reg}: *{string.Join(", ", rest.Select(p => "-" + shapes[p].Ending))}*.");
        else
        {
            var examples = endChanged.Take(3).Select(p => $"*{shapes[p].Word}* (not *{shapes[p].Stem + shapes[p].RegularEnding}*)").ToList();
            if (endChanged.Count > 3) examples.Add($"{endChanged.Count - 3} more");
            lines.Add($"Same endings as {reg}, except {Join(examples)}.");
        }

        // Stems: group the persons by their new stem
        var plain = v.PatternStem(t);
        var stemGroups = rest.Where(p => shapes[p].StemChanged)
            .GroupBy(p => shapes[p].Stem)
            .ToList();
        if (stemGroups.Count > 0 && plain.Length > 0)
        {
            var parts = stemGroups.Select(g => g.Count() == persons.Count
                ? $"*{g.Key}-* in every person"
                : stemGroups.Count == 1
                    ? $"*{g.Key}-* in {Join(g.Select(p => Short(t, p)).ToList())}"
                    : $"*{g.Key}-* ({string.Join(", ", g.Select(p => Short(t, p)))})").ToList();
            lines.Add($"The stem *{plain}-* becomes {Join(parts)}.");
        }

        if (own.Count > 0)
            lines.Add($"Its own form{(own.Count > 1 ? "s" : "")}: {Join(own.Select(p => $"*{Display(v, t, p)}* ({Short(t, p)})").ToList())}.");

        if (regular.Count > 0)
            lines.Add($"Completely regular: {Join(regular.Select(p => Short(t, p)).ToList())}.");

        if (v.Reflexive) lines.Add(PronounLine(t));
        return lines;
    }

    private static string Display(Verb v, string t, int p) =>
        (VerbGrammar.PersonsFor(t)[p].Negative ? "no " : "") + v.Form(t, p);

    private static string PronounLine(string t) => t == "cmd"
        ? "Plus the pronoun: in front of \"don't\" commands, on the end of the others (*acuérdate*)."
        : "Plus *me, te, se, nos, os, se* in front.";

    /// <summary>
    /// The persons where this verb differs from the plain pattern (used to pick a clear example for a group:
    /// a verb whose only changes are the ones the group is about).
    /// </summary>
    public static HashSet<int> ChangedPersons(Verb v, string t)
    {
        var set = new HashSet<int>();
        if (VerbGrammar.TenseById[t].IsCompound)
        {
            var regPp = v.PatternStem("perf") + (v.Class == "ar" ? "ado" : "ido");
            if (v.Reflexive || (!VerbGrammar.TenseById[t].IsGoingTo && v.Participle != regPp))
                for (var p = 0; p < 6; p++) if (v.HasForm(t, p)) set.Add(p);
            return set;
        }
        for (var p = 0; p < VerbGrammar.PersonsFor(t).Count; p++)
            if (v.Form(t, p) is { } f && !VerbParts.Analyse(v, t, p, f).Regular) set.Add(p);
        return set;
    }

    /// <summary>A regular verb of the same kind to show side by side (not the verb itself).</summary>
    public static Verb? RegularPartner(Verb v, string t, VerbBook book) =>
        book.PatternSamples(t, v.PatternIn(t)).FirstOrDefault(x => x != v);
}
