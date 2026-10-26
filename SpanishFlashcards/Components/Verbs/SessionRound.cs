using SpanishFlashcards.Components.Games;
using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// A verb session and where you are in it. Continue puts one together: reviews that are due (both making and
/// reading, across tenses), then the current unit (its check, the idea if you need it, then a step up its lowest
/// meter, moving on to the next unit once it's solid), then the rest of the reviews mixed with real sentences and a
/// few quick questions on a unit that's solid but not automatic yet. Other modes: reviews only (the Cards tab),
/// one unit (Practise this unit), a unit's check (I know this), and the placement check.
/// <para>Every answer updates the form's strength (used by Explore), the unit's evidence, and for a review its
/// review memory. Like practice rounds, a session lives outside its screen, so you can leave and come back.</para>
/// </summary>
public sealed class SessionRound
{
    public enum Mode { Continue, Reviews, Unit, Check, Placement }

    public enum StepKind { Question, Idea, CheckIntro, UnitDone, End }

    public sealed class Step
    {
        public required StepKind Kind { get; init; }
        public SessionQuestion? Question { get; init; }
        public VerbUnit? Unit { get; init; }

        /// <summary>UnitDone: skipped as already known (rather than learned now).</summary>
        public bool Known { get; init; }
    }

    private enum SlotKind { Review, UnitBlock, Real, Quick }

    private sealed record Slot(SlotKind Kind, string? Key = null);

    private readonly VerbKit kit;
    private readonly Progress progress;
    private readonly IReadOnlyList<TextSpot> texts;
    private readonly bool canSpeak;
    private readonly Random rng = Random.Shared;
    private readonly Dictionary<string, UnitState> startStates;

    private readonly List<Slot> slots = new();
    private int slotIndex;
    private bool inUnitBlock;
    private int unitLeft;
    private bool lotsDue;

    /// <summary>Steps queued ahead (a check's questions, a unit-done note), served before anything else.</summary>
    private readonly List<Step> pending = new();

    /// <summary>Missed questions, asked once more when this many answers have been given.</summary>
    private readonly List<(int At, SessionQuestion Q)> retries = new();

    private string? lastVerb;
    private Meter lastMeter = Meter.Read;

    // The check at the start of a unit.
    private VerbUnit? checkUnit;
    private int checkTotal;
    private readonly List<(SessionQuestion Q, Grade G, bool Fast)> checkAnswers = new();

    // Trying the rule on a verb you haven't met.
    private VerbUnit? predictUnit;
    private int predictTotal;
    private readonly List<Grade> predictAnswers = new();
    private int sincePredict = 10;

    // The placement check.
    private VerbUnit? placementUnit;
    private int placementTotal;
    private readonly List<(SessionQuestion Q, Grade G)> placementAnswers = new();
    private int placementIndex;
    private int placementMisses;
    private bool placementJumped;
    private bool placementStopped;

    public SessionRound(Mode mode, VerbKit kit, Progress progress, int minutes, VerbUnit? unit = null,
                        IReadOnlyList<TextSpot>? texts = null, bool canSpeak = false)
    {
        Kind = mode;
        Target = unit;
        this.kit = kit;
        this.progress = progress;
        this.texts = texts ?? [];
        this.canSpeak = canSpeak;
        Minutes = Math.Clamp(minutes, 5, 30);
        startStates = VerbCourse.States(kit, progress);
        Plan();
        Advance();
    }

    public Mode Kind { get; }

    /// <summary>The unit of a one-unit session or check.</summary>
    public VerbUnit? Target { get; }

    public int Minutes { get; }

    public Step Current { get; private set; } = new() { Kind = StepKind.End };

    public SessionQuestion? Question => Current.Question;

    /// <summary>About how many questions the session has (for the progress bar).</summary>
    public int Planned { get; private set; }

    public int AnsweredCount { get; private set; }
    public int RightCount { get; private set; }
    public bool Done => Current.Kind == StepKind.End;
    public bool InProgress => !Done && (AnsweredCount > 0 || Current.Kind != StepKind.Question);
    public DateTime LastActive { get; private set; } = DateTime.UtcNow;

    /// <summary>Placement: units checked, and units skipped as known.</summary>
    public int PlacementChecked { get; private set; }
    public int PlacementKnown { get; private set; }

    // ------------------------------------------------------------------ the answer on screen

    public bool Answered { get; private set; }
    public Grade Grade { get; private set; }
    public string? Given { get; private set; }

    /// <summary>The accepted answer the reply matched (or the main answer after a mistake).</summary>
    public string? Matched { get; private set; }

    /// <summary>Milliseconds the answer took.</summary>
    public double Ms { get; private set; }

    /// <summary>Right and within the time limit.</summary>
    public bool Fast { get; private set; }

    public bool UsedTiles { get; private set; }

    /// <summary>Why a made form was wrong (rules markup).</summary>
    public List<string> Why { get; private set; } = new();

    /// <summary>Decode: how to read the form (rules markup).</summary>
    public List<string> Clues { get; private set; } = new();

    public string? DecodeVerb { get; set; }
    public string? DecodeWho { get; set; }
    public string? DecodeWhen { get; set; }
    public bool DecodeReady => DecodeVerb is not null && DecodeWho is not null && DecodeWhen is not null;

    public string? BuildStem { get; set; }
    public string? BuildEnding { get; set; }

    /// <summary>The option picked (meaning questions).</summary>
    public string? Picked { get; private set; }

    /// <summary>The time limit for the question on screen, in seconds (infinity when time limits are off).</summary>
    public double LimitSeconds(SessionQuestion q, bool tiles) => VerbSpeed.Limit(q.Kind, q.Form, tiles, progress.VerbTimeLimits);

    // ------------------------------------------------------------------ planning

    private void Plan()
    {
        var now = DateTime.UtcNow;
        switch (Kind)
        {
            case Mode.Reviews:
            {
                foreach (var k in VerbCourse.DueKeys(kit, progress, now).Take(12).OrderBy(_ => rng.Next()))
                    slots.Add(new Slot(SlotKind.Review, k));
                Planned = slots.Count;
                return;
            }
            case Mode.Unit:
                slots.Add(new Slot(SlotKind.UnitBlock));
                unitLeft = 12;
                Planned = 12;
                return;
            case Mode.Check:
                slots.Add(new Slot(SlotKind.UnitBlock));
                unitLeft = 8;
                Planned = 5;
                return;
            case Mode.Placement:
                slots.Add(new Slot(SlotKind.UnitBlock));
                unitLeft = int.MaxValue;
                Planned = kit.Units.Count * 2;
                progress.VerbsStarted = true;
                return;
        }

        // Continue: about 2.5 questions a minute.
        var n = (int)Math.Round(2.5 * Minutes);
        var due = VerbCourse.DueKeys(kit, progress, now);
        var reviewCap = Math.Max(4, n / 2);
        lotsDue = due.Count > 2 * reviewCap;
        var current = VerbCourse.Current(kit, progress);
        var real = Math.Max(1, (int)Math.Round(n * 0.15));
        var quick = startStates.Values.Any(s => s is { Solid: true, Automatic: false }) ? (n >= 20 ? 3 : 2) : 0;
        var reviews = Math.Min(due.Count, lotsDue ? n - real : reviewCap);

        // With lots of reviews due, no new unit starts today (one already under way carries on).
        var unitOn = current is not null && (!lotsDue || VerbCourse.Find(progress, current)?.Started is not null);
        unitLeft = unitOn ? Math.Max(4, n - reviews - real - quick) : 0;
        if (current is null && reviews == 0 && startStates.Values.Any(s => s.Solid))
        {
            // All caught up: practise what you know.
            quick = Math.Max(quick, n - real);
        }

        var keys = due.Take(reviews).OrderBy(_ => rng.Next()).ToList();
        var half = keys.Count / 2;
        foreach (var k in keys.Take(half)) slots.Add(new Slot(SlotKind.Review, k));
        slots.Add(new Slot(SlotKind.UnitBlock));
        var tail = keys.Skip(half).Select(k => new Slot(SlotKind.Review, k)).ToList();
        for (var i = 0; i < real; i++) tail.Add(new Slot(SlotKind.Real));
        for (var i = 0; i < quick; i++) tail.Add(new Slot(SlotKind.Quick));
        slots.AddRange(tail.OrderBy(_ => rng.Next()));
        Planned = reviews + unitLeft + real + quick;
    }

    // ------------------------------------------------------------------ moving on

    /// <summary>On from the step on screen (after an answer, the idea, a note).</summary>
    public void Continue()
    {
        if (Current.Kind == StepKind.Question && !Answered) return;
        if (Current.Kind == StepKind.Idea && Current.Unit is { } u) VerbCourse.Record(progress, u).IdeaSeen = true;
        Advance();
    }

    /// <summary>"I know this" on the idea screen: the unit's check instead of the idea.</summary>
    public void KnowThis()
    {
        if (Current.Kind != StepKind.Idea || Current.Unit is not { } u) return;
        if (!StartCheck(u, intro: false)) VerbCourse.Record(progress, u).IdeaSeen = true;
        Advance();
    }

    /// <summary>Ends the session here (the placement check's "Stop here"). Answers given so far count.</summary>
    public void Stop()
    {
        placementStopped = true;
        pending.Clear();
        retries.Clear();
        inUnitBlock = false;
        slotIndex = slots.Count;
        ClearAnswer();
        Current = new Step { Kind = StepKind.End };
    }

    private void Advance()
    {
        ClearAnswer();
        LastActive = DateTime.UtcNow;

        var ri = retries.FindIndex(r => r.At <= AnsweredCount);
        if (ri >= 0)
        {
            var again = retries[ri].Q;
            retries.RemoveAt(ri);
            Current = Ask(again);
            return;
        }

        for (var guard = 0; guard < 400; guard++)
        {
            if (pending.Count > 0)
            {
                Current = pending[0];
                pending.RemoveAt(0);
                return;
            }
            if (inUnitBlock)
            {
                var step = NextUnitStep();
                if (step is not null)
                {
                    Current = step;
                    return;
                }
                inUnitBlock = false;
            }
            if (slotIndex >= slots.Count)
            {
                if (retries.Count > 0)
                {
                    var last = retries[0].Q;
                    retries.RemoveAt(0);
                    Current = Ask(last);
                    return;
                }
                Current = new Step { Kind = StepKind.End };
                return;
            }
            var slot = slots[slotIndex++];
            if (slot.Kind == SlotKind.UnitBlock)
            {
                inUnitBlock = true;
                continue;
            }
            var q = slot.Kind switch
            {
                SlotKind.Review => ReviewQuestion(slot.Key!),
                SlotKind.Real => RealQuestion(),
                _ => QuickQuestion(),
            };
            if (q is not null)
            {
                Current = Ask(q);
                return;
            }
        }
        Current = new Step { Kind = StepKind.End };
    }

    private static Step Ask(SessionQuestion q) => new() { Kind = StepKind.Question, Question = q, Unit = q.Unit };

    private Step Pop()
    {
        var s = pending[0];
        pending.RemoveAt(0);
        return s;
    }

    private void ClearAnswer()
    {
        Answered = false;
        Given = Matched = Picked = null;
        Ms = 0;
        Fast = false;
        UsedTiles = false;
        Why = new();
        Clues = new();
        DecodeVerb = DecodeWho = DecodeWhen = null;
        BuildStem = BuildEnding = null;
    }

    // ------------------------------------------------------------------ the unit block

    private Step? NextUnitStep()
    {
        if (Kind == Mode.Placement) return NextPlacementStep();
        if (unitLeft <= 0) return null;
        var u = Kind is Mode.Unit or Mode.Check ? Target : VerbCourse.Current(kit, progress);
        if (u is null) return null;
        var r = VerbCourse.Record(progress, u);

        if (Kind == Mode.Check)
        {
            if (checkUnit == u || r.Checked && AnsweredCount > 0) return null;
            return StartCheck(u, intro: false) ? Pop() : null;
        }

        if (r.Started is null)
        {
            if (Kind == Mode.Continue && lotsDue) return null;
            r.Started = DateTime.UtcNow;
        }
        if (!r.Checked && !r.HasEvidence && checkUnit != u)
        {
            if (StartCheck(u, intro: true)) return Pop();
            r.Checked = true;
        }
        if (!r.IdeaSeen) return new Step { Kind = StepKind.Idea, Unit = u };

        var q = NextLearnQuestion(u, VerbCourse.State(kit, progress, u));
        if (q is null) return null;
        unitLeft--;
        return Ask(q);
    }

    /// <summary>
    /// The next question for a unit: trying the rule on a new verb once the meters have started, otherwise the next
    /// step of the lower meter (Make or Read, taking turns when level). A solid unit (practised on purpose) gets
    /// quick questions.
    /// </summary>
    private SessionQuestion? NextLearnQuestion(VerbUnit u, UnitState state)
    {
        sincePredict++;
        if (state.Rule == 1 && UnitProgress.CanPredict(u) && predictUnit is null && sincePredict >= 3
            && (state.Make >= 1 || !state.MakeRequired) && state.Read >= 1)
        {
            var first = StartPredict(u, state);
            if (first is not null) return first;
        }

        var makeNeeded = state.MakeRequired && state.Make < 3;
        var readNeeded = state.Read < 3;
        Meter m;
        if (makeNeeded && readNeeded)
            m = state.Make < state.Read ? Meter.Make
                : state.Read < state.Make ? Meter.Read
                : lastMeter == Meter.Make ? Meter.Read : Meter.Make;
        else if (makeNeeded) m = Meter.Make;
        else if (readNeeded) m = Meter.Read;
        else m = state.MakeRequired && state.Make < 4 && (state.Read == 4 || rng.Next(2) == 0) ? Meter.Make : Meter.Read;
        lastMeter = m;

        var purpose = makeNeeded || readNeeded ? Purpose.Learn : Purpose.Quick;
        var q = m == Meter.Make ? MakeStep(u, state.Make, purpose) : ReadStep(u, state.Read, purpose);
        if (q is null)
            q = m == Meter.Make ? ReadStep(u, state.Read, purpose) : state.MakeRequired ? MakeStep(u, state.Make, purpose) : null;
        return q;
    }

    private SessionQuestion? MakeStep(VerbUnit u, int level, Purpose purpose)
    {
        if (u.Kind == UnitKind.Phrases) return EnglishQ(u, purpose);
        if (u.Kind == UnitKind.Choose) return ContextQ(u, purpose);
        if (level == 0)
        {
            var b = BuildQ(u, purpose);
            if (b is not null) return b;
            VerbCourse.Record(progress, u).Built = true;
            level = 1;
        }
        if (level == 1) return CueQ(u, purpose);
        if (level == 2)
        {
            var q = rng.Next(2) == 0 ? ContextQ(u, purpose) ?? EnglishQ(u, purpose) : EnglishQ(u, purpose) ?? ContextQ(u, purpose);
            return q ?? CueQ(u, purpose);
        }
        var quick = rng.Next(3) switch
        {
            0 => ContextQ(u, purpose),
            1 => EnglishQ(u, purpose),
            _ => null,
        };
        return quick ?? CueQ(u, purpose);
    }

    private SessionQuestion? ReadStep(VerbUnit u, int level, Purpose purpose)
    {
        if (u.Kind is UnitKind.Phrases or UnitKind.Choose) return SentenceQ(u, purpose);
        switch (level)
        {
            case 0: return DecodeQ(u, purpose) ?? MeaningQ(u, purpose);
            case 1: return MeaningQ(u, purpose) ?? DecodeQ(u, purpose);
            case 2: return SentenceQ(u, purpose) ?? MeaningQ(u, purpose);
        }
        SessionQuestion? q = null;
        if (canSpeak && rng.Next(3) == 0) q = MeaningQ(u, purpose, AskKind.Listen);
        q ??= rng.Next(2) == 0 ? MeaningQ(u, purpose) : DecodeQ(u, purpose);
        return q ?? SentenceQ(u, purpose);
    }

    // ------------------------------------------------------------------ the check, trying the rule

    /// <summary>
    /// Queues a unit's check: 3 forms to spell (one of a verb you haven't met, one trap) and 2 to decode; units you
    /// only read get 4 reading questions, phrase and choose units their own kinds. False when there's nothing to ask.
    /// </summary>
    private bool StartCheck(VerbUnit u, bool intro)
    {
        const Purpose p = Purpose.Check;
        var qs = new List<SessionQuestion?>();
        var state = VerbCourse.State(kit, progress, u);
        switch (u.Kind)
        {
            case UnitKind.Phrases:
                qs.Add(EnglishQ(u, p));
                qs.Add(EnglishQ(u, p));
                qs.Add(SentenceQ(u, p));
                qs.Add(SentenceQ(u, p));
                break;
            case UnitKind.Choose:
                for (var i = 0; i < 3; i++) qs.Add(ContextQ(u, p));
                for (var i = 0; i < 2; i++) qs.Add(SentenceQ(u, p));
                break;
            default:
                if (state.MakeRequired)
                {
                    qs.Add(CueQ(u, p, unseen: true));
                    qs.Add(TrapQ(u, p) ?? CueQ(u, p));
                    qs.Add(CueQ(u, p));
                    qs.Add(DecodeQ(u, p) ?? MeaningQ(u, p));
                    qs.Add(DecodeQ(u, p) ?? MeaningQ(u, p));
                }
                else
                {
                    qs.Add(DecodeQ(u, p, unseen: true));
                    qs.Add(DecodeQ(u, p));
                    qs.Add(MeaningQ(u, p));
                    qs.Add(SentenceQ(u, p) ?? DecodeQ(u, p));
                }
                break;
        }
        var list = qs.OfType<SessionQuestion>().DistinctBy(q => (q.Kind, q.FormKey, q.Before)).ToList();
        if (list.Count == 0) return false;

        checkUnit = u;
        checkTotal = list.Count;
        checkAnswers.Clear();
        if (intro) pending.Add(new Step { Kind = StepKind.CheckIntro, Unit = u });
        foreach (var q in list) pending.Add(Ask(q));
        unitLeft -= list.Count;
        return true;
    }

    /// <summary>
    /// The check is answered. All right: the unit is skipped as known (a review confirms it in a few days). Some
    /// right: the idea screen is skipped, and a meter answered fully right counts as done. None right: the full unit.
    /// </summary>
    private void ApplyCheck(VerbUnit u)
    {
        var r = VerbCourse.Record(progress, u);
        r.Checked = true;
        var make = checkAnswers.Where(a => a.Q.Meter == Meter.Make).ToList();
        var read = checkAnswers.Where(a => a.Q.Meter == Meter.Read).ToList();
        if (checkAnswers.All(a => a.G == Grade.Right))
        {
            r.Known = true;
            r.IdeaSeen = true;
            r.Predicted = true;
            r.MakeCredit = true;
            r.ReadCredit = true;
        }
        else
        {
            if (checkAnswers.Any(a => a.G == Grade.Right)) r.IdeaSeen = true;
            if (make.Count > 0 && make.All(a => a.G == Grade.Right)) r.MakeCredit = true;
            if (read.Count > 0 && read.All(a => a.G == Grade.Right)) r.ReadCredit = true;
            var tries = checkAnswers.Where(a => a.Q.Unseen || a.Q.Trap).ToList();
            if (tries.Count > 0 && tries.All(a => a.G == Grade.Right)) r.Predicted = true;
        }
        if (Kind != Mode.Check) checkUnit = null;
    }

    /// <summary>Trying the rule: a form of a verb you haven't met in this unit, then a trap. Returns the first.</summary>
    private SessionQuestion? StartPredict(VerbUnit u, UnitState state)
    {
        const Purpose p = Purpose.Predict;
        var list = new List<SessionQuestion>();
        if (state.MakeRequired)
        {
            if (CueQ(u, p, unseen: true) is { } a) list.Add(a);
            if (list.Count > 0 && TrapQ(u, p) is { } b) list.Add(b);
        }
        else if (DecodeQ(u, p, unseen: true) is { } c) list.Add(c);

        if (list.Count == 0)
        {
            // No verb left that you haven't met: nothing to try it on.
            VerbCourse.Record(progress, u).Predicted = true;
            return null;
        }
        predictUnit = u;
        predictTotal = list.Count;
        predictAnswers.Clear();
        sincePredict = 0;
        foreach (var q in list.Skip(1)) pending.Add(Ask(q));
        unitLeft -= list.Count - 1;
        return list[0];
    }

    // ------------------------------------------------------------------ the placement check

    private Step? NextPlacementStep()
    {
        if (placementStopped) return null;
        var later = VerbCourse.LaterMake(kit, progress);
        while (placementIndex < kit.Units.Count)
        {
            var u = kit.Units[placementIndex++];
            if (UnitProgress.Evaluate(u, VerbCourse.Find(progress, u), later).Solid) continue;
            var qs = PlacementQuestions(u).OfType<SessionQuestion>().DistinctBy(q => (q.Kind, q.FormKey)).ToList();
            if (qs.Count == 0) continue;
            placementUnit = u;
            placementTotal = qs.Count;
            placementAnswers.Clear();
            foreach (var q in qs.Skip(1)) pending.Add(Ask(q));
            return Ask(qs[0]);
        }
        return null;
    }

    /// <summary>Two questions per unit: one to make and one to read (two to read for units you read first).</summary>
    private List<SessionQuestion?> PlacementQuestions(VerbUnit u)
    {
        const Purpose p = Purpose.Placement;
        if (u.Kind == UnitKind.Phrases) return new List<SessionQuestion?> { EnglishQ(u, p), SentenceQ(u, p) };
        if (u.Kind == UnitKind.Choose) return new List<SessionQuestion?> { ContextQ(u, p), SentenceQ(u, p) };
        if (u.Goal == UnitGoal.Use) return new List<SessionQuestion?> { CueQ(u, p), DecodeQ(u, p) ?? MeaningQ(u, p) };
        return new List<SessionQuestion?> { DecodeQ(u, p) ?? MeaningQ(u, p), MeaningQ(u, p) };
    }

    /// <summary>
    /// A unit's placement questions are answered. Both right: known (confirmed in a review). One right: that meter
    /// counts. Neither: two such units in a row skip to the next tier, and two more like that end the check.
    /// </summary>
    private void ApplyPlacement(VerbUnit u)
    {
        var r = VerbCourse.Record(progress, u);
        PlacementChecked++;
        r.Checked = true;
        var make = placementAnswers.Where(a => a.Q.Meter == Meter.Make).ToList();
        var read = placementAnswers.Where(a => a.Q.Meter == Meter.Read).ToList();
        if (placementAnswers.All(a => a.G == Grade.Right))
        {
            r.Known = true;
            r.IdeaSeen = true;
            r.Predicted = true;
            r.MakeCredit = true;
            r.ReadCredit = true;
            r.Started ??= DateTime.UtcNow;
            PlacementKnown++;
            placementMisses = 0;
            placementJumped = false;
        }
        else if (placementAnswers.Any(a => a.G == Grade.Right))
        {
            r.IdeaSeen = true;
            r.Started ??= DateTime.UtcNow;
            if (make.Count > 0 && make.All(a => a.G == Grade.Right)) r.MakeCredit = true;
            if (read.Count > 0 && read.All(a => a.G == Grade.Right)) r.ReadCredit = true;
            placementMisses = 0;
            placementJumped = false;
        }
        else if (++placementMisses >= 2)
        {
            placementMisses = 0;
            if (placementJumped) placementStopped = true;
            else
            {
                placementJumped = true;
                var next = -1;
                for (var i = 0; i < kit.Units.Count; i++)
                    if (kit.Units[i].Tier > u.Tier) { next = i; break; }
                if (next < 0) placementStopped = true;
                else placementIndex = Math.Max(placementIndex, next);
            }
        }
        placementUnit = null;
    }

    // ------------------------------------------------------------------ reviews, real sentences, quick questions

    private SessionQuestion? ReviewQuestion(string key)
    {
        var meter = VerbReviews.MeterOf(key);
        SessionQuestion? q = null;
        const Purpose p = Purpose.Review;
        if (key.StartsWith("u:", StringComparison.Ordinal))
        {
            var u = kit.Unit(key[2..key.LastIndexOf(':')]);
            if (u is null) return null;
            if (meter == Meter.Make)
            {
                if (u.Kind == UnitKind.Phrases) q = EnglishQ(u, p);
                else if (u.Kind == UnitKind.Choose) q = ContextQ(u, p);
                else
                {
                    var roll = rng.Next(4);
                    if (roll == 0) q = ContextQ(u, p);
                    else if (roll == 1) q = EnglishQ(u, p);
                    q ??= CueQ(u, p);
                }
            }
            else
            {
                if (u.Kind is UnitKind.Phrases or UnitKind.Choose) q = SentenceQ(u, p);
                else
                {
                    var roll = rng.Next(3);
                    q = roll == 0 ? DecodeQ(u, p) : roll == 1 ? MeaningQ(u, p) : SentenceQ(u, p);
                    q ??= DecodeQ(u, p) ?? MeaningQ(u, p);
                }
            }
        }
        else if (VerbReviews.VerbOf(key) is { } vt && kit.Book.ByInf.TryGetValue(vt.Inf, out var v))
        {
            var persons = Enumerable.Range(0, VerbGrammar.PersonsFor(vt.Tense).Count)
                .Where(x => v.HasForm(vt.Tense, x) && VerbSettings.IsActive(vt.Tense, x)).ToList();
            if (persons.Count == 0) return null;
            // The weakest person first, with a little chance.
            var person = persons.OrderBy(x => Strength(meter, v, vt.Tense, x) + rng.NextDouble() * 0.4).First();
            var u = kit.UnitOf(v, vt.Tense, person);
            if (meter == Meter.Make) q = MakeCue(u, v, vt.Tense, person, VerbGameKit.PracticeKey(v, vt.Tense, person), p);
            else
            {
                var d = kit.DecodeItem(v, vt.Tense, person);
                if (d is not null && rng.Next(2) == 0) q = MakeDecode(u, d, p);
                q ??= MeaningFor(u, v, vt.Tense, person, p, AskKind.Meaning);
                if (q is null && d is not null) q = MakeDecode(u, d, p);
            }
        }
        return q is null ? null : q with { ReviewKey = key };
    }

    /// <summary>A real sentence (a story, a gap sentence, or one of your texts) with a form from a unit you've started reading.</summary>
    private SessionQuestion? RealQuestion()
    {
        var later = VerbCourse.LaterMake(kit, progress);
        bool Ok(VerbUnit? u)
        {
            if (u is null || u.Kind == UnitKind.Phrases || VerbCourse.Find(progress, u) is not { } r) return false;
            var s = UnitProgress.Evaluate(u, r, later);
            return s.Solid || s.Read >= 1;
        }

        var eligible = kit.Units.Where(Ok).ToList();
        if (eligible.Count == 0) return null;

        if (texts.Count > 0 && rng.Next(5) < 2)
        {
            var mine = texts.Where(t => VerbSettings.IsActive(t.Tense, t.Person) && Ok(kit.UnitOf(t.Verb, t.Tense, t.Person))).ToList();
            if (mine.Count > 0 && TextRead(mine[rng.Next(mine.Count)]) is { } tq) return tq;
        }
        for (var i = 0; i < 6; i++)
        {
            var q = SentenceQ(eligible[rng.Next(eligible.Count)], Purpose.Real);
            if (q is not null) return q;
        }
        return null;
    }

    /// <summary>A quick question on a unit that's solid but not automatic yet (or any solid unit when all are).</summary>
    private SessionQuestion? QuickQuestion()
    {
        var later = VerbCourse.LaterMake(kit, progress);
        var states = kit.Units.Select(u => (Unit: u, State: UnitProgress.Evaluate(u, VerbCourse.Find(progress, u), later))).ToList();
        var pool = states.Where(x => x.State is { Solid: true, Automatic: false }).ToList();
        if (pool.Count == 0) pool = states.Where(x => x.State.Solid).ToList();
        if (pool.Count == 0) return null;
        var (unit, state) = pool[rng.Next(pool.Count)];
        var make = state.MakeRequired && state.Make < 4 && (state.Read == 4 || rng.Next(2) == 0);
        return make ? MakeStep(unit, 3, Purpose.Quick) : ReadStep(unit, 3, Purpose.Quick);
    }

    // ------------------------------------------------------------------ choosing forms

    private VerbBook Book => kit.Book;

    private static string? InfOf(PracticeItem i) => i.Verb ?? (i.Form.IsPattern ? null : i.Form.VerbKey);

    /// <summary>Verbs you've already made or read in this unit.</summary>
    private static HashSet<string> Used(UnitRecord? r)
    {
        var set = new HashSet<string>();
        if (r is null) return set;
        foreach (var k in r.Cue.Concat(r.Decoded)) set.Add(k.Split('|')[0]);
        return set;
    }

    private double Strength(Meter m, Verb v, string t, int p) =>
        progress.VerbSkills.GetValueOrDefault(m == Meter.Make ? VerbGameKit.PracticeKey(v, t, p) : VerbGameKit.DecodeKey(v, t, p))?.Strength ?? 0;

    /// <summary>
    /// A form of the unit and the verb to ask it with: weak forms first (with some chance), not the verb of the last
    /// question. <paramref name="unseen"/>: a verb you haven't made or read in this unit yet (null when there's none).
    /// </summary>
    private (PracticeItem Item, Verb Verb)? Pick(VerbUnit u, bool unseen = false, Func<PracticeItem, Verb, bool>? fits = null)
    {
        var items = u.Active.ToList();
        if (items.Count == 0) return null;
        var used = Used(VerbCourse.Find(progress, u));
        var ordered = items.OrderBy(i => (progress.VerbSkills.GetValueOrDefault(i.Form.Key)?.Strength ?? 0) + rng.NextDouble() * 0.5).ToList();
        var patterns = items.Where(i => i.Form.IsPattern && i.Verb is null).ToList();
        var fresh = items.Where(i => InfOf(i) is { } inf && !used.Contains(inf)).ToList();

        for (var attempt = 0; attempt < 16; attempt++)
        {
            PracticeItem item;
            if (unseen)
            {
                if (patterns.Count > 0 && (fresh.Count == 0 || rng.Next(2) == 0)) item = patterns[rng.Next(patterns.Count)];
                else if (fresh.Count > 0) item = fresh[rng.Next(fresh.Count)];
                else return null;
            }
            else item = attempt < ordered.Count ? ordered[attempt] : items[rng.Next(items.Count)];

            var verb = VerbFor(item, unseen ? v => !used.Contains(v.Inf) : null);
            if (verb is null) continue;
            if (unseen && used.Contains(verb.Inf)) continue;
            if (verb.Inf == lastVerb && attempt < 8 && (items.Count > 1 || item.Form.IsPattern)) continue;
            if (!verb.HasForm(item.Form.Tense, item.Form.Person)) continue;
            if (fits is not null && !fits(item, verb)) continue;
            return (item, verb);
        }
        return null;
    }

    /// <summary>The verb for an item: its own, or for a regular pattern a common regular verb (common ones more often).</summary>
    private Verb? VerbFor(PracticeItem item, Func<Verb, bool>? prefer = null)
    {
        if (item.Verb is not null) return Book.ByInf.GetValueOrDefault(item.Verb);
        if (!item.Form.IsPattern) return Book.ByInf.GetValueOrDefault(item.Form.VerbKey);
        var samples = Book.PatternSamples(item.Form.Tense, item.Form.Pattern!).Take(60).ToList();
        if (samples.Count == 0) return null;
        var pool = prefer is null ? samples : samples.Where(prefer).ToList();
        if (pool.Count == 0) pool = samples;
        var idx = Math.Min(pool.Count - 1, (int)(Math.Pow(rng.NextDouble(), 2) * pool.Count));
        var v = pool[idx];
        if (v.Inf == lastVerb && pool.Count > 1) v = pool[(idx + 1) % pool.Count];
        return v;
    }

    /// <summary>
    /// Who, as a cue says it. The él and ellos forms are often asked as usted and ustedes (and sometimes ella, ellas),
    /// since those are the forms you'll use with people you don't know.
    /// </summary>
    private string CueLabel(Verb v, string t, int p)
    {
        var person = VerbGrammar.PersonsFor(t)[p];
        if (t == "cmd") return person.Label;
        if (v.OnlyThird) return "it";
        return person.Group switch
        {
            "el" => rng.Next(5) switch { 0 or 1 => "usted", 2 => "ella", _ => "él" },
            "ellos" => rng.Next(5) switch { 0 or 1 => "ustedes", 2 => "ellas", _ => "ellos" },
            _ => person.Label,
        };
    }

    private HashSet<string> KnownTenses(VerbUnit u)
    {
        var set = new HashSet<string>(u.Tenses);
        foreach (var x in kit.Units)
            if (VerbCourse.Find(progress, x)?.Started is not null)
                foreach (var t in x.Tenses) set.Add(t);
        return set;
    }

    private int Decoys(Purpose purpose) => purpose is Purpose.Review or Purpose.Quick or Purpose.Check or Purpose.Placement ? 2 : 0;

    // ------------------------------------------------------------------ making questions

    private SessionQuestion? CueQ(VerbUnit u, Purpose purpose, bool unseen = false)
    {
        if (Pick(u, unseen) is not { } pick) return null;
        return MakeCue(u, pick.Verb, pick.Item.Form.Tense, pick.Item.Form.Person, pick.Item.Form.Key, purpose, unseen);
    }

    private SessionQuestion MakeCue(VerbUnit? u, Verb v, string t, int p, string skillKey, Purpose purpose, bool unseen = false, bool trap = false)
    {
        lastVerb = v.Inf;
        var answers = v.Answers(t, p);
        return new SessionQuestion
        {
            Kind = AskKind.Cue,
            Purpose = purpose,
            Unit = u,
            Verb = v,
            Tense = t,
            Person = p,
            Form = answers.Count > 0 ? answers[0] : v.Form(t, p) ?? "",
            Answers = answers,
            SkillKey = skillKey,
            PersonLabel = CueLabel(v, t, p),
            Unseen = unseen,
            Trap = trap,
            ExtraDecoys = Decoys(purpose),
        };
    }

    /// <summary>A form of one of the group's verbs where its change doesn't happen (pensamos, buscó).</summary>
    private SessionQuestion? TrapQ(VerbUnit u, Purpose purpose)
    {
        if (u.Kind != UnitKind.Group) return null;
        var pairs = u.Active.Where(i => !i.Form.IsPattern && i.Verb is null)
            .Select(i => (Inf: i.Form.VerbKey, i.Form.Tense)).Distinct().OrderBy(_ => rng.Next()).ToList();
        foreach (var (inf, t) in pairs)
        {
            if (!Book.ByInf.TryGetValue(inf, out var v) || v.Reflexive) continue;
            var persons = Enumerable.Range(0, VerbGrammar.PersonsFor(t).Count)
                .Where(p => v.HasForm(t, p) && VerbSettings.IsActive(t, p) && v.Why(t, p) is null).ToList();
            if (persons.Count == 0) continue;
            var person = persons[rng.Next(persons.Count)];
            return MakeCue(u, v, t, person, VerbGameKit.PracticeKey(v, t, person), purpose, trap: true);
        }
        return null;
    }

    private SessionQuestion? BuildQ(VerbUnit u, Purpose purpose)
    {
        for (var i = 0; i < 6; i++)
        {
            if (Pick(u) is not { } pick) return null;
            var (item, v) = pick;
            var (t, p) = (item.Form.Tense, item.Form.Person);
            var chunks = VerbMeanings.Build(v, t, p);
            if (chunks is null) continue;
            lastVerb = v.Inf;
            var answers = v.Answers(t, p);
            return new SessionQuestion
            {
                Kind = AskKind.Build,
                Purpose = purpose,
                Unit = u,
                Verb = v,
                Tense = t,
                Person = p,
                Form = answers[0],
                Answers = answers,
                SkillKey = item.Form.Key,
                PersonLabel = CueLabel(v, t, p),
                Build = chunks,
            };
        }
        return null;
    }

    /// <summary>A sentence with a gap: a gap sentence (or clue sentence) of the unit, or a verb from a story.</summary>
    private SessionQuestion? ContextQ(VerbUnit u, Purpose purpose)
    {
        for (var i = 0; i < 6; i++)
        {
            var useSentence = u.Sentences.Count > 0 && (u.Stories.Count == 0 || rng.Next(3) > 0);
            SessionQuestion? q = null;
            if (useSentence) q = GapFromSentence(u, u.Sentences[rng.Next(u.Sentences.Count)], purpose, AskKind.Context);
            else if (u.Stories.Count > 0) q = GapFromStory(u, u.Stories[rng.Next(u.Stories.Count)], purpose);
            else return null;
            if (q is not null) return q;
        }
        return null;
    }

    private SessionQuestion? GapFromSentence(VerbUnit u, VerbSentence s, Purpose purpose, AskKind kind)
    {
        if (!Book.ByInf.TryGetValue(s.Inf, out var v) || !VerbSettings.IsActive(s.Tense, s.Person)) return null;
        var answers = VerbSentences.Answers(s, Book);
        if (answers.Count == 0) return null;
        var (before, after) = VerbSentences.Split(s.Es);
        lastVerb = v.Inf;
        return new SessionQuestion
        {
            Kind = kind,
            Purpose = purpose,
            Unit = u,
            Verb = v,
            Tense = s.Tense,
            Person = s.Person,
            Form = answers[0],
            Answers = answers,
            SkillKey = VerbGameKit.PracticeKey(v, s.Tense, s.Person),
            Before = before,
            After = after,
            Hint = u.Kind == UnitKind.Phrases ? null : v.Inf,
            English = s.En,
            Clue = s.Clue,
            AtStart = VerbSentences.AtStart(before),
            ExtraDecoys = Decoys(purpose),
        };
    }

    private SessionQuestion? GapFromStory(VerbUnit u, StorySpot spot, Purpose purpose)
    {
        if (!VerbSettings.IsActive(spot.Tense, spot.Person)) return null;
        var toks = spot.Sentence.Tokens;
        var word = toks[spot.Token].X;
        var answers = spot.Verb.Answers(spot.Tense, spot.Person).ToList();
        if (!answers.Any(a => string.Equals(a, word, StringComparison.OrdinalIgnoreCase))) answers.Insert(0, word.ToLowerInvariant());
        var before = string.Concat(toks.Take(spot.Token).Select(t => t.X));
        lastVerb = spot.Verb.Inf;
        return new SessionQuestion
        {
            Kind = AskKind.Context,
            Purpose = purpose,
            Unit = u,
            Verb = spot.Verb,
            Tense = spot.Tense,
            Person = spot.Person,
            Form = answers[0],
            Answers = answers,
            SkillKey = VerbGameKit.PracticeKey(spot.Verb, spot.Tense, spot.Person),
            Before = before,
            After = string.Concat(toks.Skip(spot.Token + 1).Select(t => t.X)),
            Hint = spot.Verb.Inf,
            English = spot.Sentence.En,
            Source = $"From the story \"{spot.Story.Title}\"",
            AtStart = VerbSentences.AtStart(before),
            ExtraDecoys = Decoys(purpose),
        };
    }

    /// <summary>
    /// English to Spanish: "we had (tener)" → tuvimos. "You" means tú in the English, so usted's form counts too.
    /// A phrase unit gives the English sentence and the Spanish with a gap.
    /// </summary>
    private SessionQuestion? EnglishQ(VerbUnit u, Purpose purpose)
    {
        if (u.Kind == UnitKind.Phrases)
            return u.Sentences.Count == 0 ? null : GapFromSentence(u, u.Sentences[rng.Next(u.Sentences.Count)], purpose, AskKind.English);

        if (Pick(u, fits: (item, v) => VerbEnglish.Meaning(v, item.Form.Tense, item.Form.Person) is not null) is not { } pick)
            return null;
        var (it, verb) = pick;
        var (t, p) = (it.Form.Tense, it.Form.Person);
        var answers = verb.Answers(t, p).ToList();
        if (answers.Count == 0) return null;
        var persons = VerbGrammar.PersonsFor(t);
        var group = persons[p].Group;
        if (t != "cmd" && group is "tu" or "vos")
        {
            var other = group == "tu" ? "el" : "ellos";
            for (var q = 0; q < persons.Count; q++)
                if (persons[q].Group == other) answers.AddRange(verb.Answers(t, q));
        }
        lastVerb = verb.Inf;
        return new SessionQuestion
        {
            Kind = AskKind.English,
            Purpose = purpose,
            Unit = u,
            Verb = verb,
            Tense = t,
            Person = p,
            Form = answers[0],
            Answers = answers,
            SkillKey = it.Form.Key,
            English = VerbEnglish.Meaning(verb, t, p),
            Hint = verb.Inf,
            ExtraDecoys = Decoys(purpose),
        };
    }

    // ------------------------------------------------------------------ reading questions

    private SessionQuestion? DecodeQ(VerbUnit u, Purpose purpose, bool unseen = false)
    {
        // The read-only unit: now and then a vosotros form to recognise, when vosotros is off in Settings.
        if (u.Def.VosotrosRead && !VerbSettings.Vosotros && !unseen && rng.Next(3) == 0 && VosotrosDecode(u, purpose) is { } vq)
            return vq;
        if (Pick(u, unseen, (item, v) => kit.DecodeItem(v, item.Form.Tense, item.Form.Person) is not null) is not { } pick)
            return null;
        var d = kit.DecodeItem(pick.Verb, pick.Item.Form.Tense, pick.Item.Form.Person)!;
        return MakeDecode(u, d, purpose, unseen);
    }

    private SessionQuestion MakeDecode(VerbUnit? u, DecodeItem d, Purpose purpose, bool unseen = false, bool allPersons = false)
    {
        lastVerb = d.Verb.Inf;
        var tenses = u is null ? new HashSet<string> { d.Tense } : KnownTenses(u);
        return new SessionQuestion
        {
            Kind = AskKind.Decode,
            Purpose = purpose,
            Unit = u,
            Verb = d.Verb,
            Tense = d.Tense,
            Person = d.Person,
            Form = d.Form,
            Answers = [d.Form],
            Decode = d,
            VerbChoices = kit.Decode.VerbOptions(d),
            WhenChoices = VerbDecode.WhenOptions(d, tenses),
            Unseen = unseen,
            AllPersons = allPersons,
        };
    }

    private SessionQuestion? VosotrosDecode(VerbUnit u, Purpose purpose)
    {
        string[] tenses = ["pres", "pret", "impf", "fut", "subj"];
        for (var i = 0; i < 10; i++)
        {
            var v = Book.Verbs[rng.Next(Math.Min(40, Book.Verbs.Count))];
            var t = tenses[rng.Next(tenses.Length)];
            if (v.OnlyThird || !v.HasForm(t, 4) || kit.DecodeItem(v, t, 4) is not { } d) continue;
            return MakeDecode(u, d, purpose, allPersons: true);
        }
        return null;
    }

    private SessionQuestion? MeaningQ(VerbUnit u, Purpose purpose, AskKind kind = AskKind.Meaning)
    {
        for (var i = 0; i < 6; i++)
        {
            if (Pick(u, fits: (_, v) => VerbEnglish.Has(v)) is not { } pick) return null;
            var q = MeaningFor(u, pick.Verb, pick.Item.Form.Tense, pick.Item.Form.Person, purpose, kind);
            if (q is not null) return q;
        }
        return null;
    }

    private SessionQuestion? MeaningFor(VerbUnit? u, Verb v, string t, int p, Purpose purpose, AskKind kind)
    {
        var choices = VerbMeanings.For(Book, v, t, p);
        var form = v.Form(t, p);
        if (choices is null || form is null) return null;
        lastVerb = v.Inf;
        return new SessionQuestion
        {
            Kind = kind,
            Purpose = purpose,
            Unit = u,
            Verb = v,
            Tense = t,
            Person = p,
            Form = form,
            Answers = [form],
            Meaning = choices,
        };
    }

    /// <summary>A form in its sentence: what does it mean here? (Phrase units: what does the sentence mean?)</summary>
    private SessionQuestion? SentenceQ(VerbUnit u, Purpose purpose)
    {
        if (u.Kind == UnitKind.Phrases) return PhraseMeaning(u, purpose);
        var rivals = u.Kind == UnitKind.Choose ? u.Def.Choose : null;
        for (var i = 0; i < 6; i++)
        {
            var fromStory = u.Stories.Count > 0 && (u.Sentences.Count == 0 || rng.Next(2) == 0);
            SessionQuestion? q;
            if (fromStory) q = StoryRead(u, u.Stories[rng.Next(u.Stories.Count)], purpose, rivals);
            else if (u.Sentences.Count > 0) q = SentenceRead(u, u.Sentences[rng.Next(u.Sentences.Count)], purpose, rivals);
            else return null;
            if (q is not null) return q;
        }
        return null;
    }

    private SessionQuestion? StoryRead(VerbUnit? u, StorySpot spot, Purpose purpose, IReadOnlyList<string>? rivals)
    {
        if (!VerbSettings.IsActive(spot.Tense, spot.Person)) return null;
        var toks = spot.Sentence.Tokens;
        var tok = toks[spot.Token];
        var readings = (tok.P ?? new[] { spot.Person }).Select(x => (Verb: spot.Verb, Tense: spot.Tense, Person: x)).ToList();
        var choices = VerbMeanings.For(Book, spot.Verb, spot.Tense, spot.Person, readings, rivals);
        if (choices is null) return null;
        lastVerb = spot.Verb.Inf;
        return new SessionQuestion
        {
            Kind = AskKind.Sentence,
            Purpose = purpose,
            Unit = u,
            Verb = spot.Verb,
            Tense = spot.Tense,
            Person = spot.Person,
            Form = tok.X,
            Answers = [tok.X],
            Before = string.Concat(toks.Take(spot.Token).Select(t => t.X)),
            After = string.Concat(toks.Skip(spot.Token + 1).Select(t => t.X)),
            English = spot.Sentence.En,
            Meaning = choices,
            Source = $"From the story \"{spot.Story.Title}\"",
        };
    }

    private SessionQuestion? SentenceRead(VerbUnit u, VerbSentence s, Purpose purpose, IReadOnlyList<string>? rivals)
    {
        if (!Book.ByInf.TryGetValue(s.Inf, out var v) || !VerbSettings.IsActive(s.Tense, s.Person)) return null;
        var form = VerbSentences.Answer(s, Book);
        if (form.Length == 0) return null;
        var (before, after) = VerbSentences.Split(s.Es);
        var choices = VerbMeanings.For(Book, v, s.Tense, s.Person, new[] { (Verb: v, Tense: s.Tense, Person: s.Person) }, rivals);
        if (choices is null) return null;
        lastVerb = v.Inf;
        return new SessionQuestion
        {
            Kind = AskKind.Sentence,
            Purpose = purpose,
            Unit = u,
            Verb = v,
            Tense = s.Tense,
            Person = s.Person,
            Form = VerbSentences.Shown(form, VerbSentences.AtStart(before)),
            Answers = [form],
            Before = before,
            After = after,
            English = s.En,
            Clue = s.Clue,
            Meaning = choices,
        };
    }

    /// <summary>A set phrase in its sentence: pick what the sentence means.</summary>
    private SessionQuestion? PhraseMeaning(VerbUnit u, Purpose purpose)
    {
        if (u.Sentences.Count < 3) return null;
        var s = u.Sentences[rng.Next(u.Sentences.Count)];
        if (!Book.ByInf.TryGetValue(s.Inf, out var v)) return null;
        var form = VerbSentences.Answer(s, Book);
        var (before, after) = VerbSentences.Split(s.Es);
        var wrong = u.Sentences.Select(x => x.En).Where(en => en != s.En).Distinct().OrderBy(_ => rng.Next()).Take(3);
        var options = wrong.Append(s.En).OrderBy(_ => rng.Next()).ToList();
        return new SessionQuestion
        {
            Kind = AskKind.Sentence,
            Purpose = purpose,
            Unit = u,
            Verb = v,
            Tense = s.Tense,
            Person = s.Person,
            Form = VerbSentences.Shown(form, VerbSentences.AtStart(before)),
            Answers = [form],
            Before = before,
            After = after,
            English = s.En,
            Meaning = new MeaningChoices(s.En, new HashSet<string> { s.En }, options),
            WholeSentence = true,
        };
    }

    private SessionQuestion? TextRead(TextSpot spot)
    {
        var choices = VerbMeanings.For(Book, spot.Verb, spot.Tense, spot.Person, spot.Readings);
        if (choices is null) return null;
        lastVerb = spot.Verb.Inf;
        return new SessionQuestion
        {
            Kind = AskKind.Sentence,
            Purpose = Purpose.Real,
            Unit = kit.UnitOf(spot.Verb, spot.Tense, spot.Person),
            Verb = spot.Verb,
            Tense = spot.Tense,
            Person = spot.Person,
            Form = spot.Word,
            Answers = [spot.Word],
            Before = spot.Line[..spot.Start],
            After = spot.Line[(spot.Start + spot.Length)..],
            English = spot.English,
            Meaning = choices,
            Source = $"From your text \"{spot.Title}\"",
        };
    }

    // ------------------------------------------------------------------ answering

    /// <summary>A spelled answer (keyboard or tiles). True when it was recorded (progress needs saving).</summary>
    public bool AnswerSpelled(string typed, bool tiles, double ms)
    {
        if (Answered || Question is not { } q || string.IsNullOrWhiteSpace(typed)) return false;
        var (grade, matched) = AnswerCheck.Check(typed, q.Answers, q.Negative);
        Matched = matched;
        Record(q, grade, ms, typed, tiles);
        return true;
    }

    /// <summary>Stem and ending picked (both set): check them.</summary>
    public bool AnswerBuild(double ms)
    {
        if (Answered || Question is not { } q || q.Build is null || BuildStem is null || BuildEnding is null) return false;
        var built = BuildStem + BuildEnding;
        var right = q.Answers.Any(a => string.Equals(a, built, StringComparison.OrdinalIgnoreCase));
        Matched = right ? built : q.Form;
        Record(q, right ? Grade.Right : Grade.Wrong, ms, built, tiles: false);
        return true;
    }

    /// <summary>Verb, who and when picked: check them (one of who / when wrong is half right).</summary>
    public bool AnswerDecode(double ms)
    {
        if (Answered || Question is not { } q || q.Decode is not { } d || !DecodeReady) return false;
        var grade = VerbDecode.GradeOf(d, DecodeVerb!, DecodeWho!, DecodeWhen!);
        Clues = VerbDecode.Clues(d, DecodeVerb, DecodeWho, DecodeWhen);
        Record(q, grade, ms, DecodeVerb, tiles: false);
        return true;
    }

    public bool AnswerChoice(string option, double ms)
    {
        if (Answered || Question is not { } q || q.Meaning is not { } m) return false;
        Picked = option;
        Record(q, m.AllRight.Contains(option) ? Grade.Right : Grade.Wrong, ms, option, tiles: false);
        return true;
    }

    /// <summary>"I don't know": a wrong answer, with the answer and how to work it out.</summary>
    public bool DontKnow(double ms)
    {
        if (Answered || Question is not { } q) return false;
        if (q.Decode is { } d) Clues = VerbDecode.Clues(d);
        Matched = q.Form;
        Record(q, Grade.Wrong, ms, null, tiles: false);
        return true;
    }

    private void Record(SessionQuestion q, Grade grade, double ms, string? given, bool tiles)
    {
        var now = DateTime.UtcNow;
        Answered = true;
        Grade = grade;
        Given = given;
        Ms = ms;
        UsedTiles = tiles;
        Fast = grade == Grade.Right && VerbSpeed.IsFast(q.Kind, q.Form, tiles, ms, progress.VerbTimeLimits);
        AnsweredCount++;
        if (grade == Grade.Right) RightCount++;
        LastActive = now;

        // The form's own strength (the Browse tree, the grid and Decode use it).
        var skillKey = q.Meter == Meter.Make ? q.SkillKey : VerbGameKit.DecodeKey(q.Verb, q.Tense, q.Person);
        if (skillKey is not null)
        {
            progress.VerbSkills.TryGetValue(skillKey, out var before);
            progress.VerbSkills[skillKey] = VerbSrs.Apply(before, grade, typed: q.Kind is not (AskKind.Build or AskKind.Meaning or AskKind.Sentence or AskKind.Listen), now);
        }

        // A review: its memory. A unit skipped as known that fails its first review goes back to learning.
        if (q.Purpose == Purpose.Review && q.ReviewKey is { } key)
        {
            if (q.Unit is not null && progress.VerbMemory.TryGetValue(key, out var mem) && mem.Confirm && grade == Grade.Wrong)
                VerbCourse.FallBack(progress, q.Unit, q.Meter);
            VerbReviews.Rate(progress.VerbMemory, key, VerbReviews.Rating(grade, Fast), now);
        }

        if (q.Unit is { } u)
        {
            var r = VerbCourse.Record(progress, u);
            if (q.Purpose != Purpose.Placement || grade == Grade.Right) r.Started ??= now;
            var wasSolid = VerbCourse.State(kit, progress, u).Solid;

            if (grade == Grade.Right)
            {
                switch (q.Kind)
                {
                    case AskKind.Build: r.Built = true; break;
                    case AskKind.Cue: if (!r.Cue.Contains(q.FormKey)) r.Cue.Add(q.FormKey); break;
                    case AskKind.Context or AskKind.English: r.Context++; break;
                    case AskKind.Decode: if (!r.Decoded.Contains(q.FormKey)) r.Decoded.Add(q.FormKey); break;
                    case AskKind.Meaning: r.Meaning++; break;
                    case AskKind.Sentence: r.Sentences++; break;
                    case AskKind.Listen: r.Meaning++; r.Heard = true; break;
                }
            }
            if (q.Kind is AskKind.Cue or AskKind.Context or AskKind.English) r.MakeRecent = UnitRecord.Push(r.MakeRecent, Fast);
            else if (q.Kind != AskKind.Build) r.ReadRecent = UnitRecord.Push(r.ReadRecent, Fast);

            if (q.Purpose == Purpose.Check && checkUnit == u)
            {
                checkAnswers.Add((q, grade, Fast));
                if (checkAnswers.Count >= checkTotal) ApplyCheck(u);
            }
            if (q.Purpose == Purpose.Predict && predictUnit == u)
            {
                predictAnswers.Add(grade);
                if (predictAnswers.Count >= predictTotal)
                {
                    if (predictAnswers.All(g => g == Grade.Right)) r.Predicted = true;
                    predictUnit = null;
                }
            }
            if (q.Purpose == Purpose.Placement && placementUnit == u)
            {
                placementAnswers.Add((q, grade));
                if (placementAnswers.Count >= placementTotal) ApplyPlacement(u);
            }

            var after = VerbCourse.State(kit, progress, u);
            if (after.Solid)
            {
                r.Solid ??= now;
                VerbReviews.Start(progress.VerbMemory, u, after.MakeRequired, true, r.Known, now);
            }
            if (after.Automatic) r.Automatic ??= now;
            if (!wasSolid && after.Solid && Kind is Mode.Continue or Mode.Unit or Mode.Check && q.Purpose != Purpose.Review)
                pending.Insert(0, new Step { Kind = StepKind.UnitDone, Unit = u, Known = r.Known });
        }

        if (grade != Grade.Right && q.Meter == Meter.Make && q.Kind != AskKind.Build && q.Unit?.Kind != UnitKind.Phrases)
            Why = VerbExplain.Mistake(q.Verb, q.Tense, q.Person, given, grade);

        // A miss comes back once, three questions later.
        if (grade == Grade.Wrong && q.Purpose is not (Purpose.Retry or Purpose.Check or Purpose.Placement or Purpose.Predict))
            retries.Add((AnsweredCount + 3, q.Again(Purpose.Retry)));
    }

    // ------------------------------------------------------------------ the end

    /// <summary>Units whose meters or status changed in this session, in unit order.</summary>
    public List<(VerbUnit Unit, UnitState Before, UnitState After)> Changes()
    {
        var now = VerbCourse.States(kit, progress);
        var list = new List<(VerbUnit, UnitState, UnitState)>();
        foreach (var u in kit.Units)
        {
            var b = startStates[u.Id];
            var a = now[u.Id];
            if (a.Rule != b.Rule || a.Make != b.Make || a.Read != b.Read || a.Status != b.Status) list.Add((u, b, a));
        }
        return list;
    }

    /// <summary>"3 of 25"</summary>
    public string PositionText => $"{AnsweredCount + (Answered ? 0 : 1)} of {Math.Max(Planned, AnsweredCount + 1)}";
}
