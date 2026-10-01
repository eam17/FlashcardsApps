using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>wwwroot/data/stories.json (made by tools/stories/build.py).</summary>
public sealed class StoryFile
{
    public List<Story> Stories { get; set; } = new();
}

/// <summary>A short story written to show one tense in use.</summary>
public sealed class Story
{
    public string Tense { get; set; } = "";
    public string Title { get; set; } = "";
    public string TitleEn { get; set; } = "";
    [JsonPropertyName("s")] public List<StorySentence> Sentences { get; set; } = new();
}

public sealed class StorySentence
{
    [JsonPropertyName("t")] public List<StoryToken> Tokens { get; set; } = new();
    public string En { get; set; } = "";

    public string Text => string.Concat(Tokens.Select(t => t.X));
}

/// <summary>A piece of a sentence: plain text, or a verb with its infinitive, tense and person(s).</summary>
public sealed class StoryToken
{
    public string X { get; set; } = "";

    /// <summary>The infinitive, for verbs.</summary>
    public string? V { get; set; }

    /// <summary>The tense id, for verbs.</summary>
    public string? Te { get; set; }

    /// <summary>Person indexes (two when the form is the same, like hablaba for yo and él).</summary>
    public int[]? P { get; set; }

    [JsonIgnore] public bool IsVerb => V is not null && Te is not null;

    [JsonIgnore] public int Person => P is { Length: > 0 } p ? p[0] : 0;

    /// <summary>"yo / él" or "tú" (command persons use their own names).</summary>
    public string PersonLabel() => P is null || Te is null ? "" :
        string.Join(" / ", P.Select(i => VerbGrammar.PersonsFor(Te)[i].Label.Split(" / ")[0]));
}

/// <summary>A story sentence shown elsewhere ("In the stories"), with the verbs that matter there.</summary>
public sealed record StoryLine(Story Story, StorySentence Sentence, IReadOnlySet<int> Highlight);

public static class VerbStories
{
    /// <summary>
    /// Sentences from all the stories that use the forms an item is about: for a verb page, that verb in that
    /// tense; for a group, its verbs in the persons it covers; for a regular pattern, regular verbs of that kind.
    /// The tense's own story comes first. Up to <paramref name="max"/>.
    /// </summary>
    public static List<StoryLine> LinesFor(VerbNode node, VerbBook book, int max = 6)
    {
        if (node.Tense is null || book.Stories.Count == 0) return [];
        var t = node.Tense;
        var keys = node.AllForms.Select(f => f.Key).ToHashSet();

        bool Matches(StoryToken tok)
        {
            if (!tok.IsVerb || tok.Te != t) return false;
            if (node.Kind == NodeKind.Verb) return tok.V == node.Verb!.Inf;
            if (node.Pattern is not null)
            {
                if (!book.ByInf.TryGetValue(tok.V!, out var v) || v.PatternIn(t) != node.Pattern) return false;
                return tok.P!.Any(p => v.Why(t, p) is null && VerbSettings.IsActive(t, p));
            }
            return tok.P!.Any(p => keys.Contains($"{tok.V}|{t}|{VerbGrammar.PersonsFor(t)[p].Id}"));
        }

        var lines = new List<StoryLine>();
        foreach (var story in book.Stories.OrderByDescending(s => s.Tense == t))
        {
            foreach (var s in story.Sentences)
            {
                var hit = Enumerable.Range(0, s.Tokens.Count).Where(i => Matches(s.Tokens[i])).ToHashSet();
                if (hit.Count > 0) lines.Add(new StoryLine(story, s, hit));
                if (lines.Count >= max) return lines;
            }
        }
        return lines;
    }

    /// <summary>Where a story verb lives in the tree: its verb item in that tense, or the regular pattern it follows.</summary>
    public static VerbNode? NodeFor(StoryToken tok, VerbNode tree, VerbBook book)
    {
        if (!tok.IsVerb) return null;
        VerbNode? found = null;
        void Walk(VerbNode n)
        {
            if (found is not null) return;
            if (n.Kind == NodeKind.Verb && n.Tense == tok.Te && n.Verb?.Inf == tok.V
                && n.Forms.Any(f => tok.P!.Contains(f.Person))) { found = n; return; }
            foreach (var c in n.Children) Walk(c);
        }
        Walk(tree);
        if (found is not null) return found;
        if (!book.ByInf.TryGetValue(tok.V!, out var v)) return null;
        var pattern = v.PatternIn(tok.Te!);
        void WalkPattern(VerbNode n)
        {
            if (found is not null) return;
            if (n.Tense == tok.Te && n.Pattern == pattern) { found = n; return; }
            foreach (var c in n.Children) WalkPattern(c);
        }
        WalkPattern(tree);
        return found;
    }
}
