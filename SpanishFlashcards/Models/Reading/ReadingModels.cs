namespace SpanishFlashcards.Models.Reading;

/// <summary>A text you pasted into the Read tab, kept with your progress.</summary>
public sealed class SavedText
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime Added { get; set; } = DateTime.UtcNow;

    /// <summary>Words you chose to study for this text (word list ids, including your own words).</summary>
    public List<string> Study { get; set; } = new();

    /// <summary>Song lyrics found by search or by listening: where they came from (null for a pasted text).</summary>
    public SongInfo? Song { get; set; }
}

/// <summary>The song a text's lyrics belong to.</summary>
public sealed class SongInfo
{
    public string Track { get; set; } = "";
    public string Artist { get; set; } = "";
    public string? Album { get; set; }

    /// <summary>Length in seconds (0 if unknown).</summary>
    public double Duration { get; set; }

    /// <summary>The lyrics' id on LRCLIB.</summary>
    public long LrclibId { get; set; }

    /// <summary>Lyrics with a time for each line ("[01:23.45] line"), when LRCLIB has them; kept for following along.</summary>
    public string? Synced { get; set; }
}

/// <summary>
/// A word you added from a text that isn't in the app's word list (meaning from the dictionary, or
/// your own). It becomes a normal card, in the "My words" topic.
/// </summary>
public sealed class MyWord
{
    public string Es { get; set; } = "";
    public string En { get; set; } = "";
    /// <summary>"noun", "verb"… (the word list's names).</summary>
    public string Pos { get; set; } = "other";
    /// <summary>Nouns: "m" or "f".</summary>
    public string? Gender { get; set; }
    /// <summary>The sentence from your text it came from.</summary>
    public string Example { get; set; } = "";
    /// <summary>The word as it appears in that sentence (for the fill-the-gap step).</summary>
    public string? Form { get; set; }
    public string? TextId { get; set; }
    public DateTime Added { get; set; } = DateTime.UtcNow;

    /// <summary>As a card. Ranks start after the word list so these come last in frequency order.</summary>
    public Word ToWord(int rank) => new()
    {
        Es = Es,
        En = En,
        Pos = Pos,
        Example = Example,
        ExampleEn = "",
        Rank = rank,
        Topic = "My words",
        Gender = Pos == "noun" ? Gender : null,
        Article = Pos == "noun" ? Gender switch { "f" => "la", "m" => "el", _ => null } : null,
        Form = Form,
    };
}
