using SpanishFlashcards.Models.Verbs;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>A practice round or a test, kept in memory so you can leave it and come back (see VerbsHub).</summary>
public interface IVerbRound
{
    VerbNode Node { get; }
    bool InProgress { get; }
    bool IsTest { get; }
    DateTime LastActive { get; }

    /// <summary>"3 of 6"</summary>
    string PositionText { get; }
}
