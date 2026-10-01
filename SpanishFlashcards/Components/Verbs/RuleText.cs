using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>Renders the Rules mini-markup: **bold** and *Spanish* (italic, marked as Spanish for screen readers).</summary>
public static partial class RuleText
{
    public static MarkupString Render(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = Bold().Replace(html, "<strong>$1</strong>");
        html = Spanish().Replace(html, "<em lang=\"es\">$1</em>");
        return new MarkupString(html);
    }

    /// <summary>An example sentence: the verb, marked [like this], is highlighted.</summary>
    public static MarkupString RenderExample(string text)
    {
        var html = WebUtility.HtmlEncode(text);
        html = Bracketed().Replace(html, "<span class=\"ex-verb\">$1</span>");
        return new MarkupString(html);
    }

    [GeneratedRegex(@"\[(.+?)\]")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex Spanish();
}
