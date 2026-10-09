namespace SpanishFlashcards.Models;

/// <summary>
/// Letter tiles for spelling a word on a card instead of typing it: every letter of the word (and a space
/// tile for each space), plus decoys so it can't be solved as an anagram: the accent twin of a letter
/// (o for ó, ñ for n…), the letters a look-alike word would need (same first letter, similar length), then
/// common letters. Not the whole alphabet: about half as many decoys as letters (4 to 8). Shuffled.
/// </summary>
public static class WordTiles
{
    private const string Accented = "áéíóúñü";
    private const string Plain = "aeiounu";
    private const string Common = "eaosnrildtcmup";

    /// <summary>What the tiles spell: the word's first spelling, without the article (casa, not la casa).</summary>
    public static string Target(Word w) =>
        w.Es.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0].ToLowerInvariant();

    public static List<string> For(Word w, IReadOnlyList<Word> all)
    {
        var rng = Random.Shared;
        var answer = Target(w);
        var letters = answer.Replace(" ", "");
        var tiles = answer.Select(c => c.ToString()).ToList();
        var have = letters.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var want = Math.Clamp((int)Math.Round(letters.Length * 0.6), 4, 8);
        var extras = new List<string>();

        void Add(char c)
        {
            var s = c.ToString();
            if (extras.Count >= want || c == ' ' || !char.IsLetter(c) || extras.Contains(s)) return;
            extras.Add(s);
        }

        // 1. Accent traps: the plain twin of each accented letter, or (with none) an accented twin of a vowel.
        foreach (var c in letters)
        {
            var i = Accented.IndexOf(c);
            if (i >= 0) Add(Plain[i]);
        }
        if (!letters.Any(c => Accented.Contains(c)))
        {
            var vowels = letters.Where(c => "aeiou".Contains(c)).Distinct().OrderBy(_ => rng.Next()).ToList();
            if (vowels.Count > 0) Add(Accented["aeiou".IndexOf(vowels[0])]);
            if (letters.Contains('n') && rng.Next(2) == 0) Add('ñ');
        }

        // 2. Letters a look-alike word would need: same first letter, similar length.
        var lookAlikes = all
            .Where(o => o != w)
            .Select(o => Target(o))
            .Where(o => o.Length > 0 && o[0] == answer[0] && Math.Abs(o.Length - answer.Length) <= 2 && o != answer)
            .Distinct()
            .OrderBy(_ => rng.Next())
            .Take(3);
        foreach (var near in lookAlikes)
        {
            foreach (var g in near.Replace(" ", "").GroupBy(c => c).OrderBy(_ => rng.Next()))
                if (g.Count() > have.GetValueOrDefault(g.Key)) Add(g.Key);
        }

        // 3. Common letters, to make up the number.
        foreach (var c in Common.OrderBy(_ => rng.Next()))
        {
            if (extras.Count >= want) break;
            Add(c);
        }

        tiles.AddRange(extras);
        // Shuffle until the tiles don't already spell the word.
        List<string> shuffled;
        var tries = 0;
        do shuffled = tiles.OrderBy(_ => rng.Next()).ToList();
        while (string.Concat(shuffled).StartsWith(answer, StringComparison.Ordinal) && ++tries < 10);
        return shuffled;
    }
}
