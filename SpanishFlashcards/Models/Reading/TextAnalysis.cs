using System.Text;
using System.Text.RegularExpressions;
using SpanishFlashcards.Components.Games;
using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Models.Reading;

/// <summary>How well you know a word in a text.</summary>
public enum ReadStatus
{
    /// <summary>Learned (level 4+ or marked learned).</summary>
    Known,
    /// <summary>A card you've started.</summary>
    Learning,
    /// <summary>In the word list (or your words), not started yet.</summary>
    New,
    /// <summary>Not in the word list; the dictionary knows it.</summary>
    NotInList,
    /// <summary>Not found anywhere (a typo, slang or a rare word).</summary>
    Unknown,
}

/// <summary>A word or phrase in the text, counted once however often it appears.</summary>
public sealed class ReadUnit
{
    /// <summary>The word list id, or "dict:" + headword, or "?:" + the word.</summary>
    public required string Key { get; init; }
    public Word? Word { get; init; }
    public DictEntry? Entry { get; init; }
    /// <summary>What to show: "casa", "tener", "por favor".</summary>
    public required string Head { get; init; }
    public required string Meaning { get; init; }
    public ReadStatus Status { get; set; }
    /// <summary>Times it appears.</summary>
    public int Count { get; set; }
    /// <summary>Words of text it covers (a two-word phrase twice = 4).</summary>
    public int Tokens { get; set; }
    /// <summary>For ordering: word list rank, or 10,000 + dictionary rank (list words first).</summary>
    public int Rank { get; init; }
    /// <summary>Token index of its first appearance.</summary>
    public int First { get; set; } = -1;
    /// <summary>You've already picked it to study for this text.</summary>
    public bool InStudy { get; set; }
}

/// <summary>One reading of a verb form: fue → (ser, preterite, él) and (ir, preterite, él).</summary>
public sealed record ReadReading(string Inf, string Tense, int Person);

/// <summary>A piece of the text: a word or the spaces and punctuation between words.</summary>
public sealed class ReadToken
{
    public required string Text { get; init; }
    public bool IsWord { get; init; }
    public int Sentence { get; init; }
    public ReadUnit? Unit { get; set; }
    /// <summary>The rest of a phrase that started at an earlier word (por [favor], he [comido]).</summary>
    public bool Continues { get; set; }
    /// <summary>A name (capital letter mid-sentence, not in the dictionary): not counted.</summary>
    public bool IsName { get; set; }
    /// <summary>Verbs from the Verbs tab: every way of reading the form.</summary>
    public IReadOnlyList<ReadReading> Readings { get; set; } = Array.Empty<ReadReading>();
    /// <summary>The whole phrase text for a phrase start ("he comido"); otherwise the word.</summary>
    public string Phrase { get; set; } = "";
    /// <summary>An extra line for the word's info ("Past participle of llamar.").</summary>
    public string? Note { get; set; }
}

/// <summary>A verb form as it appears in the text, and the token where it first appears.</summary>
public sealed record TenseExample(string Text, int Token);

/// <summary>
/// How much of the text is in one tense (shared forms are split between their tenses): a few different
/// forms as examples, and every place a form of it starts (token indexes).
/// </summary>
public sealed record TenseUse(string Tense, double Count, IReadOnlyList<TenseExample> Examples, IReadOnlyList<int> Tokens);

/// <summary>
/// Reads a pasted text against what you know: which words are known, being learned or new, which words
/// to learn first to understand most of it, and which verb tenses it uses.
/// </summary>
public sealed class TextAnalysis
{
    /// <summary>The share of words you need to know to read comfortably.</summary>
    public const double Target = 0.95;

    /// <summary>At most this many words are suggested at once.</summary>
    public const int MaxSuggested = 40;

    public List<ReadToken> Tokens { get; } = new();
    public List<string> Sentences { get; } = new();
    public Dictionary<string, ReadUnit> Units { get; } = new();

    /// <summary>Words that count (names and words found nowhere are left out).</summary>
    public int Countable { get; private set; }
    public int KnownTokens { get; private set; }
    public int LearningTokens { get; private set; }
    public int UnknownTokens { get; private set; }

    public double KnownShare => Countable == 0 ? 0 : (double)KnownTokens / Countable;
    public double LearningShare => Countable == 0 ? 0 : (double)LearningTokens / Countable;
    /// <summary>Known or being learned.</summary>
    public double CoveredShare => KnownShare + LearningShare;

    /// <summary>Every new word, most useful first (most often in the text, then most common in Spanish).</summary>
    public List<ReadUnit> ToLearn { get; } = new();
    /// <summary>The fewest words from <see cref="ToLearn"/> that take you to <see cref="Target"/>.</summary>
    public List<ReadUnit> Suggested { get; } = new();
    /// <summary>Known + learning + suggested.</summary>
    public double AfterSuggested { get; private set; }

    public List<TenseUse> Tenses { get; } = new();

    private static readonly Regex WordRx = new(@"\p{L}+(?:['’]\p{L}+)*", RegexOptions.Compiled);
    private static readonly string[] Clitics = ["los", "las", "les", "nos", "lo", "la", "le", "me", "te", "se", "os"];

    public static TextAnalysis Analyse(string text, IReadOnlyList<Word> words, Func<Word, CardState?> state,
                                       VerbBook? book, SpanishDictionary? dict, ISet<string> study)
    {
        var a = new TextAnalysis();
        a.Tokenize(text);

        // The word list by spelling: single words, and phrases (por favor, a veces) up to four words.
        var singles = new Dictionary<string, Word>(StringComparer.Ordinal);
        var phrases = new Dictionary<string, Word>(StringComparer.Ordinal);
        foreach (var w in words)
        {
            foreach (var variant in w.Es.Split(" / ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var key = variant.ToLowerInvariant();
                if (key.Contains(' ')) phrases.TryAdd(key, w);
                else singles.TryAdd(key, w);
            }
        }

        // Contractions: del = de + el, al = a + el.
        if (!singles.ContainsKey("del") && singles.TryGetValue("de", out var de)) singles["del"] = de;
        if (!singles.ContainsKey("al") && singles.TryGetValue("a", out var al)) singles["al"] = al;

        var wordIdx = Enumerable.Range(0, a.Tokens.Count).Where(i => a.Tokens[i].IsWord).ToList();

        // Past participles of the list's verbs, in all four forms: llamado, llamada, llamados, llamadas → llamar.
        var participles = new Dictionary<string, Word>(StringComparer.Ordinal);
        foreach (var (key, w) in singles)
        {
            if (w.Pos != "verb") continue;
            var inf = key.Length > 4 && key.EndsWith("se") ? key[..^2] : key;
            var verb = singles.GetValueOrDefault(inf) ?? w;
            var pp = (book is not null && (book.ByInf.TryGetValue(key, out var bv) || book.ByInf.TryGetValue(inf, out bv)))
                ? bv.Participle
                : RegularParticiple(inf);
            if (pp is null || !pp.EndsWith('o')) continue;
            var stem = pp[..^1];
            foreach (var f in new[] { pp, stem + "a", stem + "os", stem + "as" }) participles.TryAdd(f, verb);
        }

        ReadUnit WordUnit(Word w) => a.Unit(w.Es, () => new ReadUnit
        {
            Key = w.Es, Word = w, Head = w.Es, Meaning = w.En, Rank = w.Rank, InStudy = study.Contains(w.Es),
            Status = Srs.IsLearned(state(w)) ? ReadStatus.Known : Srs.IsNew(state(w)) ? ReadStatus.New : ReadStatus.Learning,
        });

        // The list may have a verb in its -se form (acercarse) where the dictionary has acercar.
        ReadUnit DictUnit(DictEntry e) =>
            singles.TryGetValue(e.Lemma.ToLowerInvariant(), out var listed)
            || (e.Pos == "v" && singles.TryGetValue(e.Lemma.ToLowerInvariant() + "se", out listed))
                ? WordUnit(listed)
                : a.Unit("dict:" + e.Lemma, () => new ReadUnit
                {
                    Key = "dict:" + e.Lemma, Entry = e, Head = e.Lemma, Meaning = e.Meaning, Rank = 10_000 + e.Rank,
                    Status = ReadStatus.NotInList,
                });

        // A verb form from the Verbs tab: the verb's word list entry (or its dictionary entry).
        ReadUnit? VerbUnit(IReadOnlyList<(Verb Verb, string Tense, int Person)> readings)
        {
            foreach (var r in readings)
            {
                if (singles.TryGetValue(r.Verb.Inf, out var w)) return WordUnit(w);
                if (dict is not null && dict.TryGetLemma(r.Verb.Inf, out var e)) return DictUnit(e);
            }
            return null;
        }

        IReadOnlyList<(Verb, string, int)> Readings(string form) =>
            book is null ? Array.Empty<(Verb, string, int)>() : VerbGameKit.Readings(book, form);

        static IReadOnlyList<ReadReading> Wrap(IEnumerable<(Verb Verb, string Tense, int Person)> rs) =>
            rs.Select(r => new ReadReading(r.Verb.Inf, r.Tense, r.Person)).ToList();

        // Verbs outside the Verbs tab: worked out from the regular endings (habló, comían, viviremos).
        IReadOnlyList<ReadReading> Guess(DictEntry e, string form) => GuessRegular(e, form, book);

        for (var k = 0; k < wordIdx.Count; k++)
        {
            var ti = wordIdx[k];
            var tok = a.Tokens[ti];
            if (tok.Continues) continue;

            // Phrases first: word list phrases, then verb forms of two or three words (he comido, me llamo, voy a ir).
            var matched = false;
            for (var n = Math.Min(4, wordIdx.Count - k); n >= 2 && !matched; n--)
            {
                if (!a.OnlySpaces(wordIdx[k], wordIdx[k + n - 1])) continue;
                var key = string.Join(' ', Enumerable.Range(k, n).Select(j => a.Tokens[wordIdx[j]].Text.ToLowerInvariant()));
                ReadUnit? unit = null;
                IReadOnlyList<ReadReading> readings = Array.Empty<ReadReading>();
                if (phrases.TryGetValue(key, out var pw)) unit = WordUnit(pw);
                else if (n <= 3 && Readings(key) is { Count: > 0 } rs)
                {
                    unit = VerbUnit(rs);
                    readings = Wrap(rs);
                }
                else if (n == 2 && dict is not null && Compound(Readings(a.Tokens[wordIdx[k]].Text.ToLowerInvariant()),
                             a.Tokens[wordIdx[k + 1]].Text.ToLowerInvariant(), dict) is { } comp)
                {
                    // haber + the participle of a verb outside the Verbs tab: ha anunciado, habían llegado.
                    unit = DictUnit(comp.Entry);
                    readings = comp.Readings;
                }
                if (unit is null) continue;
                a.Place(ti, unit, readings, string.Join(' ', Enumerable.Range(k, n).Select(j => a.Tokens[wordIdx[j]].Text)));
                for (var j = 1; j < n; j++)
                {
                    a.Tokens[wordIdx[k + j]].Unit = unit;
                    a.Tokens[wordIdx[k + j]].Continues = true;
                }
                unit.Tokens += n - 1;
                matched = true;
            }
            if (matched) continue;

            // One word.
            var lower = tok.Text.ToLowerInvariant();
            if (singles.TryGetValue(lower, out var sw))
            {
                var rs = sw.Pos == "verb" ? Wrap(Readings(lower)) : Array.Empty<ReadReading>();
                a.Place(ti, WordUnit(sw), rs, tok.Text);
                continue;
            }
            var verbReadings = Readings(lower);
            if (verbReadings.Count > 0 && VerbUnit(verbReadings) is { } vu)
            {
                a.Place(ti, vu, Wrap(verbReadings), tok.Text);
                continue;
            }
            // A capital letter mid-sentence: a name (María, Madrid), even if the dictionary has a word spelled the same.
            var sentenceStart = k == 0 || a.Tokens[wordIdx[k - 1]].Sentence != tok.Sentence;
            if (char.IsUpper(tok.Text[0]) && !sentenceStart)
            {
                tok.IsName = true;
                continue;
            }
            // A participle of a verb in the list (llamado, hecha, abiertos) is that verb, even when the dictionary also
            // has it as an adjective. Nouns stay their own word: el estado (state), la llamada (call), el resultado.
            if (participles.TryGetValue(lower, out var pv))
            {
                var own = dict?.Lookup(lower).FirstOrDefault(e => string.Equals(e.Lemma, lower, StringComparison.OrdinalIgnoreCase));
                if (own is null || own.Pos != "n")
                {
                    a.Place(ti, WordUnit(pv), Array.Empty<ReadReading>(), tok.Text);
                    tok.Note = own is { Pos: "adj" } && own.Meaning.Length > 0
                        ? $"Past participle of {pv.Es}. As an adjective: {own.Meaning}."
                        : $"Past participle of {pv.Es}.";
                    continue;
                }
            }
            if (dict?.Lookup(lower) is { Count: > 0 } entries)
            {
                var listed = entries.FirstOrDefault(e => singles.ContainsKey(e.Lemma.ToLowerInvariant()));
                var entry = listed ?? entries[0];
                a.Place(ti, DictUnit(entry), Guess(entry, lower), tok.Text);
                continue;
            }

            // Feminine and plural forms the dictionary doesn't list (tanta → tanto, mesas → mesa).
            var plain = BaseForms(lower).FirstOrDefault(b => singles.ContainsKey(b) || dict?.Lookup(b).Count > 0);
            if (plain is not null)
            {
                if (singles.TryGetValue(plain, out var pwd)) a.Place(ti, WordUnit(pwd), Array.Empty<ReadReading>(), tok.Text);
                else a.Place(ti, DictUnit(dict!.Lookup(plain)[0]), Array.Empty<ReadReading>(), tok.Text);
                continue;
            }

            // Pronouns on the end (dámelo, levantarse): take them off and try again.
            if (a.TryClitics(lower, ti, Readings, VerbUnit, Wrap, dict, DictUnit)) continue;
            a.Place(ti, a.Unit("?:" + lower, () => new ReadUnit
            {
                Key = "?:" + lower, Head = lower, Meaning = "", Rank = 100_000, Status = ReadStatus.Unknown,
            }), Array.Empty<ReadReading>(), tok.Text);
        }

        a.Count();
        return a;
    }

    // ------------------------------------------------------------------ pieces

    private static readonly Dictionary<string, string> CompoundOf = new()
    {
        ["pres"] = "perf", ["impf"] = "plup", ["fut"] = "futperf", ["cond"] = "condperf", ["subj"] = "subjperf", ["impsubj"] = "plupsubj",
    };

    /// <summary>A form of haber followed by a participle the dictionary knows: the compound tense.</summary>
    private static (DictEntry Entry, IReadOnlyList<ReadReading> Readings)? Compound(
        IReadOnlyList<(Verb Verb, string Tense, int Person)> first, string second, SpanishDictionary dict)
    {
        var haber = first.Where(r => r.Verb.Inf == "haber" && CompoundOf.ContainsKey(r.Tense)).ToList();
        if (haber.Count == 0) return null;
        if (!(second.EndsWith("ado") || second.EndsWith("ido") || second.EndsWith("ído") || second.EndsWith("to") || second.EndsWith("cho"))) return null;
        var verb = dict.Lookup(second).FirstOrDefault(e => e.Pos == "v");
        if (verb is null) return null;
        return (verb, haber.Select(r => new ReadReading(verb.Lemma, CompoundOf[r.Tense], r.Person)).ToList());
    }

    /// <summary>
    /// Readings of a regular form of a verb outside the Verbs tab, from the endings of hablar, comer and vivir.
    /// Irregular forms just get none.
    /// </summary>
    private static IReadOnlyList<ReadReading> GuessRegular(DictEntry e, string form, VerbBook? book)
    {
        if (book is null || e.Pos != "v") return Array.Empty<ReadReading>();
        var inf = e.Lemma.EndsWith("se", StringComparison.Ordinal) && e.Lemma.Length > 4 ? e.Lemma[..^2] : e.Lemma;
        if (inf.Length < 4) return Array.Empty<ReadReading>();
        var model = inf[^2..] switch { "ar" => "hablar", "er" => "comer", "ir" or "ír" => "vivir", _ => null };
        if (model is null || !book.ByInf.TryGetValue(model, out var m)) return Array.Empty<ReadReading>();
        var stem = inf[..^2];
        var list = new List<ReadReading>();
        foreach (var t in new[] { "pres", "pret", "impf", "fut", "cond", "subj", "impsubj" })
        {
            for (var p = 0; p < 6; p++)
            {
                var mf = m.Form(t, p);
                if (mf is null) continue;
                var cand = t is "fut" or "cond" ? inf + mf[model.Length..] : stem + mf[(model.Length - 2)..];
                if (string.Equals(cand, form, StringComparison.OrdinalIgnoreCase)) list.Add(new ReadReading(e.Lemma, t, p));
            }
        }
        return list;
    }

    /// <summary>hablar → hablado, comer → comido, vivir → vivido.</summary>
    private static string? RegularParticiple(string inf) =>
        inf.EndsWith("ar") ? inf[..^2] + "ado" :
        inf.EndsWith("er") || inf.EndsWith("ir") ? inf[..^2] + "ido" :
        null;

    /// <summary>Masculine singular guesses for a word: tanta → tanto, mesas → mesa, flores → flor.</summary>
    private static IEnumerable<string> BaseForms(string w)
    {
        if (w.Length < 4) yield break;
        if (w.EndsWith("as")) { yield return w[..^2] + "o"; yield return w[..^1]; }
        if (w.EndsWith("os")) yield return w[..^1];
        if (w.EndsWith("es")) { yield return w[..^2]; yield return w[..^1]; }
        if (w.EndsWith('s')) yield return w[..^1];
        if (w.EndsWith('a')) yield return w[..^1] + "o";
    }

    private ReadUnit Unit(string key, Func<ReadUnit> make)
    {
        if (!Units.TryGetValue(key, out var u)) Units[key] = u = make();
        return u;
    }

    private void Place(int token, ReadUnit unit, IReadOnlyList<ReadReading> readings, string phrase)
    {
        var t = Tokens[token];
        t.Unit = unit;
        t.Readings = readings;
        t.Phrase = phrase;
        unit.Count++;
        unit.Tokens++;
        if (unit.First < 0) unit.First = token;
    }

    private bool OnlySpaces(int fromToken, int toToken)
    {
        for (var i = fromToken + 1; i < toToken; i++)
            if (!Tokens[i].IsWord && !string.IsNullOrWhiteSpace(Tokens[i].Text)) return false;
        return true;
    }

    private bool TryClitics(string lower, int token,
                            Func<string, IReadOnlyList<(Verb, string, int)>> readings,
                            Func<IReadOnlyList<(Verb Verb, string Tense, int Person)>, ReadUnit?> verbUnit,
                            Func<IEnumerable<(Verb Verb, string Tense, int Person)>, IReadOnlyList<ReadReading>> wrap,
                            SpanishDictionary? dict, Func<DictEntry, ReadUnit> dictUnit)
    {
        var candidates = new List<string>();
        void Strip(string w, int depth)
        {
            foreach (var c in Clitics)
            {
                if (w.Length <= c.Length + 1 || !w.EndsWith(c, StringComparison.Ordinal)) continue;
                var rest = w[..^c.Length];
                candidates.Add(rest);
                candidates.Add(AnswerCheck.StripAccents(rest));
                if (depth < 2) Strip(rest, depth + 1);
            }
        }
        Strip(lower, 1);
        foreach (var c in candidates.Distinct())
        {
            var rs = readings(c);
            if (rs.Count > 0 && verbUnit(rs) is { } vu)
            {
                Place(token, vu, wrap(rs), Tokens[token].Text);
                return true;
            }
            if (dict?.Lookup(c) is { Count: > 0 } entries && entries[0].Pos == "v")
            {
                Place(token, dictUnit(entries[0]), Array.Empty<ReadReading>(), Tokens[token].Text);
                return true;
            }
        }
        return false;
    }

    /// <summary>Splits the text into words and the bits between them, numbering sentences.</summary>
    private void Tokenize(string text)
    {
        var sentence = 0;
        var pos = 0;
        var sb = new StringBuilder();
        void Gap(string gap)
        {
            if (gap.Length == 0) return;
            Tokens.Add(new ReadToken { Text = gap, IsWord = false, Sentence = sentence });
            sb.Append(gap);
            if (gap.IndexOfAny(['.', '!', '?', '…', '\n']) >= 0)
            {
                Sentences.Add(sb.ToString().Trim());
                sb.Clear();
                sentence++;
            }
        }
        foreach (Match m in WordRx.Matches(text))
        {
            Gap(text[pos..m.Index]);
            Tokens.Add(new ReadToken { Text = m.Value, IsWord = true, Sentence = sentence, Phrase = m.Value });
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        Gap(text[pos..]);
        if (sb.Length > 0) Sentences.Add(sb.ToString().Trim());
    }

    /// <summary>The sentence a token is in (for a card's example).</summary>
    public string SentenceOf(int token)
    {
        var s = Tokens[token].Sentence;
        return s < Sentences.Count ? Sentences[s] : "";
    }

    private void Count()
    {
        foreach (var t in Tokens)
        {
            if (!t.IsWord || t.IsName || t.Unit is null) continue;
            if (t.Unit.Status == ReadStatus.Unknown) { UnknownTokens++; continue; }
            Countable++;
            if (t.Unit.Status == ReadStatus.Known) KnownTokens++;
            else if (t.Unit.Status == ReadStatus.Learning) LearningTokens++;
        }

        ToLearn.AddRange(Units.Values
            .Where(u => u.Status is ReadStatus.New or ReadStatus.NotInList)
            .OrderByDescending(u => u.Tokens).ThenBy(u => u.Rank));

        var covered = KnownTokens + LearningTokens;
        foreach (var u in ToLearn)
        {
            if (Countable == 0 || (double)covered / Countable >= Target || Suggested.Count >= MaxSuggested) break;
            Suggested.Add(u);
            covered += u.Tokens;
        }
        AfterSuggested = Countable == 0 ? 0 : (double)covered / Countable;

        // Tenses: each verb form shares its count between the tenses it could be (hablamos: present and preterite).
        var counts = new Dictionary<string, double>();
        var examples = new Dictionary<string, List<TenseExample>>();
        var places = new Dictionary<string, List<int>>();
        for (var i = 0; i < Tokens.Count; i++)
        {
            var t = Tokens[i];
            if (t.Continues || t.Readings.Count == 0) continue;
            var tenses = t.Readings.Select(r => r.Tense).Distinct().ToList();
            foreach (var tense in tenses)
            {
                counts[tense] = counts.GetValueOrDefault(tense) + 1.0 / tenses.Count;
                if (!examples.TryGetValue(tense, out var list)) examples[tense] = list = new();
                if (!places.TryGetValue(tense, out var at)) places[tense] = at = new();
                at.Add(i);
                var shown = t.Phrase.ToLowerInvariant();
                if (list.Count < 5 && !list.Any(e => e.Text == shown)) list.Add(new TenseExample(shown, i));
            }
        }
        Tenses.AddRange(counts.OrderByDescending(kv => kv.Value)
            .Select(kv => new TenseUse(kv.Key, kv.Value, examples[kv.Key], places[kv.Key])));
    }
}
