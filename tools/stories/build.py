"""
Builds SpanishFlashcards/wwwroot/data/stories.json from stories_src.py, and checks it.

  python3 tools/stories/build.py

For every [bracketed] verb it finds the verb, tense and person(s) in verbs.json (compound tenses and
reflexive forms included). It stops with an error when a form can't be found, or could be more than one
verb or tense and no hint says which. Every other word must be in words.json (plural / feminine forms
count), a small word from SMALL_WORDS below, or a name from NAMES.
"""
import json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
DATA = os.path.join(ROOT, "SpanishFlashcards", "wwwroot", "data")
sys.path.insert(0, HERE)
from stories_src import STORIES  # noqa: E402

TENSES = ["pres", "pret", "impf", "fut", "cond", "perf", "subj", "cmd", "impsubj",
          "plup", "futperf", "condperf", "subjperf", "plupsubj"]
COMPOUND = {"perf": "pres", "plup": "impf", "futperf": "fut", "condperf": "cond", "subjperf": "subj", "plupsubj": "impsubj"}
PERSONS = ["yo", "tu", "el", "nos", "vos", "ellos"]
CMD_PERSONS = ["tu", "tu-neg", "usted", "nos", "vos", "vos-neg", "ustedes"]
PRON = ["me", "te", "se", "nos", "os", "se"]

SMALL_WORDS = set("""
el la los las lo un una unos unas al del
mi mis tu tus su sus nuestra nuestras nuestros vuestro vuestra
me te se nos os le les ella ellas ellos él yo tú usted ustedes nosotros nosotras mí ti
que qué y e o u ni a de en con por para sin sobre entre desde hasta hacia
muy no sí más menos tan todo toda todos todas
este esta estos estas ese esa esos esas esto eso
otro otra otros otras mismo misma
ojalá
""".split())
NAMES = {"ana", "luis", "marta", "pablo"}


def load():
    words = json.load(open(os.path.join(DATA, "words.json"), encoding="utf-8"))
    verbs = json.load(open(os.path.join(DATA, "verbs.json"), encoding="utf-8"))["verbs"]
    return words, verbs


def build_index(verbs):
    """form (lower case) -> list of (infinitive, tense, person)."""
    index = {}
    haber = next(v for v in verbs if v["inf"] == "haber")["f"]

    def add(form, inf, tense, p):
        for alt in form.split("|"):
            index.setdefault(alt.lower(), []).append((inf, tense, p))

    for v in verbs:
        for t, forms in v["f"].items():
            for p, f in enumerate(forms):
                if f:
                    add(f, v["inf"], t, p)
        for t, ht in COMPOUND.items():
            for p in range(6):
                if v.get("only3") and p != 2:
                    continue
                aux = haber[ht][p].split("|")[0]
                pre = PRON[p] + " " if v.get("refl") else ""
                add(f"{pre}{aux} {v['pp']}", v["inf"], t, p)
    return index


def vocabulary(words, index):
    known = set()
    for w in words:
        for part in re.split(r"[ /,;()]+", w["es"].lower()):
            if part:
                known.add(part)
    for form in index:
        for part in form.split():
            known.add(part)
    return known


def is_known(word, known):
    w = word.lower()
    if w in known or w in SMALL_WORDS or w in NAMES or w.isdigit():
        return True
    tries = []
    if w.endswith("s"):
        tries += [w[:-1]]
    if w.endswith("es"):
        tries += [w[:-2]]
    if w.endswith("ces"):
        tries += [w[:-3] + "z"]
    for x in [w] + tries:
        if x.endswith("a"):
            tries += [x[:-1] + "o"]
    for x in tries:
        if x in known:
            return True
        # razones → razón: put the accent back on the last vowel
        for i in range(len(x) - 1, -1, -1):
            if x[i] in "aeiou":
                if x[:i] + "áéíóú"["aeiou".index(x[i])] + x[i + 1:] in known:
                    return True
                break
    return False


TOKEN = re.compile(r"\[([^\]]+)\]")


def resolve(text, hint, tense, index, verbs_by_inf, where):
    key = text.lower()
    cands = index.get(key, [])
    if not cands:
        sys.exit(f"{where}: can't find the verb form '{text}' in verbs.json")
    if hint:
        if hint in verbs_by_inf:
            cands = [c for c in cands if c[0] == hint]
        elif hint in TENSES:
            cands = [c for c in cands if c[1] == hint]
        elif hint in PERSONS or hint in CMD_PERSONS:
            cands = [c for c in cands if (CMD_PERSONS if c[1] == "cmd" else PERSONS)[c[2]] == hint]
        else:
            sys.exit(f"{where}: unknown hint '{hint}' for '{text}'")
        if not cands:
            sys.exit(f"{where}: no reading of '{text}' matches the hint '{hint}'")
    combos = sorted({(c[0], c[1]) for c in cands})
    # Outside the commands story, "pregunta" is "he asks", not "ask!".
    if len(combos) > 1 and tense != "cmd" and any(c[1] != "cmd" for c in combos):
        combos = [c for c in combos if c[1] != "cmd"]
    if len(combos) > 1:
        in_tense = [c for c in combos if c[1] == tense]
        if len(in_tense) == 1:
            combos = in_tense
        else:
            options = ", ".join(f"{i}/{t}" for i, t in combos)
            sys.exit(f"{where}: '{text}' could be {options}. Add a hint like [{text}:{combos[0][0]}] or [{text}:{combos[0][1]}]")
    inf, t = combos[0]
    persons = sorted({c[2] for c in cands if (c[0], c[1]) == (inf, t)})
    return {"x": text, "v": inf, "te": t, "p": persons}


def main():
    words, verbs = load()
    index = build_index(verbs)
    known = vocabulary(words, index)
    by_inf = {v["inf"]: v for v in verbs}
    out, unknown, counts = [], {}, {}

    seen_tenses = set()
    for story in STORIES:
        t = story["tense"]
        assert t in TENSES and t not in seen_tenses, f"bad or repeated tense {t}"
        seen_tenses.add(t)
        sentences = []
        for n, (es, en) in enumerate(story["sentences"], 1):
            where = f"{t} sentence {n}"
            tokens, pos = [], 0
            for m in TOKEN.finditer(es):
                if m.start() > pos:
                    tokens.append({"x": es[pos:m.start()]})
                inner = m.group(1)
                text, _, hint = inner.partition(":")
                tok = resolve(text, hint, t, index, by_inf, where)
                tokens.append(tok)
                if tok["te"] == t:
                    counts[t] = counts.get(t, 0) + 1
                pos = m.end()
            if pos < len(es):
                tokens.append({"x": es[pos:]})
            for tok in tokens:
                if "v" in tok:
                    continue
                for word in re.findall(r"[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+", tok["x"]):
                    if not is_known(word, known):
                        unknown.setdefault(word, []).append(where)
            sentences.append({"t": tokens, "en": en})
        out.append({"tense": t, "title": story["title"], "titleEn": story["titleEn"], "s": sentences})

    missing = [t for t in TENSES if t not in seen_tenses]
    if missing:
        print("No story yet for:", ", ".join(missing))
    if unknown:
        print("Words not in words.json (change them, or add small words to SMALL_WORDS):")
        for w, places in sorted(unknown.items()):
            print(f"  {w}: {', '.join(places)}")
        sys.exit(1)

    path = os.path.join(DATA, "stories.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"stories": out}, f, ensure_ascii=False, separators=(",", ":"))
    total = sum(len(s["s"]) for s in out)
    print(f"{len(out)} stories, {total} sentences -> {path}")
    for s in out:
        print(f"  {s['tense']:9} {len(s['s']):2} sentences, {counts.get(s['tense'], 0):2} verbs in the story's tense")


if __name__ == "__main__":
    main()
