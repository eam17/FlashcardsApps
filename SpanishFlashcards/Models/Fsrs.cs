using System.Globalization;

namespace SpanishFlashcards.Models;

/// <summary>
/// FSRS-6, the memory model behind the word schedule (the same one Anki uses). Each word has a
/// <em>stability</em> (how many days until the chance of remembering it falls to 90%) and a
/// <em>difficulty</em> (1 to 10: how slowly its stability grows). Each answer updates both, and the next
/// review is planned for the day the chance of remembering it falls to your target (90% by default).
/// <para>Formulas and default parameters follow the reference implementation (open-spaced-repetition/py-fsrs).
/// The defaults were fitted to millions of reviews; Settings can take parameters fitted to your own history.</para>
/// <para>Ratings: 1 = forgot, 2 = hard, 3 = good, 4 = easy.</para>
/// </summary>
public static class Fsrs
{
    public static readonly double[] DefaultParameters =
    [
        0.212, 1.2931, 2.3065, 8.2956, 6.4133, 0.8334, 3.0194, 0.001, 1.8722, 0.1666, 0.796,
        1.4835, 0.0614, 0.2629, 1.6483, 0.6014, 1.8729, 0.5425, 0.0912, 0.0658, 0.1542,
    ];

    private static readonly double[] Lower =
    [
        0.001, 0.001, 0.001, 0.001, 1.0, 0.001, 0.001, 0.001, 0.0, 0.0, 0.001,
        0.001, 0.001, 0.001, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.1,
    ];

    private static readonly double[] Upper =
    [
        100.0, 100.0, 100.0, 100.0, 10.0, 4.0, 4.0, 0.75, 4.5, 0.8, 3.5,
        5.0, 0.25, 0.9, 4.0, 1.0, 6.0, 2.0, 2.0, 0.8, 0.8,
    ];

    public const double DefaultRetention = 0.9;
    public const double MinStability = 0.001;
    private const double MinDifficulty = 1.0;
    private const double MaxDifficulty = 10.0;

    private static double[] w = DefaultParameters;

    /// <summary>The chance of remembering a word that its next review is planned for (0.9 = 90%).</summary>
    public static double Retention { get; private set; } = DefaultRetention;

    public static IReadOnlyList<double> Parameters => w;

    public static bool UsingDefaults => ReferenceEquals(w, DefaultParameters);

    /// <summary>Your parameters (null or invalid → the defaults) and target retention (kept between 70% and 97%).</summary>
    public static void Configure(IReadOnlyList<double>? parameters, double retention)
    {
        w = parameters is not null && Validate(parameters) is null ? parameters.ToArray() : DefaultParameters;
        Retention = retention is >= 0.7 and <= 0.97 ? retention : DefaultRetention;
    }

    /// <summary>Null when the list is 21 numbers within the allowed ranges; otherwise what's wrong.</summary>
    public static string? Validate(IReadOnlyList<double> p)
    {
        if (p.Count != DefaultParameters.Length) return $"Expected {DefaultParameters.Length} numbers, got {p.Count}.";
        for (var i = 0; i < p.Count; i++)
        {
            if (double.IsNaN(p[i]) || p[i] < Lower[i] || p[i] > Upper[i])
                return $"Number {i + 1} ({p[i].ToString(CultureInfo.InvariantCulture)}) is outside {Lower[i].ToString(CultureInfo.InvariantCulture)} to {Upper[i].ToString(CultureInfo.InvariantCulture)}.";
        }
        return null;
    }

    /// <summary>Reads parameters pasted as numbers separated by commas or spaces (brackets are ignored).</summary>
    public static bool TryParse(string? text, out double[] parameters, out string? error)
    {
        parameters = [];
        var parts = (text ?? "").Split([',', ' ', '\n', '\r', '\t', '[', ']', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        var list = new List<double>();
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                error = $"\"{part}\" isn't a number.";
                return false;
            }
            list.Add(v);
        }
        error = Validate(list);
        if (error is not null) return false;
        parameters = list.ToArray();
        return true;
    }

    public static string Format(IReadOnlyList<double> p) =>
        string.Join(", ", p.Select(v => Math.Round(v, 4).ToString(CultureInfo.InvariantCulture)));

    private static double Decay => -w[20];
    private static double Factor => Math.Pow(0.9, 1 / Decay) - 1;

    /// <summary>The chance of remembering a word with this stability after this many days.</summary>
    public static double Retrievability(double stability, double elapsedDays) =>
        Math.Pow(1 + Factor * Math.Max(0, elapsedDays) / Math.Max(stability, MinStability), Decay);

    public static double InitialStability(int rating) => Math.Max(w[rating - 1], MinStability);

    public static double InitialDifficulty(int rating) => ClampDifficulty(RawInitialDifficulty(rating));

    private static double RawInitialDifficulty(int rating) => w[4] - Math.Exp(w[5] * (rating - 1)) + 1;

    public static double NextDifficulty(double difficulty, int rating)
    {
        var delta = -(w[6] * (rating - 3));
        var damped = difficulty + (10.0 - difficulty) * delta / 9.0;
        // Mean reversion toward the "easy" starting difficulty: a word can't get stuck as hard forever.
        return ClampDifficulty(w[7] * RawInitialDifficulty(4) + (1 - w[7]) * damped);
    }

    /// <summary>Another answer on the same day (learning steps, the check-in).</summary>
    public static double ShortTermStability(double stability, int rating)
    {
        var increase = Math.Exp(w[17] * (rating - 3 + w[18])) * Math.Pow(stability, -w[19]);
        if (rating >= 2) increase = Math.Max(increase, 1.0);
        return Math.Max(stability * increase, MinStability);
    }

    /// <summary>An answer on a later day, when the chance of remembering was <paramref name="retrievability"/>.</summary>
    public static double NextStability(double difficulty, double stability, double retrievability, int rating) =>
        Math.Max(rating == 1
            ? ForgetStability(difficulty, stability, retrievability)
            : RecallStability(difficulty, stability, retrievability, rating), MinStability);

    private static double ForgetStability(double d, double s, double r)
    {
        var longTerm = w[11] * Math.Pow(d, -w[12]) * (Math.Pow(s + 1, w[13]) - 1) * Math.Exp((1 - r) * w[14]);
        var shortTerm = s / Math.Exp(w[17] * w[18]);
        return Math.Min(longTerm, shortTerm);
    }

    private static double RecallStability(double d, double s, double r, int rating)
    {
        var hardPenalty = rating == 2 ? w[15] : 1;
        var easyBonus = rating == 4 ? w[16] : 1;
        return s * (1 + Math.Exp(w[8]) * (11 - d) * Math.Pow(s, -w[9]) * (Math.Exp((1 - r) * w[10]) - 1)
                    * hardPenalty * easyBonus);
    }

    /// <summary>Days until the chance of remembering falls to <see cref="Retention"/> (at least 1).</summary>
    public static int NextInterval(double stability, int maxDays)
    {
        var days = stability / Factor * (Math.Pow(Retention, 1 / Decay) - 1);
        return (int)Math.Clamp(Math.Round(days, MidpointRounding.ToEven), 1, maxDays);
    }

    /// <summary>
    /// Memory for a word that was scheduled without FSRS (older progress, imports): a gap that was planned for
    /// 90% recall becomes its stability, and its ease becomes a difficulty (as Anki does when switching).
    /// </summary>
    public static (double Stability, double Difficulty) FromGap(int intervalDays, double ease)
    {
        const double oldRetention = 0.9;
        var s = Math.Max(intervalDays, MinStability) * Factor / (Math.Pow(oldRetention, 1 / Decay) - 1);
        var growth = Math.Exp(w[8]) * Math.Pow(s, -w[9]) * (Math.Exp((1 - oldRetention) * w[10]) - 1);
        var d = 11 - (Math.Max(ease, 1.3) - 1) / growth;
        return (s, ClampDifficulty(d));
    }

    private static double ClampDifficulty(double d) =>
        double.IsNaN(d) ? 5 : Math.Clamp(d, MinDifficulty, MaxDifficulty);
}
