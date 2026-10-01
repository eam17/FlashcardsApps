"""Compare every generated form in verbs.json with verbecc. Prints disagreements."""
import json, sys, logging
logging.disable(logging.CRITICAL)
from verbecc import CompleteConjugator

PR = {"yo": "yo", "tú": "tu", "él": "el", "nosotros": "nos", "vosotros": "vos", "ellos": "ellos"}
SIMPLE = {
    "pres": ("indicativo", "presente"), "pret": ("indicativo", "pretérito-perfecto-simple"),
    "impf": ("indicativo", "pretérito-imperfecto"), "fut": ("indicativo", "futuro"),
    "cond": ("condicional", "presente"), "subj": ("subjuntivo", "presente"),
    "impsubj": ("subjuntivo", "pretérito-imperfecto-1"),
}
SE = ("subjuntivo", "pretérito-imperfecto-2")
COMPOUND = {  # tense → (verbecc mood/tense, haber tense in our data)
    "perf": (("indicativo", "pretérito-perfecto-compuesto"), "pres"),
    "plup": (("indicativo", "pretérito-pluscuamperfecto"), "impf"),
    "futperf": (("indicativo", "futuro-perfecto"), "fut"),
    "condperf": (("condicional", "perfecto"), "cond"),
    "subjperf": (("subjuntivo", "pretérito-perfecto"), "subj"),
    "plupsubj": (("subjuntivo", "pretérito-pluscuamperfecto-1"), "impsubj"),
}
ORDER = ["yo", "tu", "el", "nos", "vos", "ellos"]
REFL = {"acordarse": "acordar", "mudarse": "mudar", "llamarse": "llamar", "sentirse": "sentir",
        "irse": "ir", "quedarse": "quedar"}
PRON = ["me ", "te ", "se ", "nos ", "os ", "se "]

def table(moods, mood, tense):
    rows = moods[mood][tense]
    out = {}
    for r in rows:
        key = PR.get(r.get("pr"))
        if key:
            words = [c.split(" ", 1)[1] if " " in c else c for c in r["c"]]
            out[key] = words
    return [out.get(p, []) for p in ORDER]

def ra_to_se(f):
    for a, b in (("ramos", "semos"), ("rais", "seis"), ("ras", "ses"), ("ran", "sen"), ("ra", "se")):
        if f.endswith(a): return f[: -len(a)] + b
    return f

def main(path):
    data = json.load(open(path, encoding="utf-8"))["verbs"]
    by = {v["inf"]: v for v in data}
    cg = CompleteConjugator(lang="es")
    haber = by["haber"]["f"]
    problems, checked = [], 0
    for v in data:
        inf = v["inf"]
        base = REFL.get(inf, inf)
        try:
            c = json.loads(str(cg.conjugate(base)))
        except Exception as e:
            problems.append((inf, "-", f"verbecc crashed: {type(e).__name__}: {e}"))
            continue
        if c["verb"].get("predicted"):
            problems.append((inf, "-", "verbecc only predicted this verb (not in its dictionary)"))
        moods = c["moods"]

        def ours(t, i):
            f = v["f"][t][i]
            if f is None: return None
            alts = f.split("|")
            if inf in REFL:
                alts = [a[len(PRON[i]):] if a.startswith(PRON[i]) else a for a in alts]
            return alts

        for t, (m, tn) in SIMPLE.items():
            if t not in v["f"]: continue
            ref = table(moods, m, tn)
            for i in range(6):
                o = ours(t, i)
                if o is None: continue
                checked += 1
                if not set(o) & set(ref[i]):
                    problems.append((inf, f"{t}.{ORDER[i]}", f"ours={o} verbecc={ref[i]}"))
        # -se imperfect subjunctive: our app derives it from -ra
        ref = table(moods, *SE)
        for i in range(6):
            o = ours("impsubj", i)
            if o is None: continue
            checked += 1
            if not {ra_to_se(x) for x in o} & set(ref[i]):
                problems.append((inf, f"impsubj-se.{ORDER[i]}", f"ours={[ra_to_se(x) for x in o]} verbecc={ref[i]}"))
        # compound tenses: haber (our data) + participle
        for t, ((m, tn), ht) in COMPOUND.items():
            ref = table(moods, m, tn)
            for i in range(6):
                if v["f"]["pres"][i] is None: continue
                checked += 1
                f = haber[ht][i] + " " + v["pp"]
                if f not in ref[i]:
                    problems.append((inf, f"{t}.{ORDER[i]}", f"ours={f} verbecc={ref[i]}"))
        # commands (non-reflexive only; reflexive commands are checked by hand in check_refl)
        if "cmd" in v["f"] and inf not in REFL:
            aff = {r["pr"]: r["c"] for r in moods["imperativo"]["afirmativo"]}
            neg = {r["pr"]: [x[3:] for x in r["c"]] for r in moods["imperativo"]["negativo"]}
            ref = [aff.get("tú", []), neg.get("tú", []), aff.get("usted", []), aff.get("nosotros", []),
                   aff.get("vosotros", []), neg.get("vosotros", []), aff.get("ustedes", [])]
            names = ["tu", "tu-neg", "usted", "nos", "vos", "vos-neg", "ustedes"]
            for i in range(7):
                o = v["f"]["cmd"][i].split("|")
                checked += 1
                if not set(o) & set(ref[i]):
                    problems.append((inf, f"cmd.{names[i]}", f"ours={o} verbecc={ref[i]}"))
    for p in problems: print(*p, sep=" | ")
    print(f"\nchecked {checked} forms, {len(problems)} disagreements")

main(sys.argv[1])
