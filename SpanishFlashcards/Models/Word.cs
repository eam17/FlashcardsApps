namespace SpanishFlashcards.Models;

/// <summary>One vocabulary card. Rank is its position in the frequency list (1 = most common).</summary>
public sealed class Word
{
    public required string Es { get; init; }
    public required string En { get; init; }
    public required string Pos { get; init; }
    public required string Example { get; init; }
    public required string ExampleEn { get; init; }
    public required int Rank { get; init; }

    /// <summary>Topic such as "Food &amp; drink".</summary>
    public required string Topic { get; init; }

    /// <summary>Nouns only: "m", "f" or "mf" (either).</summary>
    public string? Gender { get; init; }

    /// <summary>Nouns only: "el", "la", "las", "el / la"…</summary>
    public string? Article { get; init; }

    /// <summary>Verbs only: "yo hablo · tú hablas · él habla | Past: yo hablé · él habló".</summary>
    public string? Conjugation { get; init; }

    /// <summary>The exact text in <see cref="Example"/> to blank out when English is shown first.</summary>
    public string? Form { get; init; }

    /// <summary>Frequency group of 100, e.g. "101–200".</summary>
    public string Band => BandFor(Rank);

    public string Tag => $"#{Rank} · {Pos}";

    public bool IsNoun => Pos == "noun" && Article is not null;

    /// <summary>What to show / say as the Spanish side, e.g. "la casa".</summary>
    public string SpanishWithArticle => IsNoun ? $"{Article} {Es}" : Es;

    /// <summary>CSS class for the gender colour.</summary>
    public string GenderClass => Gender switch
    {
        "m" => "g-m",
        "f" => "g-f",
        "mf" => "g-mf",
        _ => "",
    };

    public string GenderLabel => Gender switch
    {
        "m" => "masculine",
        "f" => "feminine",
        "mf" => "masculine or feminine",
        _ => "",
    };

    /// <summary>Article chip text; flags feminine nouns that take "el" (el agua).</summary>
    public string ArticleChip => Gender == "f" && Article == "el" ? "el (fem.)" : Article ?? "";

    public (string Present, string? Past) ConjugationParts
    {
        get
        {
            if (Conjugation is null) return ("", null);
            var parts = Conjugation.Split(" | Past: ");
            return (parts[0], parts.Length > 1 ? parts[1] : null);
        }
    }

    /// <summary>The example split around the blank: (before, after). Null if the word can't be found.</summary>
    public (string Before, string After)? ClozeParts
    {
        get
        {
            if (string.IsNullOrEmpty(Form)) return null;
            var i = Example.IndexOf(Form, StringComparison.Ordinal);
            if (i < 0) return null;
            return (Example[..i], Example[(i + Form.Length)..]);
        }
    }

    public static string BandFor(int rank)
    {
        var lo = (rank - 1) / 100 * 100 + 1;
        return $"{lo}–{lo + 99}";
    }
}
