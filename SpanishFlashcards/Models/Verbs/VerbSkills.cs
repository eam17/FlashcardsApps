using System.Text;
using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>
/// One tracked form: a verb (or a pattern such as "~ar") in one tense and person.
/// Key format: "verb|tense|person", e.g. "tener|pret|yo" or "~ar|pres|nos".
/// </summary>
public sealed record FormRef(string VerbKey, string Tense, int Person)
{
    public string Key => $"{VerbKey}|{Tense}|{PersonId}";

    public bool IsPattern => VerbKey.StartsWith('~');

    /// <summary>"ar", "er-ir"… for pattern forms.</summary>
    public string? Pattern => IsPattern ? VerbKey[1..] : null;

    public PersonInfo PersonInfo => VerbGrammar.PersonsFor(Tense)[Person];

    public string PersonId => PersonInfo.Id;

    public static string PatternKey(string pattern) => "~" + pattern;
}

/// <summary>How well you know one form. Saved in localStorage (short names keep it small).</summary>
public sealed class VerbSkill
{
    /// <summary>0 (new) to 1 (solid).</summary>
    [JsonPropertyName("s")] public double Strength { get; set; }

    /// <summary>When it should be practised next (UTC).</summary>
    [JsonPropertyName("d")] public DateTime Due { get; set; }

    /// <summary>Wrong answers.</summary>
    [JsonPropertyName("m")] public int Mistakes { get; set; }

    /// <summary>Right except for a missing accent (counts as half a mistake).</summary>
    [JsonPropertyName("a")] public int AccentSlips { get; set; }

    /// <summary>Times answered.</summary>
    [JsonPropertyName("n")] public int Reps { get; set; }

    /// <summary>Last answered (UTC).</summary>
    [JsonPropertyName("l")] public DateTime? Last { get; set; }

    [JsonIgnore] public double MistakeScore => Mistakes + 0.5 * AccentSlips;
}

public enum Grade { Right, AccentSlip, Wrong }

/// <summary>
/// Scheduling for verb forms. Strength goes up with right answers (more for typed answers than for
/// multiple choice) and the next practice date moves further out as it grows. A wrong answer loses
/// 60% of the strength; a missing accent loses half as much (30%). Both bring the form back soon.
/// Answering a form before it's due still counts, but only gives a small boost.
/// </summary>
public static class VerbSrs
{
    /// <summary>Below this you get multiple choice; from here on you type the answer.</summary>
    public const double TypingFrom = 0.4;

    /// <summary>Multiple choice alone can't take a form beyond this: typing is needed to make it strong.</summary>
    public const double ChoiceCap = 0.45;

    /// <summary>Counts as "strong" in summaries.</summary>
    public const double Strong = 0.8;

    private const double ChoiceGain = 0.3;
    private const double TypedGain = 0.4;
    private const double EarlyFactor = 0.3;   // answering before it's due
    private const double WrongKeeps = 0.4;    // a wrong answer keeps 40% of the strength
    private const double AccentKeeps = 0.7;   // a missing accent keeps 70% (half the loss of a wrong answer)

    public static bool IsSeen(VerbSkill? s) => s is { Reps: > 0 };

    public static bool IsDue(VerbSkill? s, DateTime nowUtc) => s is { Reps: > 0 } && s.Due <= nowUtc;

    public static bool UsesTyping(VerbSkill? s) => s is not null && s.Strength >= TypingFrom;

    public static VerbSkill Apply(VerbSkill? before, Grade grade, bool typed, DateTime nowUtc)
    {
        var s = before ?? new VerbSkill();
        var next = new VerbSkill
        {
            Strength = s.Strength,
            Due = s.Due,
            Mistakes = s.Mistakes,
            AccentSlips = s.AccentSlips,
            Reps = s.Reps + 1,
            Last = nowUtc,
        };
        var counts = !IsSeen(s) || s.Due <= nowUtc;

        switch (grade)
        {
            case Grade.Right:
                var gain = (typed ? TypedGain : ChoiceGain) * (counts ? 1 : EarlyFactor);
                next.Strength = s.Strength + (1 - s.Strength) * gain;
                if (!typed) next.Strength = Math.Min(next.Strength, Math.Max(s.Strength, ChoiceCap));
                var due = NextDue(next.Strength, nowUtc);
                next.Due = counts || due > s.Due ? due : s.Due;
                break;
            case Grade.AccentSlip:
                next.Strength = s.Strength * AccentKeeps;
                next.AccentSlips++;
                next.Due = nowUtc.AddMinutes(5);
                break;
            default:
                next.Strength = s.Strength * WrongKeeps;
                next.Mistakes++;
                next.Due = nowUtc.AddMinutes(5);
                break;
        }
        return next;
    }

    /// <summary>
    /// Weak forms come back in a few minutes; then 1 day, 2, 4, 8, 16 and 30 days as strength grows.
    /// Day-based dates are local midnights, so "tomorrow" means any time tomorrow.
    /// </summary>
    public static DateTime NextDue(double strength, DateTime nowUtc)
    {
        if (strength < 0.2) return nowUtc.AddMinutes(5);
        var days = strength switch
        {
            < 0.4 => 1,
            < 0.55 => 2,
            < 0.7 => 4,
            < 0.8 => 8,
            < 0.9 => 16,
            _ => 30,
        };
        return DateTime.Now.Date.AddDays(days).ToUniversalTime();
    }
}

/// <summary>Checks a typed answer. A missing accent is "almost right" (half penalty).</summary>
public static class AnswerCheck
{
    private static readonly string[] SubjectPronouns =
        ["yo", "tú", "tu", "él", "el", "ella", "usted", "ud.", "nosotros", "nosotras", "vosotros", "vosotras", "ellos", "ellas", "ustedes", "uds."];

    /// <summary>Lower case, single spaces, no trailing punctuation, typographic apostrophes fixed.</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim().ToLowerInvariant().Replace('’', '\'');
        t = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return t.Trim('.', '!', '¡', '?', '¿', ',', ';', ' ');
    }

    /// <summary>Removes a leading subject pronoun ("yo tengo" → "tengo") and, for negative commands, "no".</summary>
    private static string StripExtras(string typed, bool negative)
    {
        var t = typed;
        if (negative && t.StartsWith("no ", StringComparison.Ordinal)) t = t[3..];
        foreach (var p in SubjectPronouns)
        {
            if (t.StartsWith(p + " ", StringComparison.Ordinal) && t.Length > p.Length + 1)
            {
                t = t[(p.Length + 1)..];
                break;
            }
        }
        if (negative && t.StartsWith("no ", StringComparison.Ordinal)) t = t[3..];
        return t;
    }

    /// <summary>Grades a typed answer. Returns which accepted answer it matched (or the main one).</summary>
    public static (Grade Grade, string Matched) Check(string? typed, IReadOnlyList<string> answers, bool negative)
    {
        var raw = Normalize(typed);
        var candidates = new[] { raw, StripExtras(raw, negative) }.Distinct().ToList();
        foreach (var a in answers)
        {
            var n = Normalize(a);
            if (candidates.Contains(n)) return (Grade.Right, a);
        }
        foreach (var a in answers)
        {
            var n = Normalize(a);
            if (candidates.Any(c => OnlyMissingAccents(c, n))) return (Grade.AccentSlip, a);
        }
        return (Grade.Wrong, answers.Count > 0 ? answers[0] : "");
    }

    /// <summary>True when the typed text is the answer with one or more accents left off (and nothing else wrong).</summary>
    public static bool OnlyMissingAccents(string typed, string answer)
    {
        if (typed.Length != answer.Length || typed == answer) return false;
        for (var i = 0; i < typed.Length; i++)
        {
            var t = typed[i];
            var a = answer[i];
            if (t == a) continue;
            if (StripAccent(a) == t && IsAccented(a)) continue; // á typed as a: missing accent
            return false;
        }
        return true;
    }

    private static bool IsAccented(char c) => "áéíóúü".IndexOf(c) >= 0;

    public static char StripAccent(char c) => c switch
    {
        'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u', 'ü' => 'u',
        _ => c,
    };

    public static string StripAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(StripAccent(c));
        return sb.ToString();
    }
}
