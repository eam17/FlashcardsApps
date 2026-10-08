namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// Explains a wrong answer in verb practice from what the answer actually was: another person's form, the
/// same person in another tense, the regular pattern used on an irregular verb, a stem change used where it
/// doesn't belong, a missing accent or pronoun. No guessing: each line says what the given form is, then
/// the rule that gives the right one. Lines use the rules markup (*Spanish*, **bold**).
/// </summary>
public static class VerbExplain
{
    private static readonly string[] SubjectPronouns =
        ["yo", "tú", "tu", "él", "el", "ella", "usted", "nosotros", "nosotras", "vosotros", "vosotras", "ellos", "ellas", "ustedes"];

    public static List<string> Mistake(Verb v, string t, int p, string? given, Grade grade)
    {
        var lines = new List<string>();
        var answer = v.Form(t, p);
        if (answer is null) return lines;
        var info = VerbGrammar.TenseById[t];
        var negative = VerbGrammar.PersonsFor(t)[p].Negative;
        var g = Clean(given, negative);
        var who = VerbCompare.Short(t, p);

        if (g.Length == 0)
        {
            lines.Add($"The {Name(t)} for *{who}* is *{answer}*.");
            AddRule(lines, v, t, p, answer);
            return lines;
        }

        if (grade == Grade.AccentSlip)
        {
            lines.Add(Accent(v, t, p, answer, g));
            return lines;
        }

        // Reflexive verb without its pronoun: levanto for me levanto.
        if (v.Reflexive && answer.Contains(' ') && Same(g, answer[(answer.IndexOf(' ') + 1)..]))
        {
            lines.Add($"*{v.Inf}* is reflexive: it needs its pronoun (*me, te, se, nos, os, se*). Here: *{answer}*.");
            return lines;
        }

        // The regular pattern on a verb that's different here.
        if (v.Regularised(t, p) is { } reg && Same(g, reg))
        {
            lines.Add($"*{reg}* is what the pattern of {VerbCompare.RegularName(v, t)} would give, but *{v.Inf}* is different here: *{answer}*.");
            AddRule(lines, v, t, p, answer, skipEnding: true);
            return lines;
        }

        // Another person, same tense.
        var persons = VerbGrammar.PersonsFor(t);
        for (var q = 0; q < persons.Count; q++)
        {
            if (q == p || !Matches(v, t, q, g)) continue;
            lines.Add($"*{g}* is the *{VerbCompare.Short(t, q)}* form ({persons[q].English}). For *{who}* ({persons[p].English}) it's *{answer}*.");
            if (EndingLine(v, t, p, answer) is { } end) lines.Add(end);
            return lines;
        }

        if (info.IsGoingTo && GoingTo(v, p, g, answer) is { } near)
        {
            lines.Add(near);
            return lines;
        }

        // Same person (or another one), another tense.
        foreach (var other in VerbGrammar.Tenses)
        {
            if (other.Id == t) continue;
            var q = PersonIn(t, p, other.Id);
            if (q >= 0 && Matches(v, other.Id, q, g))
            {
                lines.Add($"*{g}* is the {Name(other.Id)} ({other.English}). Here you need the {Name(t)} ({info.English}): *{answer}*.");
                if (Contrast(t, other.Id) is { } c) lines.Add(c);
                return lines;
            }
        }
        foreach (var other in VerbGrammar.Tenses)
        {
            if (other.Id == t) continue;
            var others = VerbGrammar.PersonsFor(other.Id);
            for (var q = 0; q < others.Count; q++)
            {
                if (!Matches(v, other.Id, q, g)) continue;
                lines.Add($"*{g}* is the {Name(other.Id)} for *{VerbCompare.Short(other.Id, q)}*. Here you need the {Name(t)} for *{who}*: *{answer}*.");
                if (Contrast(t, other.Id) is { } c) lines.Add(c);
                return lines;
            }
        }

        // A stem change used where the stem stays plain (puedemos for podemos).
        if (Boot(v, t, p, g, answer) is { } boot)
        {
            lines.Add(boot);
            return lines;
        }

        lines.Add($"The {Name(t)} of *{v.Inf}* for *{who}* is *{answer}*.");
        AddRule(lines, v, t, p, answer);
        return lines;
    }

    /// <summary>"present", "preterite", "command form"… (the tense name, lower case).</summary>
    private static string Name(string t) => t == "cmd"
        ? "command form"
        : VerbGrammar.TenseById[t].Name.ToLowerInvariant().Replace("(ir a)", "(*ir a*)");

    /// <summary>Why this form is what it is: its group's rule, or how the regular ending works.</summary>
    private static void AddRule(List<string> lines, Verb v, string t, int p, string answer, bool skipEnding = false)
    {
        var why = v.Why(t, p);
        if (why is not null)
        {
            var rule = VerbRules.WhyRule(why, t);
            lines.Add(rule.Length > 0 ? rule : $"*{v.Inf}* is irregular here: this form is learned as it is.");
            return;
        }
        if (!skipEnding && EndingLine(v, t, p, answer) is { } end) lines.Add(end);
    }

    /// <summary>"Regular -ar verbs: habl + -amos." (simple tenses whose ending can be split off).</summary>
    private static string? EndingLine(Verb v, string t, int p, string answer)
    {
        if (VerbGrammar.TenseById[t].IsCompound || t == "cmd") return null;
        var shape = VerbParts.Analyse(v, t, p, answer);
        if (shape.Whole || shape.Ending.Length == 0) return null;
        var who = VerbCompare.Short(t, p);
        return shape.StemChanged
            ? $"Stem *{shape.Stem}-* + the *{who}* ending *-{shape.Ending}*."
            : $"*{shape.Stem}-* + the *{who}* ending *-{shape.Ending}*.";
    }

    private static string Accent(Verb v, string t, int p, string answer, string given)
    {
        var plain = given;
        foreach (var other in VerbGrammar.Tenses)
        {
            var persons = VerbGrammar.PersonsFor(other.Id);
            for (var q = 0; q < persons.Count; q++)
            {
                if (other.Id == t && q == p) continue;
                if (v.Form(other.Id, q) is { } f && f == plain)
                    return $"Without the accent, *{plain}* is a different form: the {Name(other.Id)} for *{VerbCompare.Short(other.Id, q)}* ({other.English}). The accent makes it *{answer}*.";
            }
        }
        return $"The accent is part of the spelling: it shows which syllable is stressed. *{answer}*.";
    }

    private static string? GoingTo(Verb v, int p, string g, string answer)
    {
        if (Same(g, answer.Replace(" a ", " ")))
            return $"Don't forget the *a*: *{answer}* (*ir* + *a* + the infinitive).";
        var words = g.Split(' ');
        var right = answer.Split(' ');
        if (words.Length >= 3 && right.Length >= 3 && words[0] == right[0] && words[^1] != right[^1])
            return $"Only *ir* changes. The second verb stays as it is (the infinitive): *{answer}*.";
        if (words.Length >= 1 && right.Length >= 1 && words[0] != right[0])
            return $"\"Going to\" uses the present of *ir*: *voy, vas, va, vamos, vais, van*. For this person: *{answer}*." +
                   (words[0].StartsWith("ib", StringComparison.Ordinal) ? " (*iba a* means \"was going to\".)" : "");
        return null;
    }

    /// <summary>
    /// Boot verbs: the stem only changes where it's stressed. If this form keeps the plain stem but the answer
    /// used a changed stem with the right ending, say so.
    /// </summary>
    private static string? Boot(Verb v, string t, int p, string g, string answer)
    {
        if (v.Why(t, p) is not null || t is not ("pres" or "subj" or "cmd")) return null;
        var changes = Enumerable.Range(0, VerbGrammar.PersonsFor(t).Count)
            .Select(q => v.Why(t, q)).Any(w => w is "e-ie" or "o-ue" or "e-i" or "u-ue");
        if (!changes) return null;
        var shape = VerbParts.Analyse(v, t, p, answer);
        if (shape.Ending.Length == 0 || !g.EndsWith(shape.Ending, StringComparison.Ordinal)) return null;
        // Your answer changed the stem (not just a typo in the ending).
        if (g.StartsWith(shape.Stem, StringComparison.Ordinal) || g.Length <= shape.Ending.Length) return null;
        var stressed = t == "cmd" ? "*tú, usted, ustedes*" : "*yo, tú, él, ellos*";
        return $"Stem-changers only change the stem where it's stressed ({stressed}). " +
               $"*Nosotros* and *vosotros* keep the plain stem *{shape.Stem}-*: *{answer}*.";
    }

    /// <summary>A short contrast between two tenses that are easy to mix up.</summary>
    private static string? Contrast(string a, string b)
    {
        bool Pair(string x, string y) => (a == x && b == y) || (a == y && b == x);
        if (Pair("pret", "impf"))
            return "Preterite for one finished event (*ayer fui*); imperfect for background, habits and what was going on (*siempre iba*, *estaba lloviendo*).";
        if (Pair("pres", "pret"))
            return "Present for now or usually; preterite for one finished action in the past (*ayer*, *el año pasado*).";
        if (Pair("fut", "cond"))
            return "Future = will (*-é, -ás, -á*); conditional = would (*-ía, -ías, -ía*).";
        if (Pair("impf", "cond"))
            return "Both have *-ía*, but the conditional puts it on the whole infinitive: *comería* (would eat), *comía* (used to eat).";
        if (Pair("pres", "subj"))
            return "The subjunctive comes after *que* with wishes, doubts and feelings (*quiero que vengas*); the present just states a fact.";
        if (Pair("subj", "impsubj"))
            return "Present subjunctive after a present main verb (*quiero que venga*); past subjunctive after a past one or after *si* (*quería que viniera*, *si tuviera*).";
        if (Pair("perf", "pret"))
            return "*He hablado* (have spoken) is for recent things or experiences; the preterite is a finished event at a past time.";
        if (Pair("perf", "plup"))
            return "*he hablado* = have spoken; *había hablado* = had spoken, before another past moment.";
        if (a == "cmd" || b == "cmd")
            return "Commands: *tú* uses the *él* present (*habla*), but *no* + *tú*, *usted* and *ustedes* use the subjunctive (*no hables, hable, hablen*).";
        return null;
    }

    /// <summary>The person index in another tense that matches a person (command persons map to the plain ones). -1 if none.</summary>
    private static int PersonIn(string fromTense, int person, string toTense)
    {
        if (fromTense == toTense) return person;
        var group = VerbGrammar.PersonsFor(fromTense)[person].Group;
        var negative = VerbGrammar.PersonsFor(fromTense)[person].Negative;
        var to = VerbGrammar.PersonsFor(toTense);
        var best = -1;
        for (var i = 0; i < to.Count; i++)
        {
            if (to[i].Group != group) continue;
            if (best < 0 || to[i].Negative == negative) best = i;
        }
        return best;
    }

    private static bool Matches(Verb v, string t, int q, string g) =>
        v.HasForm(t, q) && v.Answers(t, q).Any(a => Same(g, a));

    private static bool Same(string g, string form) => g == AnswerCheck.Normalize(form);

    /// <summary>The answer normalised, without a leading subject pronoun (or "no" for negative commands).</summary>
    private static string Clean(string? given, bool negative)
    {
        var t = AnswerCheck.Normalize(given);
        if (negative && t.StartsWith("no ", StringComparison.Ordinal)) t = t[3..];
        foreach (var pr in SubjectPronouns)
        {
            if (t.StartsWith(pr + " ", StringComparison.Ordinal) && t.Length > pr.Length + 1)
            {
                t = t[(pr.Length + 1)..];
                break;
            }
        }
        if (negative && t.StartsWith("no ", StringComparison.Ordinal)) t = t[3..];
        return t;
    }
}
