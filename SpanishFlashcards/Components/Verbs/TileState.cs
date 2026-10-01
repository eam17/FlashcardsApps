namespace SpanishFlashcards.Components.Verbs;

/// <summary>Letter tiles for spelling an answer (see VerbQuiz.LetterTiles) and which ones were tapped, in order.</summary>
public sealed class TileState
{
    public TileState(List<string> tiles)
    {
        Tiles = tiles;
        Used = new bool[tiles.Count];
    }

    public static TileState Empty { get; } = new([]);

    public List<string> Tiles { get; }
    public bool[] Used { get; }
    public List<int> Picks { get; } = new();
    public string Typed => string.Concat(Picks.Select(i => Tiles[i]));
    public bool Any => Picks.Count > 0;

    public void Tap(int tile)
    {
        if (tile < 0 || tile >= Used.Length || Used[tile]) return;
        Used[tile] = true;
        Picks.Add(tile);
    }

    public void Undo()
    {
        if (Picks.Count == 0) return;
        Used[Picks[^1]] = false;
        Picks.RemoveAt(Picks.Count - 1);
    }

    public void Clear()
    {
        foreach (var i in Picks) Used[i] = false;
        Picks.Clear();
    }
}
