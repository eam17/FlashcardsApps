namespace SpanishFlashcards.Models.Verbs;

public enum PartKind { Stem, Ending, Change, Pronoun, Helper }

public sealed record FormPart(string Text, PartKind Kind);

/// <summary>
/// Splits a conjugated form into coloured parts for the tables: stem, ending, what changed compared with the
/// plain pattern, the reflexive pronoun (me, te, se…) and haber in compound tenses.
/// "quiero" → qu·ie·r (stem, change, stem) + o (ending); "me acuerdo" → me + ac·ue·rd + o; "he hablado" → he + habl + ado.
/// An ending that differs from the regular one (tuve, leíste) is shown as a change.
/// </summary>
public static class VerbParts
{
    private static readonly Dictionary<string, string[]> End = new()
    {
        ["pres|ar"] = ["o", "as", "a", "amos", "áis", "an"],
        ["pres|er"] = ["o", "es", "e", "emos", "éis", "en"],
        ["pres|ir"] = ["o", "es", "e", "imos", "ís", "en"],
        ["pret|ar"] = ["é", "aste", "ó", "amos", "asteis", "aron"],
        ["pret|er"] = ["í", "iste", "ió", "imos", "isteis", "ieron"],
        ["pret|ir"] = ["í", "iste", "ió", "imos", "isteis", "ieron"],
        ["impf|ar"] = ["aba", "abas", "aba", "ábamos", "abais", "aban"],
        ["impf|er"] = ["ía", "ías", "ía", "íamos", "íais", "ían"],
        ["impf|ir"] = ["ía", "ías", "ía", "íamos", "íais", "ían"],
        ["fut|ar"] = ["é", "ás", "á", "emos", "éis", "án"],
        ["cond|ar"] = ["ía", "ías", "ía", "íamos", "íais", "ían"],
        ["subj|ar"] = ["e", "es", "e", "emos", "éis", "en"],
        ["subj|er"] = ["a", "as", "a", "amos", "áis", "an"],
        ["subj|ir"] = ["a", "as", "a", "amos", "áis", "an"],
        ["impsubj|ar"] = ["ara", "aras", "ara", "áramos", "arais", "aran"],
        ["impsubj|er"] = ["iera", "ieras", "iera", "iéramos", "ierais", "ieran"],
        ["impsubj|ir"] = ["iera", "ieras", "iera", "iéramos", "ierais", "ieran"],
    };

    // Endings of the special-stem preterites (tuve, dije) and what they give in the imperfect subjunctive (dijera).
    private static readonly string[] PretStemEnd = ["e", "iste", "o", "imos", "isteis", "ieron"];
    private static readonly string[] PretJEnd = ["e", "iste", "o", "imos", "isteis", "eron"];
    private static readonly string[] ImpsubjJEnd = ["era", "eras", "era", "éramos", "erais", "eran"];
    private static readonly string[] Classes = ["ar", "er", "ir"];

    private static readonly (string Src, string Dst)[] StemChanges =
        [("e", "ie"), ("o", "ue"), ("u", "ue"), ("e", "i"), ("o", "u")];

    /// <summary>Reflexive pronoun attached to a positive command, by person group.</summary>
    private static string? AttachedPronoun(string group) => group switch
    {
        "tu" => "te", "el" => "se", "nos" => "nos", "vos" => "os", "ellos" => "se", _ => null,
    };

    public static List<FormPart> Split(Verb v, string tense, int person, string form)
    {
        var parts = new List<FormPart>();
        var info = VerbGrammar.TenseById[tense];
        var rest = form;
        string? tailPronoun = null;

        // Reflexive pronoun in front: me acuerdo, te acuerdes, me he acordado.
        if (v.Reflexive)
        {
            foreach (var pr in new[] { "nos ", "me ", "te ", "se ", "os " })
            {
                if (!rest.StartsWith(pr, StringComparison.Ordinal)) continue;
                parts.Add(new(pr.Trim(), PartKind.Pronoun));
                parts.Add(new(" ", PartKind.Stem));
                rest = rest[pr.Length..];
                break;
            }
            // ...or on the end of a positive command: acuérdate, acordémonos.
            if (info.IsCommand && rest == form)
            {
                var p = VerbGrammar.CommandPersons[person];
                var pr = p.Negative ? null : AttachedPronoun(p.Group);
                if (pr is not null && rest.Length > pr.Length && rest.EndsWith(pr, StringComparison.Ordinal))
                {
                    tailPronoun = pr;
                    rest = rest[..^pr.Length];
                }
            }
        }

        if (info.IsGoingTo)
        {
            // voy a + infinitive (the infinitive never changes)
            var words = rest.Split(' ');
            if (words.Length >= 3)
            {
                parts.Add(new(words[0] + " " + words[1], PartKind.Helper));
                parts.Add(new(" ", PartKind.Stem));
                rest = string.Join(' ', words.Skip(2));
            }
            parts.Add(new(rest, PartKind.Stem));
        }
        else if (info.IsCompound)
        {
            // haber + participle
            var space = rest.IndexOf(' ');
            if (space > 0)
            {
                parts.Add(new(rest[..space], PartKind.Helper));
                parts.Add(new(" ", PartKind.Stem));
                rest = rest[(space + 1)..];
            }
            parts.AddRange(SplitWord(rest, v.PatternStem("perf"), v.Class == "ar" ? ["ado"] : ["ido", "ído"],
                                     v.Class == "ar" ? "ado" : "ido", minPrefix: 1));
        }
        else
        {
            var (candidates, regular) = Endings(v, tense, person, trimmed: tailPronoun is "nos" or "os");
            parts.AddRange(SplitWord(rest, v.PatternStem(tense), candidates, regular, minPrefix: 1));
        }

        if (tailPronoun is not null) parts.Add(new(tailPronoun, PartKind.Pronoun));
        return Merge(parts);
    }

    /// <summary>
    /// A simple-tense form compared with the plain pattern: its stem and ending, and whether each differs
    /// from the regular one. Whole = no recognisable ending at all (soy, di, haz). Pronouns are left out.
    /// </summary>
    public sealed record FormShape(string Stem, bool StemChanged, string Ending, bool EndingChanged, string RegularEnding, bool Whole)
    {
        public bool Regular => !StemChanged && !EndingChanged && !Whole;
        public string Word => Stem + Ending;
    }

    public static FormShape Analyse(Verb v, string tense, int person, string form)
    {
        var rest = form;
        string? tail = null;
        if (v.Reflexive)
        {
            foreach (var pr in new[] { "nos ", "me ", "te ", "se ", "os " })
            {
                if (!rest.StartsWith(pr, StringComparison.Ordinal)) continue;
                rest = rest[pr.Length..];
                break;
            }
            if (tense == "cmd" && rest == form)
            {
                var cp = VerbGrammar.CommandPersons[person];
                var pr = cp.Negative ? null : AttachedPronoun(cp.Group);
                if (pr is not null && rest.Length > pr.Length && rest.EndsWith(pr, StringComparison.Ordinal))
                {
                    tail = pr;
                    rest = rest[..^pr.Length];
                }
            }
        }

        var stem = v.PatternStem(tense);
        var (candidates, regular) = Endings(v, tense, person, trimmed: tail is "nos" or "os");
        var bare = AnswerCheck.StripAccents(rest);
        foreach (var e in candidates)
        {
            if (rest.Length - e.Length < 1 || rest[..^e.Length] != stem) continue;
            if (!bare.EndsWith(AnswerCheck.StripAccents(e), StringComparison.Ordinal)) continue;
            var end = rest[^e.Length..];
            return new FormShape(stem, false, end, end != regular, regular, false);
        }
        foreach (var e in candidates)
        {
            if (rest.Length - e.Length < 1) continue;
            if (!bare.EndsWith(AnswerCheck.StripAccents(e), StringComparison.Ordinal)) continue;
            var end = rest[^e.Length..];
            var prefix = rest[..^e.Length];
            return new FormShape(prefix, prefix != stem, end, end != regular, regular, false);
        }
        return new FormShape(rest, rest != stem, "", true, regular, rest != stem);
    }

    /// <summary>The ending this verb would have in this slot if it were regular (simple tenses only).</summary>
    public static string RegularEnding(Verb v, string tense, int person) => Endings(v, tense, person, trimmed: false).Regular;

    /// <summary>The endings a form can have in this slot (any verb class, plus irregular ones) and this verb's regular one.</summary>
    private static (List<string> Candidates, string Regular) Endings(Verb v, string tense, int person, bool trimmed)
    {
        string t = tense;
        int p = person;
        string? vosCommand = null;
        if (tense == "cmd")
        {
            (t, p) = VerbGrammar.CommandPersons[person].Id switch
            {
                "tu" => ("pres", 2),
                "tu-neg" => ("subj", 1),
                "usted" => ("subj", 2),
                "nos" => ("subj", 3),
                "vos-neg" => ("subj", 4),
                "ustedes" => ("subj", 5),
                _ => ("vos", 0),
            };
            if (t == "vos") vosCommand = v.Class switch { "ar" => "ad", "er" => "ed", _ => "id" };
        }

        var list = new List<string>();
        string regular;
        if (vosCommand is not null)
        {
            list.AddRange(["ad", "ed", "id"]);
            regular = vosCommand;
        }
        else
        {
            var key = t is "fut" or "cond" ? $"{t}|ar" : null;
            foreach (var c in Classes) list.Add(End[key ?? $"{t}|{c}"][p]);
            regular = End[key ?? $"{t}|{v.Class}"][p];
            if (t == "pret") { list.Add(PretStemEnd[p]); list.Add(PretJEnd[p]); }
            if (t == "impsubj") list.Add(ImpsubjJEnd[p]);
        }
        // Reflexive nosotros / vosotros commands drop their last letter before the pronoun (acordémonos, acordaos).
        if (trimmed)
        {
            list = list.Select(e => e.Length > 1 ? e[..^1] : e).ToList();
            regular = regular.Length > 1 ? regular[..^1] : regular;
        }
        return (list.Distinct().OrderByDescending(e => e.Length).ToList(), regular);
    }

    /// <summary>One word → stem (with any changed letters marked) + ending.</summary>
    private static List<FormPart> SplitWord(string word, string stem, IReadOnlyList<string> candidates, string regularEnding, int minPrefix)
    {
        var bareWord = AnswerCheck.StripAccents(word);
        // First choice: the plain stem followed by an ending (cambi + ó, not camb + ió).
        foreach (var e in candidates.OrderByDescending(x => x.Length))
        {
            if (word.Length - e.Length < minPrefix || word[..^e.Length] != stem) continue;
            if (!bareWord.EndsWith(AnswerCheck.StripAccents(e), StringComparison.Ordinal)) continue;
            var end = word[^e.Length..];
            return [new(stem, PartKind.Stem), new(end, end == regularEnding ? PartKind.Ending : PartKind.Change)];
        }
        foreach (var e in candidates.OrderByDescending(x => x.Length))
        {
            if (word.Length - e.Length < minPrefix) continue;
            if (!bareWord.EndsWith(AnswerCheck.StripAccents(e), StringComparison.Ordinal)) continue;
            var prefix = word[..^e.Length];
            var ending = word[^e.Length..];
            var parts = StemParts(prefix, stem);
            parts.Add(new(ending, ending == regularEnding ? PartKind.Ending : PartKind.Change));
            return parts;
        }
        // No recognisable ending (soy, di, haz, hecho): the whole word is special.
        return word == stem ? [new(word, PartKind.Stem)] : [new(word, PartKind.Change)];
    }

    /// <summary>Compares the stem in the form with the plain stem and marks what's different.</summary>
    private static List<FormPart> StemParts(string actual, string stem)
    {
        if (actual == stem) return [new(actual, PartKind.Stem)];

        // A stem-change vowel (quier, acuérd, pid): mark the whole new vowel group.
        var bare = AnswerCheck.StripAccents(actual);
        foreach (var (src, dst) in StemChanges)
        {
            for (var i = bare.IndexOf(dst, StringComparison.Ordinal); i >= 0; i = bare.IndexOf(dst, i + 1, StringComparison.Ordinal))
            {
                if (bare[..i] + src + bare[(i + dst.Length)..] != stem) continue;
                return Parts((actual[..i], PartKind.Stem), (actual.Substring(i, dst.Length), PartKind.Change),
                             (actual[(i + dst.Length)..], PartKind.Stem));
            }
        }

        // Otherwise: same start and end, different middle (busqu, conozc, tendr).
        var pre = 0;
        while (pre < actual.Length && pre < stem.Length && actual[pre] == stem[pre]) pre++;
        var post = 0;
        while (post < actual.Length - pre && post < stem.Length - pre
               && actual[actual.Length - 1 - post] == stem[stem.Length - 1 - post]) post++;
        if (pre == 0 && post == 0) return [new(actual, PartKind.Change)];
        var middle = actual.Substring(pre, actual.Length - pre - post);
        if (middle.Length == 0)
        {
            // Letters were dropped (podr from poder): mark the letters after the gap.
            return Parts((actual[..pre], PartKind.Stem), (actual[pre..], PartKind.Change));
        }
        return Parts((actual[..pre], PartKind.Stem), (middle, PartKind.Change), (actual[(actual.Length - post)..], PartKind.Stem));
    }

    private static List<FormPart> Parts(params (string Text, PartKind Kind)[] items) =>
        items.Where(x => x.Text.Length > 0).Select(x => new FormPart(x.Text, x.Kind)).ToList();

    private static List<FormPart> Merge(List<FormPart> parts)
    {
        var merged = new List<FormPart>();
        foreach (var p in parts.Where(p => p.Text.Length > 0))
        {
            if (merged.Count > 0 && merged[^1].Kind == p.Kind) merged[^1] = merged[^1] with { Text = merged[^1].Text + p.Text };
            else merged.Add(p);
        }
        return merged;
    }

    public static string Label(PartKind k) => k switch
    {
        PartKind.Stem => "stem",
        PartKind.Ending => "ending",
        PartKind.Change => "changed",
        PartKind.Pronoun => "pronoun",
        _ => "haber",
    };

    public static string Css(PartKind k) => k switch
    {
        PartKind.Stem => "pt-stem",
        PartKind.Ending => "pt-end",
        PartKind.Change => "pt-chg",
        PartKind.Pronoun => "pt-pron",
        _ => "pt-aux",
    };
}
