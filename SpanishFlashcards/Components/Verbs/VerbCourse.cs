using SpanishFlashcards.Models.Verbs;
using SpanishFlashcards.Services;

namespace SpanishFlashcards.Components.Verbs;

/// <summary>
/// The units and your progress in them: each unit's record and state, the current unit (the first one not solid
/// yet), reviews due, and the one-time move of older verb progress into the units.
/// </summary>
public static class VerbCourse
{
    public static UnitRecord? Find(Progress p, VerbUnit u) => p.VerbUnits.GetValueOrDefault(u.Id);

    /// <summary>The unit's record, created when missing.</summary>
    public static UnitRecord Record(Progress p, VerbUnit u)
    {
        if (!p.VerbUnits.TryGetValue(u.Id, out var r)) p.VerbUnits[u.Id] = r = new UnitRecord();
        return r;
    }

    /// <summary>Every unit to make and read is solid: read-first units (future, imperfect subjunctive) now want making too.</summary>
    public static bool LaterMake(VerbKit kit, Progress p) =>
        kit.Units.Where(u => u.Goal == UnitGoal.Use).All(u => UnitProgress.Evaluate(u, Find(p, u), false).Solid);

    public static UnitState State(VerbKit kit, Progress p, VerbUnit u, bool? laterMake = null) =>
        UnitProgress.Evaluate(u, Find(p, u), laterMake ?? LaterMake(kit, p));

    public static Dictionary<string, UnitState> States(VerbKit kit, Progress p)
    {
        var later = LaterMake(kit, p);
        return kit.Units.ToDictionary(u => u.Id, u => UnitProgress.Evaluate(u, Find(p, u), later));
    }

    /// <summary>The unit Continue works on: the first one that isn't solid (null when every unit is).</summary>
    public static VerbUnit? Current(VerbKit kit, Progress p)
    {
        var later = LaterMake(kit, p);
        return kit.Units.FirstOrDefault(u => !UnitProgress.Evaluate(u, Find(p, u), later).Solid);
    }

    /// <summary>A review key still points at a unit or verb that exists.</summary>
    public static bool Resolves(VerbKit kit, string key)
    {
        if (key.StartsWith("u:", StringComparison.Ordinal))
        {
            var colon = key.LastIndexOf(':');
            return colon > 2 && kit.Unit(key[2..colon]) is not null;
        }
        return VerbReviews.VerbOf(key) is { } vt && kit.Book.ByInf.TryGetValue(vt.Inf, out var v) && v.Has(vt.Tense);
    }

    public static List<string> DueKeys(VerbKit kit, Progress p, DateTime nowUtc) =>
        VerbReviews.DueKeys(p.VerbMemory, nowUtc).Where(k => Resolves(kit, k)).ToList();

    public static int DueCount(VerbKit kit, Progress p, DateTime nowUtc) =>
        p.VerbMemory.Count(kv => VerbReviews.IsDue(kv.Value, nowUtc) && Resolves(kit, kv.Key));

    /// <summary>
    /// A unit skipped as known failed its first review in one meter: back to learning in that meter (its reviews go on).
    /// </summary>
    public static void FallBack(Progress p, VerbUnit u, Meter meter)
    {
        var r = Record(p, u);
        r.Known = false;
        r.Solid = null;
        if (meter == Meter.Make)
        {
            r.MakeCredit = false;
            r.Cue.Clear();
            r.Context = 0;
        }
        else
        {
            r.ReadCredit = false;
            r.Decoded.Clear();
            r.Meaning = 0;
            r.Sentences = 0;
        }
        foreach (var m in new[] { Meter.Make, Meter.Read })
            foreach (var key in VerbReviews.KeysFor(u, m))
                if (p.VerbMemory.TryGetValue(key, out var mem)) mem.Confirm = false;
    }

    /// <summary>
    /// Once, the first time the units are used: verb progress from before them is moved in. Forms you practised count
    /// towards Make (strong ones and passed tests count it as done), decoded forms towards Read, and lessons passed on
    /// the old path count as having seen the idea. Lessons the old placement check skipped get the unit's own check.
    /// </summary>
    public static void Migrate(VerbKit kit, Progress p)
    {
        if (p.VerbUnitsMigrated) return;
        p.VerbUnitsMigrated = true;
        if (p.VerbSkills.Count == 0 && p.VerbLessons.Count == 0 && p.VerbTests.Count == 0) return;

        var now = DateTime.UtcNow;

        // Decoded forms (dec:verb|tense|person) by the unit they belong to.
        var decoded = new Dictionary<string, List<string>>();
        foreach (var (key, skill) in p.VerbSkills)
        {
            if (!key.StartsWith("dec:", StringComparison.Ordinal) || skill.Strength < 0.4) continue;
            var parts = key[4..].Split('|');
            if (parts.Length != 3 || !kit.Book.ByInf.TryGetValue(parts[0], out var v) || !VerbGrammar.TenseById.ContainsKey(parts[1])) continue;
            var persons = VerbGrammar.PersonsFor(parts[1]);
            var person = Enumerable.Range(0, persons.Count).FirstOrDefault(i => persons[i].Id == parts[2], -1);
            if (person < 0 || kit.UnitOf(v, parts[1], person) is not { } u) continue;
            if (!decoded.TryGetValue(u.Id, out var list)) decoded[u.Id] = list = new();
            list.Add(key[4..]);
        }

        foreach (var u in kit.Units.Where(x => x.HasForms))
        {
            var active = u.Active.ToList();
            var keys = active.Select(i => i.Form.Key).Distinct().ToList();
            var skills = keys.Select(k => p.VerbSkills.GetValueOrDefault(k)).ToList();
            var seen = skills.Count(VerbSrs.IsSeen);
            var strong = skills.Count(s => s is { Strength: >= 0.7 });
            var tested = u.Nodes.Count > 0 && u.Nodes.All(n => p.VerbTests.TryGetValue(n.Id, out var t) && t.Passed);
            var passed = u.Nodes.Count > 0 && u.Nodes.All(n => p.VerbLessons.TryGetValue(n.Id, out var l) && !l.Placed);
            var dec = decoded.GetValueOrDefault(u.Id) ?? new List<string>();
            if (seen == 0 && !tested && !passed && dec.Count == 0) continue;

            var r = Record(p, u);
            r.Started ??= now;
            if (tested || (keys.Count > 0 && strong >= 0.7 * keys.Count))
            {
                r.MakeCredit = true;
                r.IdeaSeen = true;
                r.Checked = true;
            }
            else if (passed || seen > 0)
            {
                r.IdeaSeen = true;
                r.Checked = true;
                r.Built = true;
                foreach (var i in active.Where(i => !i.Form.IsPattern && i.Verb is null))
                    if (p.VerbSkills.GetValueOrDefault(i.Form.Key) is { Strength: >= 0.4 } && !r.Cue.Contains(i.Form.Key))
                        r.Cue.Add(i.Form.Key);
            }
            foreach (var k in dec.Distinct()) if (!r.Decoded.Contains(k)) r.Decoded.Add(k);
            if (dec.Count > 0) r.IdeaSeen = true;
            if (tested)
            {
                r.ReadCredit = true;
                r.Known = true;
            }

            var st = UnitProgress.Evaluate(u, r, false);
            if (st.Solid)
            {
                r.Solid = now;
                VerbReviews.Start(p.VerbMemory, u, st.MakeRequired, true, r.Known, now);
            }
        }

        // Moved progress means you've started: Continue picks up from it (each unit still has its own check).
        if (p.VerbUnits.Count > 0) p.VerbsStarted = true;
    }
}
