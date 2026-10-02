namespace SpanishFlashcards.Models.Reading;

/// <summary>A dictionary headword: its meaning, part of speech and (nouns) gender. Rank 1 = most common.</summary>
public sealed record DictEntry(int Rank, string Lemma, string Pos, string Gender, string Meaning)
{
    /// <summary>Part of speech in the word list's terms ("noun", "verb"…).</summary>
    public string PosName => Pos switch
    {
        "n" => "noun",
        "v" => "verb",
        "adj" => "adjective",
        "adv" => "adverb",
        "prep" => "preposition",
        "conj" => "conjunction",
        "pron" => "pronoun",
        "interj" => "interjection",
        "num" => "number",
        "art" => "article",
        _ => "other",
    };
}

/// <summary>
/// The offline Spanish-English dictionary for the Read tab (wwwroot/data/dict.tsv, made by
/// tools/dictionary/build_dict.py from Wiktionary data, CC BY-SA). About 17,000 headwords with the forms
/// seen for each, so "casas", "dijeron" and "buena" find "casa", "decir" and "bueno".
/// </summary>
public sealed class SpanishDictionary
{
    private readonly Dictionary<string, List<DictEntry>> forms = new(StringComparer.Ordinal);

    public IReadOnlyList<DictEntry> Entries { get; }

    private SpanishDictionary(List<DictEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>Headwords a form can belong to, most common first ("casas": casa, then casar).</summary>
    public IReadOnlyList<DictEntry> Lookup(string word)
    {
        if (forms.TryGetValue(word.ToLowerInvariant(), out var hits)) return hits;
        return Array.Empty<DictEntry>();
    }

    public bool TryGetLemma(string lemma, out DictEntry entry)
    {
        foreach (var e in Lookup(lemma))
        {
            if (string.Equals(e.Lemma, lemma, StringComparison.OrdinalIgnoreCase)) { entry = e; return true; }
        }
        entry = null!;
        return false;
    }

    /// <summary>Lines: lemma, pos, gender, meaning, forms (comma separated), tab separated, most common first.</summary>
    public static SpanishDictionary Parse(string tsv)
    {
        var entries = new List<DictEntry>();
        var dict = new SpanishDictionary(entries);
        var rank = 0;
        foreach (var raw in tsv.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            var cols = line.Split('\t');
            if (cols.Length < 4) continue;
            var entry = new DictEntry(++rank, cols[0], cols[1], cols[2], cols[3]);
            entries.Add(entry);
            dict.Add(cols[0].ToLowerInvariant(), entry);
            if (cols.Length > 4 && cols[4].Length > 0)
                foreach (var form in cols[4].Split(','))
                    dict.Add(form, entry);
        }
        return dict;
    }

    private void Add(string form, DictEntry entry)
    {
        if (!forms.TryGetValue(form, out var list)) forms[form] = list = new List<DictEntry>(1);
        if (!list.Contains(entry)) list.Add(entry);
    }
}
