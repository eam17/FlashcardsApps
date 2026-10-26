using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// A lesson finished on the old path (before the units, October 2026). Kept so older progress and exports still
/// load: the units read it once, to count lessons you passed (see VerbCourse.Migrate).
/// </summary>
public sealed class LessonRecord
{
    [JsonPropertyName("d")] public DateTime Done { get; set; }

    /// <summary>The practice score, 0 to 100 (0 when placed).</summary>
    [JsonPropertyName("s")] public int Score { get; set; }

    /// <summary>Skipped by the old placement check.</summary>
    [JsonPropertyName("p")] public bool Placed { get; set; }
}
