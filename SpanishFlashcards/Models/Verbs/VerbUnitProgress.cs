using System.Text.Json.Serialization;

namespace SpanishFlashcards.Models.Verbs;

/// <summary>The three things each unit tracks: understanding the rule, making the forms, reading them.</summary>
public enum Meter { Rule, Make, Read }

public enum UnitStatus { New, Learning, Solid, Automatic, Known }

/// <summary>
/// What you've shown in one unit (saved with your progress; short names keep it small). Evidence only counts up:
/// the meters are worked out from it (see UnitProgress.Evaluate).
/// </summary>
public sealed class UnitRecord
{
    /// <summary>When you first worked on it.</summary>
    [JsonPropertyName("st")] public DateTime? Started { get; set; }

    /// <summary>The idea screen was shown (or not needed: the check showed you know some of it).</summary>
    [JsonPropertyName("id")] public bool IdeaSeen { get; set; }

    /// <summary>The check at the start of the unit was taken (or the placement check covered it).</summary>
    [JsonPropertyName("ck")] public bool Checked { get; set; }

    /// <summary>Skipped as already known (by a check or the placement check), not yet confirmed in a review.</summary>
    [JsonPropertyName("kn")] public bool Known { get; set; }

    /// <summary>Rule: made (or read) a form of a verb you hadn't met, and didn't fall for the trap.</summary>
    [JsonPropertyName("pr")] public bool Predicted { get; set; }

    /// <summary>Make, first step: built a form from its stem and ending.</summary>
    [JsonPropertyName("bu")] public bool Built { get; set; }

    /// <summary>Make: forms spelled right from a cue ("verb|tense|person", each once).</summary>
    [JsonPropertyName("mc")] public List<string> Cue { get; set; } = new();

    /// <summary>Make: right in context (a sentence with a gap, or English to Spanish).</summary>
    [JsonPropertyName("mx")] public int Context { get; set; }

    /// <summary>Read: forms decoded fully right (verb, who and when), each once.</summary>
    [JsonPropertyName("rd")] public List<string> Decoded { get; set; } = new();

    /// <summary>Read: meanings picked right for a form on its own.</summary>
    [JsonPropertyName("rm")] public int Meaning { get; set; }

    /// <summary>Read: meanings picked right in a sentence.</summary>
    [JsonPropertyName("rs")] public int Sentences { get; set; }

    /// <summary>Read: understood by ear at least once.</summary>
    [JsonPropertyName("ea")] public bool Heard { get; set; }

    /// <summary>The latest Make answers, oldest first: 1 = right and in time, 0 = anything else.</summary>
    [JsonPropertyName("mq")] public string MakeRecent { get; set; } = "";

    /// <summary>The latest Read answers, like <see cref="MakeRecent"/>.</summary>
    [JsonPropertyName("rq")] public string ReadRecent { get; set; } = "";

    /// <summary>Make shown by a check, the placement check or earlier progress: counts as the steps up to quick.</summary>
    [JsonPropertyName("mk")] public bool MakeCredit { get; set; }

    /// <summary>Read shown the same way.</summary>
    [JsonPropertyName("rk")] public bool ReadCredit { get; set; }

    /// <summary>When it first became solid (its reviews start then).</summary>
    [JsonPropertyName("so")] public DateTime? Solid { get; set; }

    /// <summary>When it first became automatic.</summary>
    [JsonPropertyName("au")] public DateTime? Automatic { get; set; }

    [JsonIgnore]
    public bool HasEvidence => IdeaSeen || Built || Cue.Count > 0 || Context > 0 || Decoded.Count > 0
                               || Meaning > 0 || Sentences > 0 || MakeCredit || ReadCredit;

    /// <summary>Adds an answer to a recent list (keeps the last 8).</summary>
    public static string Push(string recent, bool good)
    {
        var s = recent + (good ? "1" : "0");
        return s.Length > 8 ? s[^8..] : s;
    }
}

/// <summary>
/// The review memory for one review item (a unit's Make or Read, or one verb of a unit you memorise), scheduled
/// with FSRS like the word cards. Saved with your progress.
/// </summary>
public sealed class VerbMem
{
    [JsonPropertyName("s")] public double Stability { get; set; }
    [JsonPropertyName("d")] public double Difficulty { get; set; }

    /// <summary>When it's due (UTC; a local midnight).</summary>
    [JsonPropertyName("due")] public DateTime Due { get; set; }

    [JsonPropertyName("l")] public DateTime Last { get; set; }
    [JsonPropertyName("n")] public int Reps { get; set; }

    /// <summary>
    /// The unit was skipped as already known and this is its first review: a wrong answer sends the unit back to
    /// learning.
    /// </summary>
    [JsonPropertyName("c")] public bool Confirm { get; set; }
}

/// <summary>Where a unit stands: each meter's step, and whether it's solid or automatic.</summary>
public sealed record UnitState(int Rule, int Make, int Read, bool MakeRequired, bool Solid, bool Automatic, UnitStatus Status)
{
    public const int RuleMax = 2;
    public const int MeterMax = 4;
}

/// <summary>
/// The steps of each meter and what counts as done.
/// <para>Rule: 1 seen the idea, 2 tried it on a verb you hadn't met (and a trap).</para>
/// <para>Make: 1 built a form, 2 spelled forms from a cue (4 forms over 3 verbs), 3 right in context (2), 4 quick.</para>
/// <para>Read: 1 decoded forms (4), 2 picked their meaning (2), 3 understood them in sentences (2), 4 quick.</para>
/// <para>Solid: the rule, Read at 3, and Make at 3 when making is wanted. Automatic: those meters quick as well.</para>
/// A step a unit has nothing for (no sentences, no verb to build) counts as done.
/// </summary>
public static class UnitProgress
{
    /// <summary>This many answers in a row right and in time make a meter quick.</summary>
    public const int QuickRun = 4;

    public static int CueForms(VerbUnit u) => Math.Clamp(u.Active.Count(), 1, 4);
    public static int CueVerbs(VerbUnit u) => Math.Clamp(u.Verbs.Count, 1, 3);
    public static int DecodeForms(VerbUnit u) => Math.Clamp(u.Active.Count(i => i.Form.Tense != "near"), 1, 4);
    public static int ContextNeeded(VerbUnit u) => u.Kind switch { UnitKind.Choose => 4, UnitKind.Phrases => 3, _ => 2 };
    public static int SentencesNeeded(VerbUnit u) => u.Kind == UnitKind.Choose ? 3 : 2;
    public const int MeaningNeeded = 2;

    public static bool HasContext(VerbUnit u) => u.HasSentences || (u.HasForms && u.HasMeaning);

    /// <summary>The unit's meters from its record. <paramref name="laterMake"/>: read-first units now want making too.</summary>
    public static UnitState Evaluate(VerbUnit u, UnitRecord? r, bool laterMake)
    {
        var makeRequired = u.Goal == UnitGoal.Use || (u.Goal == UnitGoal.ReadFirst && laterMake);
        r ??= Empty;

        int make;
        if (r.MakeCredit) make = 3;
        else
        {
            var built = r.Built || r.Cue.Count > 0 || !u.HasBuild;
            var cue = !u.HasForms || (r.Cue.Count >= CueForms(u) && Verbs(r.Cue) >= CueVerbs(u));
            var context = !HasContext(u) || r.Context >= ContextNeeded(u);
            make = !built ? 0 : !cue ? 1 : !context ? 2 : 3;
        }
        if (make == 3 && Quick(r.MakeRecent)) make = 4;

        int read;
        if (r.ReadCredit) read = 3;
        else
        {
            var decoded = !u.HasDecode || r.Decoded.Count >= DecodeForms(u);
            var meaning = !u.HasForms || !u.HasMeaning || r.Meaning >= MeaningNeeded;
            var sentences = !u.HasReadSentences || r.Sentences >= SentencesNeeded(u);
            read = !decoded ? 0 : !meaning ? 1 : !sentences ? 2 : 3;
        }
        if (read == 3 && Quick(r.ReadRecent)) read = 4;

        var seen = r.IdeaSeen || r.Known || r.MakeCredit || r.ReadCredit;
        var predicted = r.Predicted || r.Known || !CanPredict(u) || (makeRequired ? r.MakeCredit : r.ReadCredit);
        var rule = !seen ? 0 : !predicted ? 1 : 2;

        var solid = rule == 2 && read >= 3 && (!makeRequired || make >= 3);
        var automatic = solid && read == 4 && (!makeRequired || make == 4);
        var started = r.Started is not null || r.HasEvidence;
        var status = !started ? UnitStatus.New
            : solid && r.Known ? UnitStatus.Known
            : automatic ? UnitStatus.Automatic
            : solid ? UnitStatus.Solid
            : UnitStatus.Learning;
        return new UnitState(rule, make, read, makeRequired, solid, automatic, status);
    }

    /// <summary>Units you only read have no rule to try: seeing the idea is enough.</summary>
    public static bool CanPredict(VerbUnit u) => u.HasPredict && u.Goal != UnitGoal.ReadOnly;

    private static readonly UnitRecord Empty = new();

    private static int Verbs(List<string> keys) => keys.Select(k => k.Split('|')[0]).Distinct().Count();

    public static bool Quick(string recent) => recent.Length >= QuickRun && recent[^QuickRun..].All(c => c == '1');

    public static string StatusLabel(UnitStatus s) => s switch
    {
        UnitStatus.New => "New",
        UnitStatus.Learning => "Learning",
        UnitStatus.Solid => "Solid",
        UnitStatus.Automatic => "Automatic",
        _ => "Known",
    };
}

/// <summary>
/// Review scheduling for verbs, with the same FSRS memory model as the word cards. A unit built on a rule has one
/// review item per meter ("u:pret-ar:m"): each review asks one of its forms, weak ones first. A unit of verbs to
/// memorise has one per verb and tense ("v:ser|pret:m"), since each verb is its own thing to remember.
/// Grades: wrong = forgot; a missing accent, or right but slow = hard; right in time = good.
/// </summary>
public static class VerbReviews
{
    public const int MaxDays = 365;

    private static string Tag(Meter m) => m == Meter.Make ? "m" : "r";

    public static string UnitKey(VerbUnit u, Meter m) => $"u:{u.Id}:{Tag(m)}";

    public static string VerbKey(string inf, string tense, Meter m) => $"v:{inf}|{tense}:{Tag(m)}";

    /// <summary>The meter of a review key.</summary>
    public static Meter MeterOf(string key) => key.EndsWith(":m", StringComparison.Ordinal) ? Meter.Make : Meter.Read;

    /// <summary>For a "v:" key: the verb and tense it reviews.</summary>
    public static (string Inf, string Tense)? VerbOf(string key)
    {
        if (!key.StartsWith("v:", StringComparison.Ordinal)) return null;
        var body = key[2..key.LastIndexOf(':')];
        var bar = body.IndexOf('|');
        return bar < 0 ? null : (body[..bar], body[(bar + 1)..]);
    }

    /// <summary>The review items of a unit for one meter.</summary>
    public static IEnumerable<string> KeysFor(VerbUnit u, Meter m)
    {
        if (!u.ReviewByVerb) return [UnitKey(u, m)];
        return u.Items
            .Select(i => (Inf: i.Verb ?? (i.Form.IsPattern ? null : i.Form.VerbKey), i.Form.Tense))
            .Where(x => x.Inf is not null)
            .Distinct()
            .Select(x => VerbKey(x.Inf!, x.Tense, m));
    }

    public static int Rating(Grade grade, bool fast) => grade switch
    {
        Grade.Wrong => 1,
        Grade.AccentSlip => 2,
        _ => fast ? 3 : 2,
    };

    /// <summary>Local midnight this many days from today, in UTC.</summary>
    public static DateTime DayStart(int days) => DateTime.Now.Date.AddDays(days).ToUniversalTime();

    /// <summary>
    /// A unit became solid (or was skipped as known): its review items start, the first one in a couple of days.
    /// Items that already exist are left alone.
    /// </summary>
    public static void Start(Dictionary<string, VerbMem> memory, VerbUnit u, bool make, bool read, bool confirm, DateTime nowUtc)
    {
        foreach (var m in new[] { Meter.Make, Meter.Read })
        {
            if ((m == Meter.Make && !make) || (m == Meter.Read && !read)) continue;
            foreach (var key in KeysFor(u, m))
            {
                if (memory.ContainsKey(key)) continue;
                var s = Fsrs.InitialStability(3);
                memory[key] = new VerbMem
                {
                    Stability = s,
                    Difficulty = Fsrs.InitialDifficulty(3),
                    Last = nowUtc,
                    Reps = 1,
                    Due = DayStart(Fsrs.NextInterval(s, MaxDays)),
                    Confirm = confirm,
                };
            }
        }
    }

    /// <summary>Updates a review item's memory after an answer (1 forgot, 2 hard, 3 good).</summary>
    public static void Rate(Dictionary<string, VerbMem> memory, string key, int rating, DateTime nowUtc)
    {
        if (!memory.TryGetValue(key, out var m))
        {
            m = new VerbMem
            {
                Stability = Fsrs.InitialStability(rating),
                Difficulty = Fsrs.InitialDifficulty(rating),
            };
            memory[key] = m;
        }
        else
        {
            var sameDay = m.Last.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
            if (sameDay) m.Stability = Fsrs.ShortTermStability(m.Stability, rating);
            else
            {
                var r = Fsrs.Retrievability(m.Stability, (nowUtc - m.Last).TotalDays);
                m.Stability = Fsrs.NextStability(m.Difficulty, m.Stability, r, rating);
            }
            m.Difficulty = Fsrs.NextDifficulty(m.Difficulty, rating);
        }
        m.Last = nowUtc;
        m.Reps++;
        m.Confirm = false;
        m.Due = rating == 1 ? DayStart(1) : DayStart(Fsrs.NextInterval(m.Stability, MaxDays));
    }

    public static bool IsDue(VerbMem m, DateTime nowUtc) => m.Due <= nowUtc;

    /// <summary>Review items due now, the most overdue first.</summary>
    public static List<string> DueKeys(IReadOnlyDictionary<string, VerbMem> memory, DateTime nowUtc) =>
        memory.Where(kv => IsDue(kv.Value, nowUtc)).OrderBy(kv => kv.Value.Due).Select(kv => kv.Key).ToList();
}
