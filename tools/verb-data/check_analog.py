"""For the few verbs verbecc crashes on, check those tenses against a same-pattern model verb."""
import json, sys, logging
logging.disable(logging.CRITICAL)
from verbecc import CompleteConjugator
cg = CompleteConjugator(lang="es")
ANALOG = {"pasar": ("hablar", [("habl", "pas")]), "resultar": ("hablar", [("habl", "result")]),
          "nevar": ("pensar", [("piens", "niev"), ("pens", "nev")])}
PR = {"yo": 0, "tú": 1, "él": 2, "nosotros": 3, "vosotros": 4, "ellos": 5}
verbs = {v["inf"]: v for v in json.load(open(sys.argv[1], encoding="utf-8"))["verbs"]}
bad = n = 0
for inf, (model, subs) in ANALOG.items():
    c = json.loads(str(cg.conjugate(model)))["moods"]
    def swap(f):
        for a, b in subs:
            if a in f: return f.replace(a, b)
        return f
    for t, (m, tn) in {"pres": ("indicativo", "presente"), "subj": ("subjuntivo", "presente")}.items():
        for r in c[m][tn]:
            i = PR.get(r["pr"])
            if i is None: continue
            ours = verbs[inf]["f"][t][i]
            if ours is None: continue
            ref = [swap(x.split(" ", 1)[1]) for x in r["c"]]
            n += 1
            if ours not in ref: bad += 1; print(inf, t, i, ours, ref)
    if "cmd" in verbs[inf]["f"]:
        aff = {r["pr"]: [swap(x) for x in r["c"]] for r in c["imperativo"]["afirmativo"]}
        neg = {r["pr"]: [swap(x[3:]) for x in r["c"]] for r in c["imperativo"]["negativo"]}
        ref = [aff["tú"], neg["tú"], aff["usted"], aff["nosotros"], aff["vosotros"], neg["vosotros"], aff["ustedes"]]
        for i, o in enumerate(verbs[inf]["f"]["cmd"]):
            n += 1
            if o not in ref[i]: bad += 1; print(inf, "cmd", i, o, ref[i])
print(f"analog check: {n} forms, {bad} disagreements")
