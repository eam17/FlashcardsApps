using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpanishFlashcards.Models.Reading;

namespace SpanishFlashcards.Services;

/// <summary>What a translation request gave: the translation, or what went wrong (and whether the key is the problem).</summary>
public sealed record TranslateOutcome(TextTranslation? Translation, string? Error, bool KeyProblem = false, double Cost = 0);

/// <summary>What a story request gave: the story as a text (with its translation), or what went wrong.</summary>
public sealed record StoryOutcome(SavedText? Text, string? Error, bool KeyProblem = false, double Cost = 0);

/// <summary>
/// Translates a Read text with Claude (the Messages API), called straight from the browser with your own key
/// (Settings → Translation with Claude). The answer is line by line, so it can sit under each line of the text,
/// with notes on slang, idioms and grammar.
/// </summary>
public sealed partial class ClaudeService(HttpClient http)
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";

    public const string DefaultModel = "claude-haiku-5-5";

    /// <summary>The models offered in Settings, with their prices in dollars per million tokens (in, out).</summary>
    public static readonly IReadOnlyList<(string Id, string Name, double In, double Out)> Models =
    [
        ("claude-haiku-5-5", "Claude Haiku 5.5", 0.10, 0.50),
        ("claude-sonnet-5-5", "Claude Sonnet 5.5", 2.0, 10.0),
    ];

    public static string NameOf(string model) => Models.FirstOrDefault(m => m.Id == model).Name ?? model;

    /// <summary>The model to use: the one picked in Settings if it's one of <see cref="Models"/>, else the default.</summary>
    public static string Resolve(string? model) => Models.Any(m => m.Id == model) ? model! : DefaultModel;

    private const string SystemPrompt = """
        You help someone who is learning Spanish understand a Spanish text, usually the lyrics of a song they like.

        Translate it into natural, accurate English, line by line. Stay close enough to the Spanish that a learner can match the words: no poetic rewrites and no added meaning. Each English line goes with the same line number as its Spanish line. Translate repeated lines again. Skip empty lines. If a line is already in English, copy it as it is.

        Then add the notes a learner would want: slang, idioms, regional words, cultural references, wordplay, and grammar worth noticing (for example a subjunctive, or why a verb is in the preterite rather than the imperfect). Each note quotes the exact Spanish words from the text and explains them in at most two short sentences of plain English. Give between 3 and 12 notes, the most useful first. Also give a summary of what the text is about in one or two sentences.

        Write plain English and don't use em dashes.

        Reply with only this JSON and nothing before or after it:
        {"summary": "...", "lines": [{"n": 1, "en": "..."}], "notes": [{"es": "...", "en": "..."}]}
        """;

    public async Task<TranslateOutcome> TranslateAsync(SavedText text, string key, string model)
    {
        var lines = text.Text.Replace("\r", "").Split('\n');
        var body = new StringBuilder();
        body.Append(text.Song is { } song
            ? $"Song: \"{song.Track}\" by {song.Artist}\n"
            : $"Title: {text.Title}\n");
        body.Append("\nThe text, one numbered line per line:\n\n");
        for (var i = 0; i < lines.Length; i++)
            body.Append(i + 1).Append(": ").Append(lines[i].Trim()).Append('\n');

        // Enough room for the English (about as long as the Spanish) and the notes.
        var maxTokens = Math.Clamp(text.Text.Length / 2 + 2500, 3000, 16000);
        var (reply, failure, cost) = await AskAsync(key, model, SystemPrompt, body.ToString(), maxTokens,
            "This text is too long to translate in one go.", "Claude didn't translate this text.");
        if (failure is not null) return failure;

        var translation = Read(reply!, lines, model);
        return translation is null
            ? new(null, "Claude's answer couldn't be read. Try again.", Cost: cost)
            : new(translation, null, Cost: cost);
    }

    /// <summary>
    /// One request to the Messages API. Returns Claude's text, or a failure to show (with what it cost, if
    /// Claude got as far as answering).
    /// </summary>
    private async Task<(string? Reply, TranslateOutcome? Failure, double Cost)> AskAsync(
        string key, string model, string system, string user, int maxTokens, string tooLong, string refused)
    {
        var request = new MessagesRequest(model, maxTokens, system, [new Message("user", user)]);

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(request, options: Json) };
        req.Headers.Add("x-api-key", key.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");
        // Claude's API only answers a web page directly when asked to with this header (the key is then visible
        // to the page, which is fine for an app only you use, with your own key).
        req.Headers.Add("anthropic-dangerous-direct-browser-access", "true");

        HttpStatusCode status;
        string json;
        try
        {
            using var res = await http.SendAsync(req);
            status = res.StatusCode;
            json = await res.Content.ReadAsStringAsync();
        }
        catch (Exception)
        {
            return (null, new TranslateOutcome(null, "Couldn't reach Claude. Check your connection and try again."), 0);
        }

        if ((int)status is < 200 or > 299) return (null, ErrorFor(status, json), 0);

        MessagesResponse? answer;
        try { answer = JsonSerializer.Deserialize<MessagesResponse>(json, Json); }
        catch (JsonException) { answer = null; }
        if (answer is null) return (null, new TranslateOutcome(null, "Claude's answer couldn't be read. Try again."), 0);

        var cost = CostOf(model, answer.Usage);
        if (answer.StopReason == "max_tokens") return (null, new TranslateOutcome(null, tooLong, Cost: cost), cost);
        if (answer.StopReason == "refusal") return (null, new TranslateOutcome(null, refused, Cost: cost), cost);

        var reply = string.Concat(answer.Content?.Where(c => c.Type == "text").Select(c => c.Text) ?? []);
        return (reply, null, cost);
    }

    // ------------------------------------------------------------------ a story with your words

    private const string StoryPrompt = """
        You write short reading practice in Spanish for an adult learning Spanish.

        Write one short story or scene, 6 to 10 sentences and about 80 to 140 words, that uses every target word at least once, in whatever form fits (conjugated, plural, feminine). Use each target word in the meaning given. Keep everything else easy: mostly very common words and the words they already know; the present, preterite, imperfect and "ir a" are all fine. Make it a real little story with something happening, about everyday adult life, not a list of unrelated sentences. Put each sentence on its own line, with a natural English translation of that sentence.

        For each target word, give the form you used and its meaning in the story. Give the story a short Spanish title. Write plain English and don't use em dashes.

        Reply with only this JSON and nothing before or after it:
        {"title": "...", "lines": [{"es": "...", "en": "..."}], "words": [{"word": "...", "form": "...", "en": "..."}]}
        """;

    /// <param name="targets">The words to practise: Spanish and English.</param>
    /// <param name="known">Words they know well (to lean on), Spanish only.</param>
    public async Task<StoryOutcome> WriteStoryAsync(IReadOnlyList<(string Es, string En)> targets, IReadOnlyList<string> known,
                                                    string? topic, bool vosotros, string key, string model)
    {
        var user = new StringBuilder();
        user.Append("Target words (the ones they're learning now):\n");
        foreach (var (es, en) in targets) user.Append("- ").Append(es).Append(" (").Append(en).Append(")\n");
        if (known.Count > 0) user.Append("\nWords they know well: ").Append(string.Join(", ", known)).Append('\n');
        if (!string.IsNullOrWhiteSpace(topic)) user.Append("\nWhat the story should be about: ").Append(topic.Trim()).Append('\n');
        user.Append(vosotros ? "\nSpain or Latin American Spanish are both fine.\n" : "\nUse Latin American Spanish: ustedes, not vosotros.\n");

        var (reply, failure, cost) = await AskAsync(key, model, StoryPrompt, user.ToString(), 3000,
            "The story came out too long. Try again.", "Claude didn't write this story. Try a different topic.");
        if (failure is not null) return new(null, failure.Error, failure.KeyProblem, failure.Cost);

        var start = reply!.IndexOf('{');
        var end = reply.LastIndexOf('}');
        StoryJson? r = null;
        if (start >= 0 && end > start)
        {
            try { r = JsonSerializer.Deserialize<StoryJson>(reply[start..(end + 1)], Json); }
            catch (JsonException) { r = null; }
        }
        var lines = (r?.Lines ?? []).Where(l => !string.IsNullOrWhiteSpace(l.Es)).ToList();
        if (lines.Count == 0) return new(null, "Claude's answer couldn't be read. Try again.", Cost: cost);

        var title = string.IsNullOrWhiteSpace(r!.Title) ? "Una historia" : Clean(r.Title);
        var text = new SavedText
        {
            Title = title.Length > 80 ? title[..80] : title,
            Text = string.Join('\n', lines.Select(l => l.Es!.Trim().Replace("\n", " "))),
            Translation = new TextTranslation
            {
                Lines = lines.Select(l => Clean(l.En ?? "")).ToList(),
                Notes = (r.Words ?? [])
                    .Select(w => (Es: string.IsNullOrWhiteSpace(w.Form) ? w.Word : w.Form, w.En))
                    .Where(w => !string.IsNullOrWhiteSpace(w.Es) && !string.IsNullOrWhiteSpace(w.En))
                    .Select(w => new TranslationNote { Es = w.Es!.Trim(), En = Clean(w.En!) })
                    .ToList(),
                Model = model,
                Made = DateTime.UtcNow,
            },
        };
        return new(text, null, Cost: cost);
    }

    /// <summary>The JSON in Claude's reply, turned into one English line per line of the text.</summary>
    private static TextTranslation? Read(string reply, string[] spanish, string model)
    {
        var lineCount = spanish.Length;
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        ReplyJson? r;
        try { r = JsonSerializer.Deserialize<ReplyJson>(reply[start..(end + 1)], Json); }
        catch (JsonException) { return null; }
        if (r?.Lines is not { Count: > 0 }) return null;

        var english = Enumerable.Repeat("", lineCount).ToList();
        foreach (var l in r.Lines)
        {
            // Only under lines that have Spanish on them (a blank line stays a gap between verses).
            if (l.N >= 1 && l.N <= lineCount && !string.IsNullOrWhiteSpace(l.En) && !string.IsNullOrWhiteSpace(spanish[l.N - 1]))
                english[l.N - 1] = Clean(l.En);
        }
        if (english.All(e => e.Length == 0)) return null;

        return new TextTranslation
        {
            Lines = english,
            Notes = (r.Notes ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n.Es) && !string.IsNullOrWhiteSpace(n.En))
                .Select(n => new TranslationNote { Es = n.Es!.Trim(), En = Clean(n.En!) })
                .Take(15)
                .ToList(),
            Summary = Clean(r.Summary ?? ""),
            Model = model,
            Made = DateTime.UtcNow,
        };
    }

    /// <summary>No em dashes in what the app shows.</summary>
    private static string Clean(string s) => EmDash().Replace(s.Trim(), ", ");

    [System.Text.RegularExpressions.GeneratedRegex(@"\s*\u2014\s*")]
    private static partial System.Text.RegularExpressions.Regex EmDash();

    private static double CostOf(string model, Usage? usage)
    {
        if (usage is null) return 0;
        var m = Models.FirstOrDefault(x => x.Id == model);
        return (usage.InputTokens * m.In + usage.OutputTokens * m.Out) / 1_000_000;
    }

    private static TranslateOutcome ErrorFor(HttpStatusCode status, string json)
    {
        string? type = null, message = null;
        try
        {
            var e = JsonSerializer.Deserialize<ErrorResponse>(json, Json);
            type = e?.Error?.Type;
            message = e?.Error?.Message;
        }
        catch (JsonException) { }

        return (int)status switch
        {
            401 => new(null, "Claude didn't accept the key. Check it in Settings.", KeyProblem: true),
            403 => new(null, "This key isn't allowed to use Claude's API. Check it in the Claude Console.", KeyProblem: true),
            404 => new(null, "That model isn't available to your key. Pick the other one in Settings.", KeyProblem: true),
            400 when message?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true =>
                new(null, "Your Claude credit has run out. Add credit in the Claude Console.", KeyProblem: true),
            429 => new(null, "Too many requests, or your spending limit was reached. Try again in a minute.", KeyProblem: false),
            500 or 529 or 503 => new(null, "Claude is busy right now. Try again in a minute."),
            _ => new(null, $"Claude couldn't do this ({message ?? type ?? $"error {(int)status}"}).")
        };
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ---- the Messages API shapes (only what's used) ----

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] List<Message> Messages);

    private sealed class MessagesResponse
    {
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        public Usage? Usage { get; set; }
    }

    private sealed class ContentBlock
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    private sealed class ErrorResponse
    {
        public ErrorBody? Error { get; set; }
    }

    private sealed class ErrorBody
    {
        public string? Type { get; set; }
        public string? Message { get; set; }
    }

    // ---- the JSON Claude is asked to reply with ----

    private sealed class StoryJson
    {
        public string? Title { get; set; }
        public List<StoryLine>? Lines { get; set; }
        public List<StoryWord>? Words { get; set; }
    }

    private sealed class StoryLine
    {
        public string? Es { get; set; }
        public string? En { get; set; }
    }

    private sealed class StoryWord
    {
        public string? Word { get; set; }
        public string? Form { get; set; }
        public string? En { get; set; }
    }

    private sealed class ReplyJson
    {
        public string? Summary { get; set; }
        public List<ReplyLine>? Lines { get; set; }
        public List<ReplyNote>? Notes { get; set; }
    }

    private sealed class ReplyLine
    {
        public int N { get; set; }
        public string? En { get; set; }
    }

    private sealed class ReplyNote
    {
        public string? Es { get; set; }
        public string? En { get; set; }
    }
}
