using System.Text;

namespace SpanishFlashcards.Models;

/// <summary>How a typed Spanish answer compares with the word.</summary>
public enum WordCheckResult
{
    /// <summary>Right (the word, or the form that fits the example sentence).</summary>
    Right,
    /// <summary>Right except for a missing accent or ñ.</summary>
    Accent,
    /// <summary>The right noun with the wrong article (el mano).</summary>
    Article,
    /// <summary>One letter off (a typo, or nearly remembered).</summary>
    Almost,
    Wrong,
}

/// <summary>
/// Checks a typed Spanish answer on a word card. Accepts the dictionary form (hablar, casa) or the form in
/// the example sentence (Hablo, casas), with or without an article for nouns. A missing accent, a wrong
/// article or a one-letter slip is "nearly": it counts as Hard, not as forgotten.
/// </summary>
public static class WordCheck
{
    private static readonly string[] Articles = ["el", "la", "los", "las", "un", "una", "unos", "unas"];

    /// <param name="otherWords">Every spelling in the word list (from <see cref="Spellings"/>): typing a different
    /// real word (papa for papá, si for sí, coger for comer) is wrong, not "nearly".</param>
    public static WordCheckResult Check(string? typed, Word w, ISet<string>? otherWords = null)
    {
        var t = Normalize(typed);
        if (t.Length == 0) return WordCheckResult.Wrong;

        // A leading article on a noun is checked separately ("la mano" → "la" + "mano").
        string? article = null;
        var core = t;
        if (w.IsNoun)
        {
            foreach (var a in Articles)
            {
                if (t.Length > a.Length + 1 && t.StartsWith(a + " ", StringComparison.Ordinal))
                {
                    article = a;
                    core = t[(a.Length + 1)..];
                    break;
                }
            }
        }

        var targets = Targets(w);
        if (targets.Contains(t) || targets.Contains(core))
        {
            // The form in the example (los años, la enfermera) can take a different article than the headword.
            var fromSentence = !string.IsNullOrWhiteSpace(w.Form) && core == Normalize(w.Form) && !Spellings(w).Contains(core);
            if (t != core && !fromSentence && !ArticleFits(w, article!)) return WordCheckResult.Article;
            return WordCheckResult.Right;
        }
        // A different word from the list is just wrong, however close it looks.
        if (otherWords is not null && (otherWords.Contains(core) || otherWords.Contains(t))) return WordCheckResult.Wrong;

        var articleOk = article is null || ArticleFits(w, article);
        var best = WordCheckResult.Wrong;
        foreach (var target in targets)
        {
            WordCheckResult r;
            if (t == target || core == target) r = WordCheckResult.Right;
            else if (Plain(t) == Plain(target) || Plain(core) == Plain(target)) r = articleOk ? WordCheckResult.Accent : WordCheckResult.Article;
            else if (target.Length >= 5 && (OneEditApart(Plain(core), Plain(target)) || OneEditApart(Plain(t), Plain(target))))
                r = WordCheckResult.Almost;
            else continue;
            if (r < best) best = r;
        }
        return best;
    }

    /// <summary>Lower case, single spaces, no ¿ ? ¡ ! or trailing full stop, typographic apostrophes fixed.</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant().Replace('’', '\'');
        t = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return t.Trim('.', '!', '¡', '?', '¿', ',', ';', ':', ' ');
    }

    /// <summary>The accepted answers: every spelling of the word ("el / la" → el, la) and its form in the sentence.</summary>
    private static List<string> Targets(Word w)
    {
        var list = Spellings(w).ToList();
        if (!string.IsNullOrWhiteSpace(w.Form)) list.Add(Normalize(w.Form));
        return list.Where(x => x.Length > 0).Distinct().ToList();
    }

    /// <summary>The headword's spellings ("el / la" → el, la), normalized.</summary>
    private static HashSet<string> Spellings(Word w) =>
        w.Es.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize).Where(x => x.Length > 0).ToHashSet();

    /// <summary>Every spelling of every word in the list (for <see cref="Check"/>'s "a different word" rule).</summary>
    public static HashSet<string> Spellings(IEnumerable<Word> words)
    {
        var all = new HashSet<string>();
        foreach (var w in words) all.UnionWith(Spellings(w));
        return all;
    }

    /// <summary>Does the typed article go with this noun? (el / un for el-nouns, including el agua; la / una…)</summary>
    private static bool ArticleFits(Word w, string article) => w.Article switch
    {
        "el / la" => article is "el" or "la" or "un" or "una",
        "el" => article is "el" or "un",
        "la" => article is "la" or "una",
        "los" => article is "los" or "unos",
        "las" => article is "las" or "unas",
        _ => true,
    };

    /// <summary>Without accents or ñ (está → esta, año → ano).</summary>
    private static string Plain(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c switch
            {
                'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u', 'ü' => 'u', 'ñ' => 'n',
                _ => c,
            });
        }
        return sb.ToString();
    }

    /// <summary>True when the two differ by exactly one letter added, removed, changed or swapped with its neighbour.</summary>
    private static bool OneEditApart(string a, string b)
    {
        if (a == b || Math.Abs(a.Length - b.Length) > 1) return false;
        if (a.Length == b.Length)
        {
            var diffs = new List<int>();
            for (var i = 0; i < a.Length && diffs.Count <= 2; i++)
                if (a[i] != b[i]) diffs.Add(i);
            if (diffs.Count == 1) return true;
            return diffs.Count == 2 && diffs[1] == diffs[0] + 1 && a[diffs[0]] == b[diffs[1]] && a[diffs[1]] == b[diffs[0]];
        }
        var (s, l) = a.Length < b.Length ? (a, b) : (b, a);
        int si = 0, li = 0;
        var skipped = false;
        while (si < s.Length && li < l.Length)
        {
            if (s[si] == l[li]) { si++; li++; continue; }
            if (skipped) return false;
            skipped = true;
            li++;
        }
        return true;
    }
}
