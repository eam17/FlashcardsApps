namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// What the Verbs tab's units need, built once from the verb data: the units, which unit each form belongs to,
/// the Decode pool, and the story verbs and gap sentences sorted into their units.
/// </summary>
public sealed class VerbKit
{
    private static VerbKit? cached;

    /// <summary>The kit for this verb data (built on first use, then shared).</summary>
    public static VerbKit For(VerbBook book, VerbNode tree)
    {
        if (cached is not null && ReferenceEquals(cached.Book, book)) return cached;
        cached = new VerbKit(book, tree);
        return cached;
    }

    public VerbBook Book { get; }
    public VerbNode Tree { get; }
    public IReadOnlyList<VerbUnit> Units { get; }
    public VerbDecode Decode { get; }

    private readonly Dictionary<string, VerbUnit> byId;
    private readonly Dictionary<string, VerbUnit> concrete = new();
    private readonly Dictionary<string, VerbUnit> pattern = new();
    private readonly Dictionary<string, DecodeItem> decodeIndex = new();

    private VerbKit(VerbBook book, VerbNode tree)
    {
        Book = book;
        Tree = tree;
        Units = VerbUnits.Build(book, tree);
        byId = Units.ToDictionary(u => u.Id);

        foreach (var u in Units)
        {
            foreach (var item in u.Items)
            {
                var f = item.Form;
                if (item.Verb is not null) concrete.TryAdd($"{item.Verb}|{f.Tense}|{f.PersonId}", u);
                else if (f.IsPattern) pattern.TryAdd(f.Key, u);
                else concrete.TryAdd(f.Key, u);
            }
        }

        Decode = new VerbDecode(book);
        foreach (var d in Decode.Items) decodeIndex.TryAdd($"{d.Verb.Inf}|{d.Tense}|{d.Person}", d);

        var choose = Units.Where(u => u.Kind == UnitKind.Choose).ToList();

        // Story verbs: in the unit of their form, and in a choose unit for their tense.
        foreach (var story in book.Stories)
        {
            foreach (var s in story.Sentences)
            {
                for (var i = 0; i < s.Tokens.Count; i++)
                {
                    var tok = s.Tokens[i];
                    if (!tok.IsVerb || !book.ByInf.TryGetValue(tok.V!, out var v)) continue;
                    var spot = new StorySpot(story, s, i, v, tok.Te!, tok.Person);
                    UnitOf(v, tok.Te!, tok.Person)?.Stories.Add(spot);
                    foreach (var cu in choose.Where(c => c.Def.Choose!.Contains(tok.Te!))) cu.Stories.Add(spot);
                }
            }
        }

        // Gap sentences: the unit they were written for (and the unit of their form, for a choose unit's sentence).
        foreach (var s in VerbSentences.All)
        {
            if (!book.ByInf.TryGetValue(s.Inf, out var v)) continue;
            var own = UnitOf(v, s.Tense, s.Person);
            var target = s.Unit.Length > 0 ? byId.GetValueOrDefault(s.Unit) : own;
            target?.Sentences.Add(s);
            if (target is { Kind: UnitKind.Choose } && own is not null && own != target) own.Sentences.Add(s);
        }

        // Clue-words sentences: the unit of their form, and a choose unit for their tense.
        foreach (var c in VerbClues.Sentences)
        {
            if (!book.ByInf.TryGetValue(c.Inf, out var v)) continue;
            var s = VerbSentences.FromClue(c);
            UnitOf(v, c.Tense, c.Person)?.Sentences.Add(s);
            foreach (var cu in choose.Where(x => x.Def.Choose!.Contains(c.Tense))) cu.Sentences.Add(s);
        }

        // Sentences a unit's forms can be read in: a phrase unit needs a few to choose between; others need a
        // verb with English (the meaning question).
        foreach (var u in Units)
        {
            u.HasReadSentences = u.Kind == UnitKind.Phrases
                ? u.Sentences.Count >= 3
                : u.Stories.Any(x => VerbEnglish.Has(x.Verb) && VerbSettings.IsActive(x.Tense, x.Person))
                  || u.Sentences.Any(x => book.ByInf.TryGetValue(x.Inf, out var sv) && VerbEnglish.Has(sv) && VerbSettings.IsActive(x.Tense, x.Person));
        }
    }

    public VerbUnit? Unit(string id) => byId.GetValueOrDefault(id);

    /// <summary>The unit a form is taught in (null when no unit has it, like a rare verb's form in a rare tense).</summary>
    public VerbUnit? UnitOf(Verb v, string tense, int person)
    {
        var persons = VerbGrammar.PersonsFor(tense);
        if (person < 0 || person >= persons.Count) return null;
        if (concrete.TryGetValue($"{v.Inf}|{tense}|{persons[person].Id}", out var u)) return u;
        if (v.Has(tense) && v.Why(tense, person) is null)
            return pattern.GetValueOrDefault(new FormRef(FormRef.PatternKey(v.PatternIn(tense)), tense, person).Key);
        return null;
    }

    /// <summary>The Decode item for a form (null for "going to", which Decode leaves out).</summary>
    public DecodeItem? DecodeItem(Verb v, string tense, int person) =>
        decodeIndex.GetValueOrDefault($"{v.Inf}|{tense}|{person}");
}
