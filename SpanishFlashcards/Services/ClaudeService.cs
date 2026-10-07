using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpanishFlashcards.Models.Reading;

namespace SpanishFlashcards.Services;

/// <summary>What a translation request gave: the translation, or what went wrong (and whether the key is the problem).</summary>
public sealed record TranslateOutcome(TextTranslation? Translation, string? Error, bool KeyProblem = false, double Cost = 0);

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
        var request = new MessagesRequest(model, maxTokens, SystemPrompt, [new Message("user", body.ToString())]);

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
            return new(null, "Couldn't reach Claude. Check your connection and try again.");
        }

        if ((int)status is < 200 or > 299) return ErrorFor(status, json);

        MessagesResponse? answer;
        try { answer = JsonSerializer.Deserialize<MessagesResponse>(json, Json); }
        catch (JsonException) { answer = null; }
        if (answer is null) return new(null, "Claude's answer couldn't be read. Try again.");

        var cost = CostOf(model, answer.Usage);
        if (answer.StopReason == "max_tokens")
            return new(null, "This text is too long to translate in one go.", Cost: cost);
        if (answer.StopReason == "refusal")
            return new(null, "Claude didn't translate this text.", Cost: cost);

        var reply = string.Concat(answer.Content?.Where(c => c.Type == "text").Select(c => c.Text) ?? []);
        var translation = Read(reply, lines, model);
        return translation is null
            ? new(null, "Claude's answer couldn't be read. Try again.", Cost: cost)
            : new(translation, null, Cost: cost);
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
            _ => new(null, $"Claude couldn't translate it ({message ?? type ?? $"error {(int)status}"}).")
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
