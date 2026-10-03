"""Next candidate words for words.json, most frequent first.

Takes the Read tab's dictionary (frequency-ranked) and skips words already in words.json, forms and
participles of list words (unless the dictionary has them as nouns: la vista, la bebida), contractions,
letters, non-words and vulgar words. Writes candidates.json for the writing step (see README.md).

    python tools/words/pick_candidates.py 1500 > /tmp/candidates.json
"""
import json, os, re, sys

DATA = os.path.join(os.path.dirname(__file__), "..", "..", "SpanishFlashcards", "wwwroot", "data")
VULGAR = {"mierda", "puta", "puto", "joder", "coño", "cabrón", "cabrona", "culo", "pendejo", "pinche", "carajo",
          "chingar", "maricón", "gilipollas", "hostia", "follar", "polla", "zorra", "marica", "verga", "perra",
          "maldito", "maldita", "bastardo", "imbécil", "idiota", "jodido", "cojones", "huevón", "mamar", "culero",
          "chingado", "pito", "teta", "tetas", "pija", "concha", "cagar", "mear", "prostituta", "estúpido"}
PHRASE_ONLY = {"través", "menudo", "repente"}  # only inside list phrases (a través de, a menudo, de repente)


def main(count):
    words = json.load(open(os.path.join(DATA, "words.json"), encoding="utf-8"))
    have = {v.lower() for w in words for v in w["es"].split(" / ")}
    rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(DATA, "dict.tsv"), encoding="utf-8")]
    forms = {}
    for r in rows:
        if r[0].lower() in have and len(r) > 4 and r[4]:
            for f in r[4].split(","):
                forms[f.lower()] = r[0]
    pp = {}
    for v in json.load(open(os.path.join(DATA, "verbs.json"), encoding="utf-8"))["verbs"]:
        p = v.get("pp", "").split("|")[0]
        if p.endswith("o"):
            for f in (p, p[:-1] + "a", p[:-1] + "os", p[:-1] + "as"):
                pp[f] = v["inf"]
    out = []
    for i, r in enumerate(rows):
        lemma, pos = r[0], r[1]
        low = lemma.lower()
        if low in have or pos in ("contraction", "letter", "art", "name"):
            continue
        if not re.fullmatch(r"[a-záéíóúüñ]+", lemma) or len(lemma) < 2 or low in VULGAR or low in PHRASE_ONLY:
            continue
        note = ""
        if low in forms:
            if pos != "n":
                continue
            note = f'also a form of the list word "{forms[low]}"'
        elif low in pp:
            if pos != "n":
                continue
            note = f'also the participle of the list verb "{pp[low]}"'
        out.append({"rank": i + 1, "lemma": lemma, "pos": pos, "gender": r[2], "dict_meaning": r[3], "note": note})
        if len(out) >= count:
            break
    json.dump(out, sys.stdout, ensure_ascii=False, indent=0)


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 1500)
