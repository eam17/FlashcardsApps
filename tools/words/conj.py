"""Short conjugation line for a word card ("yo hablo · tú hablas · él habla | Past: yo hablé · él habló"),
read straight from verbecc's conjugation tables (https://github.com/bretttolbert/verbecc, data/xml).

    git clone --depth 1 https://github.com/bretttolbert/verbecc.git /tmp/verbecc
    python tools/words/conj.py /tmp/verbecc/verbecc/data/xml/

With no further arguments it compares its output with the existing cards in words.json (210 of the 222 match;
the rest are written by hand: gustar-type verbs, weather verbs, haber, and reír's modern "rio"). Check the
output: verbecc uses the old accented "rió/crió" and doesn't know a few verbs; those are fixed by hand.
"""
import xml.etree.ElementTree as ET, json, os, sys

X = sys.argv[1] if len(sys.argv) > 1 else "verbecc/verbecc/data/xml/"
WORDS = os.path.join(os.path.dirname(__file__), "..", "..", "SpanishFlashcards", "wwwroot", "data", "words.json")

vt = {v.find("i").text: v.find("t").text for v in ET.parse(X + "verbs/verbs-es.xml").getroot()}
tpl = {}
for t in ET.parse(X + "conjugations/conjugations-es.xml").getroot():
    ind = t.find("Indicativo")

    def forms(tag):
        el = ind.find(tag)
        return [p.find("i").text if p.find("i") is not None else None for p in el.findall("p")] if el is not None else None

    tpl[t.get("name")] = (forms("presente"), forms("pretérito-perfecto-simple"))


def conj(inf):
    refl = inf.endswith("se") and inf[:-2] in vt
    base = inf[:-2] if refl else inf
    t = vt.get(base)
    if t is None:  # unknown verb, or listed only in its -se form (comportarse): write it by hand
        return None
    suffix = t.split(":")[1]
    stem = base[: len(base) - len(suffix)]
    pres, pret = tpl[t]
    if not pres or not pret or None in (pres[0], pres[1], pres[2], pret[0], pret[2]):
        return None
    f = lambda e: stem + e
    if refl:
        return f"me {f(pres[0])} · te {f(pres[1])} · se {f(pres[2])} | Past: me {f(pret[0])} · se {f(pret[2])}"
    return f"yo {f(pres[0])} · tú {f(pres[1])} · él {f(pres[2])} | Past: yo {f(pret[0])} · él {f(pret[2])}"


if __name__ == "__main__":
    words = json.load(open(WORDS, encoding="utf-8"))
    same = 0
    for w in words:
        if w["pos"] != "verb" or " " in w["es"]:
            continue
        c = conj(w["es"])
        if c == w.get("conj"):
            same += 1
        else:
            print(f"{w['es']}: card {w.get('conj')!r} / verbecc {c!r}")
    print(f"{same} match")
