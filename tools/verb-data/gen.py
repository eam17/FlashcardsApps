"""
Generates wwwroot/data/verbs.json for Palabras.

Every form is produced by explicit rules (regular endings + per-verb features such as stem changes,
irregular yo forms, irregular preterite / future stems and participles). Each person also gets a
reason code ("why this form isn't the plain pattern"), which the app uses to build its tree.
check.py then compares every form with verbecc (an independent conjugation library).
"""
import json, re, sys, unicodedata

PERSONS = ["yo", "tu", "el", "nos", "vos", "ellos"]
CMD_PERSONS = ["tu", "tu-neg", "usted", "nos", "vos", "vos-neg", "ustedes"]
SIMPLE_TENSES = ["pres", "pret", "impf", "fut", "cond", "subj", "cmd", "impsubj"]

ACC = {"a": "á", "e": "é", "i": "í", "o": "ó", "u": "ú"}
UNACC = {v: k for k, v in ACC.items()}
VOWELS = "aeiouáéíóú"

def is_uir(inf):
    return inf.endswith("uir") and not inf.endswith("guir")

def strip_acc(s):
    return "".join(UNACC.get(c, c) for c in s)

# ----------------------------------------------------------------------------------------------
# Verb features
# ----------------------------------------------------------------------------------------------

STEM = {  # present-tense stem change; for -ir verbs the second value is the preterite/-ir change
    **{v: ("e-ie", None) for v in ["querer", "pensar", "empezar", "entender", "perder", "comenzar", "cerrar",
                                   "despertar", "sentar", "defender", "nevar", "encender", "tener", "venir",
                                   "mantener", "obtener"]},
    **{v: ("e-ie", "e-i") for v in ["preferir", "sentir", "convertir", "divertir"]},
    **{v: ("o-ue", None) for v in ["poder", "encontrar", "volver", "contar", "recordar", "costar", "acordar",
                                   "mover", "soñar", "doler", "probar", "llover", "volar", "mostrar"]},
    **{v: ("o-ue", "o-u") for v in ["morir", "dormir"]},
    **{v: ("e-i", "e-i") for v in ["pedir", "seguir", "conseguir", "servir", "repetir", "vestir", "elegir", "decir"]},
    "jugar": ("u-ue", None),
}
YO_GO = {"tener": "tengo", "venir": "vengo", "decir": "digo", "hacer": "hago", "poner": "pongo", "salir": "salgo",
         "traer": "traigo", "caer": "caigo", "mantener": "mantengo", "obtener": "obtengo", "suponer": "supongo"}
ZCO = ["conocer", "parecer", "producir", "aparecer", "ofrecer", "pertenecer", "crecer", "nacer", "reconocer",
       "conducir", "traducir"]
ACCENT_HIATUS = {"enviar": "i", "continuar": "u"}  # envío, continúo
PRET_STEM = {  # irregular preterite stems
    "tener": ("u-stem", "tuv"), "mantener": ("u-stem", "mantuv"), "obtener": ("u-stem", "obtuv"),
    "estar": ("u-stem", "estuv"), "andar": ("u-stem", "anduv"), "poder": ("u-stem", "pud"),
    "poner": ("u-stem", "pus"), "suponer": ("u-stem", "supus"), "saber": ("u-stem", "sup"), "haber": ("u-stem", "hub"),
    "hacer": ("i-stem", "hic"), "querer": ("i-stem", "quis"), "venir": ("i-stem", "vin"),
    "decir": ("j-stem", "dij"), "traer": ("j-stem", "traj"), "producir": ("j-stem", "produj"),
    "conducir": ("j-stem", "conduj"), "traducir": ("j-stem", "traduj"),
}
PRET_Y = ["leer", "creer", "caer", "construir"]  # (oír is fully irregular here)
FUT_STEM = {
    "poder": ("drop-e", "podr"), "saber": ("drop-e", "sabr"), "querer": ("drop-e", "querr"), "haber": ("drop-e", "habr"),
    "tener": ("d-stem", "tendr"), "mantener": ("d-stem", "mantendr"), "obtener": ("d-stem", "obtendr"),
    "poner": ("d-stem", "pondr"), "suponer": ("d-stem", "supondr"), "salir": ("d-stem", "saldr"),
    "venir": ("d-stem", "vendr"),
    "decir": ("unique-fut", "dir"), "hacer": ("unique-fut", "har"),
}
PARTICIPLE = {
    "abrir": "abierto", "escribir": "escrito", "hacer": "hecho", "decir": "dicho", "poner": "puesto", "ver": "visto",
    "volver": "vuelto", "morir": "muerto", "romper": "roto", "descubrir": "descubierto", "suponer": "supuesto",
}
TU_CMD = {"decir": "di", "hacer": "haz", "ir": "ve", "poner": "pon", "salir": "sal", "ser": "sé", "tener": "ten",
          "venir": "ven", "mantener": "mantén", "obtener": "obtén", "suponer": "supón"}
# Verbs whose commands make no real sense (impersonal, "gustar"-type, auxiliaries): no command forms.
NO_CMD = ["haber", "poder", "llover", "nevar", "doler", "gustar", "encantar", "importar", "faltar", "sobrar",
          "ocurrir", "resultar", "significar", "pertenecer", "costar", "depender", "existir", "parecer",
          "aparecer", "deber", "preocupar"]
WEATHER = ["llover", "nevar"]  # only "it" forms

# Fully irregular verbs per tense: every person is tracked for these.
IRREGULAR = {
    "pres": ["ser", "estar", "ir", "haber", "dar", "saber", "ver", "oír", "reír", "sonreír"],
    "pret": ["ser", "ir", "dar", "ver", "oír", "reír", "sonreír"],
    "impf": ["ser", "ir", "ver"],
    "fut": ["oír", "reír", "sonreír"],
    "cond": ["oír", "reír", "sonreír"],
    "subj": ["ser", "estar", "ir", "haber", "dar", "saber", "ver", "oír", "reír", "sonreír"],
    "cmd": ["ser", "estar", "ir", "dar", "saber", "ver", "oír", "reír", "sonreír"],
    "impsubj": ["ser", "ir", "dar", "oír", "reír", "sonreír"],
}

# Fully hard-coded verbs (simple tenses), in PERSONS order; cmd in CMD_PERSONS order.
HARD = {
    "ser": dict(pres="soy eres es somos sois son", pret="fui fuiste fue fuimos fuisteis fueron",
                impf="era eras era éramos erais eran", subj="sea seas sea seamos seáis sean",
                cmd="sé seas sea seamos sed seáis sean", pp="sido"),
    "estar": dict(pres="estoy estás está estamos estáis están", subj="esté estés esté estemos estéis estén",
                  cmd="está estés esté estemos estad estéis estén"),
    "ir": dict(pres="voy vas va vamos vais van", pret="fui fuiste fue fuimos fuisteis fueron",
               impf="iba ibas iba íbamos ibais iban", subj="vaya vayas vaya vayamos vayáis vayan",
               cmd="ve vayas vaya vamos|vayamos id vayáis vayan", pp="ido"),
    "haber": dict(pres="he has ha hemos habéis han", subj="haya hayas haya hayamos hayáis hayan"),
    "dar": dict(pres="doy das da damos dais dan", pret="di diste dio dimos disteis dieron",
                subj="dé des dé demos deis den", cmd="da des dé demos dad deis den"),
    "saber": dict(pres="sé sabes sabe sabemos sabéis saben", subj="sepa sepas sepa sepamos sepáis sepan",
                  cmd="sabe sepas sepa sepamos sabed sepáis sepan"),
    "ver": dict(pres="veo ves ve vemos veis ven", pret="vi viste vio vimos visteis vieron",
                impf="veía veías veía veíamos veíais veían", subj="vea veas vea veamos veáis vean",
                cmd="ve veas vea veamos ved veáis vean", pp="visto"),
    "oír": dict(pres="oigo oyes oye oímos oís oyen", pret="oí oíste oyó oímos oísteis oyeron",
                impf="oía oías oía oíamos oíais oían", fut="oiré oirás oirá oiremos oiréis oirán",
                cond="oiría oirías oiría oiríamos oiríais oirían", subj="oiga oigas oiga oigamos oigáis oigan",
                cmd="oye oigas oiga oigamos oíd oigáis oigan", impsubj="oyera oyeras oyera oyéramos oyerais oyeran",
                pp="oído"),
    "reír": dict(pres="río ríes ríe reímos reís ríen", pret="reí reíste rio|rió reímos reísteis rieron",
                 impf="reía reías reía reíamos reíais reían", fut="reiré reirás reirá reiremos reiréis reirán",
                 cond="reiría reirías reiría reiríamos reiríais reirían", subj="ría rías ría riamos riais|riáis rían",
                 cmd="ríe rías ría riamos reíd riais|riáis rían", impsubj="riera rieras riera riéramos rierais rieran",
                 pp="reído"),
}
HARD["sonreír"] = {t: " ".join("son" + f.split("|")[-1] for f in v.split()) for t, v in HARD["reír"].items()}
# sonrió / sonriáis: two syllables, so they keep the accent that monosyllabic rio / riais drop

REFLEXIVE = {"acordarse": "acordar", "mudarse": "mudar", "llamarse": "llamar", "sentirse": "sentir",
             "irse": "ir", "quedarse": "quedar"}
REFL_PRON = ["me", "te", "se", "nos", "os", "se"]

# ----------------------------------------------------------------------------------------------
# Spelling helpers
# ----------------------------------------------------------------------------------------------

def soft_before_e(stem):
    """Keep the sound of the stem's last consonant before e (busc+é → busqué)."""
    if stem.endswith("c"): return stem[:-1] + "qu", "car-que"
    if stem.endswith("g") : return stem + "u", "gar-gue"
    if stem.endswith("z"): return stem[:-1] + "c", "zar-ce"
    return stem, None

def soft_before_a(stem, inf):
    """-ger/-gir: cog+o → cojo. -guir: segu+o → sigo."""
    if inf.endswith(("ger", "gir")): return stem[:-1] + "j", "g-j"
    if inf.endswith("guir"): return stem[:-1], "gu-g"
    return stem, None

def change_stem(stem, kind):
    """Apply e→ie / o→ue / e→i / o→u / u→ue to the stem's last syllable vowel."""
    src = {"e-ie": "e", "e-i": "e", "o-ue": "o", "o-u": "o", "u-ue": "u"}[kind]
    dst = {"e-ie": "ie", "e-i": "i", "o-ue": "ue", "o-u": "u", "u-ue": "ue"}[kind]
    # skip the silent u of gu/qu at the end (seguir → segu)
    s = stem
    end = len(s)
    if s.endswith(("gu", "qu")) and src != "u": end -= 1
    i = s.rfind(src, 0, end)
    if i < 0: raise ValueError(f"no {src} in {stem}")
    return s[:i] + dst + s[i + 1:]

def accent_last_vowel(s):
    for i in range(len(s) - 1, -1, -1):
        if s[i] in "aeiou":
            return s[:i] + ACC[s[i]] + s[i + 1:]
    return s

# ----------------------------------------------------------------------------------------------
# Regular endings
# ----------------------------------------------------------------------------------------------

END = {
    ("pres", "ar"): "o as a amos áis an", ("pres", "er"): "o es e emos éis en", ("pres", "ir"): "o es e imos ís en",
    ("pret", "ar"): "é aste ó amos asteis aron", ("pret", "er"): "í iste ió imos isteis ieron",
    ("pret", "ir"): "í iste ió imos isteis ieron",
    ("impf", "ar"): "aba abas aba ábamos abais aban", ("impf", "er"): "ía ías ía íamos íais ían",
    ("impf", "ir"): "ía ías ía íamos íais ían",
    ("subj", "ar"): "e es e emos éis en", ("subj", "er"): "a as a amos áis an", ("subj", "ir"): "a as a amos áis an",
}
FUT_END = "é ás á emos éis án".split()
COND_END = "ía ías ía íamos íais ían".split()

def ending_class(inf):
    base = inf[:-2] if inf.endswith("se") else inf
    return {"ar": "ar", "er": "er", "ir": "ir", "ír": "ir"}[base[-2:]]

def regular(inf, tense):
    """The plain pattern with no spelling or stem rules at all (used as the 'regularised' form)."""
    cls = ending_class(inf)
    stem = inf[:-2]
    if tense in ("fut", "cond"):
        return [inf + e for e in (FUT_END if tense == "fut" else COND_END)]
    if tense == "impsubj":
        pret = regular(inf, "pret")
        return impsubj_from(pret[5])
    if tense == "cmd":
        pres, subj = regular(inf, "pres"), regular(inf, "subj")
        return cmd_from(inf, pres, subj)
    return [stem + e for e in END[(tense, cls)].split()]

def impsubj_from(ellos_pret):
    base = ellos_pret[:-3]  # hablaron → habla
    return [base + "ra", base + "ras", base + "ra", accent_last_vowel(base) + "ramos", base + "rais", base + "ran"]

def cmd_from(inf, pres, subj, tu=None):
    return [tu or pres[2], subj[1], subj[2], subj[3], inf[:-1] + "d", subj[4], subj[5]]

# ----------------------------------------------------------------------------------------------
# Generator: returns forms and a reason per person (None = follows the plain pattern)
# ----------------------------------------------------------------------------------------------

def gen(inf):
    """Simple tenses for a non-reflexive verb. Returns {tense: [(form, reason), ...]} plus pp."""
    cls = ending_class(inf)
    stem = inf[:-2]
    if inf.endswith("ír"): stem = inf[:-2]
    out = {}
    stem_kind, ir_kind = STEM.get(inf, (None, None))
    weather = inf in WEATHER

    # ----- present -----
    pres = []
    for p, e in zip(PERSONS, END[("pres", cls)].split()):
        boot = p in ("yo", "tu", "el", "ellos")
        s, why = stem, None
        if boot and stem_kind:
            s, why = change_stem(stem, stem_kind), stem_kind
        if inf in ACCENT_HIATUS and boot:
            v = ACCENT_HIATUS[inf]
            s, why = s[:-1] + ACC[v], "accent"
        if is_uir(inf) and p != "nos" and p != "vos":
            s, why = s + "y", "uir-y"
        form = s + e
        if p == "yo":
            if inf in YO_GO:
                form, why = YO_GO[inf], "go"
            elif inf in ZCO:
                form, why = stem[:-1] + "zco", "zco"
            else:
                s2, sp = soft_before_a(s, inf)
                if sp:
                    form = s2 + e
                    why = why or sp
        pres.append((form, why))
    out["pres"] = pres

    # ----- preterite -----
    pret = []
    if inf in PRET_STEM:
        kind, ps = PRET_STEM[inf]
        ends = "e iste o imos isteis ieron".split()
        if kind == "j-stem": ends[5] = "eron"
        for p, e in zip(PERSONS, ends):
            f = ps + e
            if inf == "hacer" and p == "el": f = "hizo"
            pret.append((f, kind))
    else:
        for p, e in zip(PERSONS, END[("pret", cls)].split()):
            s, why = stem, None
            if p == "yo" and cls == "ar":
                s, why = soft_before_e(stem)
            if ir_kind and p in ("el", "ellos"):
                s, why = change_stem(stem, ir_kind), ir_kind
            f = s + e
            if inf in PRET_Y or is_uir(inf):
                if p in ("el", "ellos"):
                    f, why = s + e.replace("i", "y", 1), "y"
                elif not is_uir(inf) and e.startswith("i") and p != "yo":
                    f, why = s + "í" + e[1:], "y"
            pret.append((f, why))
    out["pret"] = pret

    # ----- imperfect -----
    out["impf"] = [(stem + e, None) for e in END[("impf", cls)].split()]

    # ----- future / conditional -----
    for t, ends in (("fut", FUT_END), ("cond", COND_END)):
        if inf in FUT_STEM:
            kind, fs = FUT_STEM[inf]
            out[t] = [(fs + e, kind) for e in ends]
        else:
            out[t] = [(inf + e, None) for e in ends]

    # ----- present subjunctive (from the yo form) -----
    yo = pres[0][0]
    subj = []
    se = "e" if cls == "ar" else "a"
    for p, e in zip(PERSONS, END[("subj", cls)].split()):
        boot = p in ("yo", "tu", "el", "ellos")
        why = None
        if inf in YO_GO or inf in ZCO:
            s = yo[:-1]
            why = "go" if inf in YO_GO else "zco"
            if inf in STEM and p in ("nos", "vos") and STEM[inf][1]:
                pass  # decir: digamos (still from yo form)
        else:
            s = stem
            if stem_kind and boot:
                s, why = change_stem(stem, stem_kind), stem_kind
            elif ir_kind and p in ("nos", "vos"):
                s = change_stem(stem, ir_kind)
                why = "ir-nos" if ir_kind != stem_kind else stem_kind
            if inf in ACCENT_HIATUS and boot:
                s, why = s[:-1] + ACC[ACCENT_HIATUS[inf]], "accent"
            if is_uir(inf):
                s, why = s + "y", "uir-y"
            if cls == "ar":
                s2, sp = soft_before_e(s)
            else:
                s2, sp = soft_before_a(s, inf)
            if sp:
                s = s2
                why = why or sp
        subj.append((s + e, why))
    out["subj"] = subj

    # ----- commands -----
    if inf not in NO_CMD:
        tu = TU_CMD.get(inf)
        pf, sf = [f for f, _ in pres], [f for f, _ in subj]
        forms = cmd_from(inf, pf, sf, tu)
        reasons = [("tu-irr" if tu else pres[2][1]), subj[1][1], subj[2][1], subj[3][1], None, subj[4][1], subj[5][1]]
        out["cmd"] = list(zip(forms, reasons))

    # ----- imperfect subjunctive (from the preterite ellos form) -----
    ellos = pret[5]
    out["impsubj"] = [(f, ellos[1]) for f in impsubj_from(ellos[0])]

    # ----- participle -----
    if inf in PARTICIPLE:
        pp, ppw = PARTICIPLE[inf], "pp-irr"
    else:
        pp = stem + ("ado" if cls == "ar" else "ido")
        ppw = None
        if cls != "ar" and stem and stem[-1] in "aeo":
            pp, ppw = stem + "ído", "pp-accent"
    out["pp"] = (pp, ppw)

    # ----- hard-coded verbs override -----
    if inf in HARD:
        for t, v in HARD[inf].items():
            if t == "pp":
                out["pp"] = (v, None if v in ("sido", "ido") else out["pp"][1])
            else:
                reg = regular(inf, t)
                out[t] = [(f, "irr" if inf in IRREGULAR.get(t, []) or f.split("|")[0] != r else None)
                          for f, r in zip(v.split(), reg)]
        if inf in ("haber",):
            out.pop("cmd", None)
        if inf == "haber":
            out["pret"] = [(f, "u-stem") for f in "hube hubiste hubo hubimos hubisteis hubieron".split()]
        # derived tenses for hard-coded verbs follow their own (hard-coded) preterite
        if "pret" in HARD[inf] and "impsubj" not in HARD[inf]:
            reg = regular(inf, "impsubj")
            out["impsubj"] = [(f, "irr" if inf in IRREGULAR["impsubj"] or f != r else None)
                              for f, r in zip(impsubj_from(out["pret"][5][0]), reg)]
        if "subj" in HARD[inf] and "cmd" not in HARD[inf] and "cmd" in out:
            sf = [f for f, _ in out["subj"]]
            pf = [f for f, _ in out["pres"]]
            out["cmd"] = list(zip(cmd_from(inf, pf, sf, TU_CMD.get(inf)), ["irr"] * 7))

    # Whole-verb irregular tenses: every person tracked
    for t, verbs in IRREGULAR.items():
        if inf in verbs and t in out:
            out[t] = [(f, "irr") for f, _ in out[t]]

    if weather:
        for t in SIMPLE_TENSES:
            if t in out:
                out[t] = [(f if p == "el" else None, w) for (f, w), p in zip(out[t], PERSONS)]
        out.pop("cmd", None)
    return out

# ----------------------------------------------------------------------------------------------
# Reflexive verbs: pronoun before the verb, attached (with an accent when needed) to commands
# ----------------------------------------------------------------------------------------------

def syllable_nuclei(word):
    """Indices of vowel nuclei (strong vowels split, weak+vowel diphthongs merge)."""
    w = word.lower()
    nuclei = []
    i = 0
    strong = "aeoáéíóú"  # accented í/ú act as strong
    while i < len(w):
        if w[i] in VOWELS:
            j = i
            while j + 1 < len(w) and w[j + 1] in VOWELS:
                a, b = w[j], w[j + 1]
                if a in strong and b in strong: break
                j += 1
            # silent u in gu/qu before e/i
            if w[i] == "u" and i > 0 and w[i - 1] in "gq" and j > i:
                pass
            nuclei.append((i, j))
            i = j + 1
        else:
            i += 1
    # drop the silent u of gue/gui/que/qui
    fixed = []
    for (a, b) in nuclei:
        if w[a] == "u" and a > 0 and w[a - 1] in "gq" and b > a:
            fixed.append((a + 1, b))
        else:
            fixed.append((a, b))
    return fixed

def stressed_nucleus(word):
    nuc = syllable_nuclei(word)
    for k, (a, b) in enumerate(nuc):
        if any(c in "áéíóú" for c in word[a:b + 1]): return k, nuc
    if len(nuc) == 1: return 0, nuc
    if word[-1] in "aeiouns": return len(nuc) - 2, nuc
    return len(nuc) - 1, nuc

def attach(form, pron):
    """múd + ate: put the pronoun on the end and keep the original stress (with an accent if needed)."""
    k, nuc = stressed_nucleus(form)
    new = form + pron
    if any(c in "áéíóú" for c in form):
        return new
    nuc2 = syllable_nuclei(new)
    # natural stress of the new word: second-to-last syllable (it ends in a vowel or s)
    natural = len(nuc2) - 2 if new[-1] in "aeiouns" else len(nuc2) - 1
    if natural == k: return new
    a, b = nuc[k]
    seg = form[a:b + 1]
    # accent the strong vowel of the nucleus (or the second weak one: "ui" → uí)
    pos = None
    for idx, c in enumerate(seg):
        if c in "aeo": pos = a + idx; break
    if pos is None: pos = b
    return new[:pos] + ACC[new[pos]] + new[pos + 1:]

def reflexive(inf):
    base = REFLEXIVE[inf]
    g = gen(base)
    out = {}
    for t in SIMPLE_TENSES:
        if t not in g: continue
        if t == "cmd":
            f = [x for x, _ in g["cmd"]]
            if base == "ir":
                f = ["vete", "te vayas", "váyase", "vámonos", "idos|iros", "os vayáis", "váyanse"]
            else:
                tu, tuneg, ud, nos, vos, vosneg, uds = f
                nos1 = nos.split("|")[0]
                f = [attach(tu, "te"), "te " + tuneg, attach(ud, "se"), attach(nos1[:-1], "nos"),
                     vos[:-2] + "íos" if vos.endswith("id") else vos[:-1] + "os", "os " + vosneg, attach(uds, "se")]
            out[t] = [(x, "refl") for x in f]
        else:
            out[t] = [("|".join(pr + " " + a for a in x.split("|")) if x else None, "refl")
                      for (x, _), pr in zip(g[t], REFL_PRON)]
    out["pp"] = (g["pp"][0], "refl")
    return out

def sentir_vos_fix(inf, out):
    return out

# ----------------------------------------------------------------------------------------------

VERB_TAB_WORDS = 1000

def main(words_path, out_path):
    words = json.load(open(words_path, encoding="utf-8"))
    # The Verbs tab covers the verbs among the first 1000 words (222 verbs). Later words (1001 and up) have no
    # irregular-verb features here, so they are left out; their cards get a short conjugation line in words.json.
    verbs = [w for w in words[:VERB_TAB_WORDS] if w["pos"] == "verb"]
    result = []
    for w in verbs:
        inf = w["es"]
        data = reflexive(inf) if inf in REFLEXIVE else gen(inf)
        base = REFLEXIVE.get(inf, inf)
        entry = {"inf": inf, "en": w["en"]}
        if inf in REFLEXIVE: entry["refl"] = True
        if inf in WEATHER: entry["only3"] = True
        t_forms, t_why = {}, {}
        for t in SIMPLE_TENSES:
            if t not in data: continue
            t_forms[t] = [f for f, _ in data[t]]
            whys = [w_ if f is not None else None for f, w_ in data[t]]
            if any(whys): t_why[t] = whys
        entry["f"] = t_forms
        entry["pp"] = data["pp"][0]
        if data["pp"][1]: entry["ppWhy"] = data["pp"][1]
        entry["why"] = t_why
        # regularised forms (only where different) for near-miss options
        reg = {}
        if inf not in REFLEXIVE:
            for t in SIMPLE_TENSES:
                if t not in t_forms: continue
                r = regular(base, t)
                diff = [x if (f and x != f.split("|")[0]) else None for x, f in zip(r, t_forms[t])]
                if any(diff): reg[t] = diff
            rpp = base[:-2] + ("ado" if ending_class(base) == "ar" else "ido")
            if rpp != entry["pp"]: entry["regPp"] = rpp
        if reg: entry["reg"] = reg
        result.append(entry)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump({"verbs": result}, f, ensure_ascii=False, separators=(",", ":"))
    print(f"{len(result)} verbs → {out_path}")

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
