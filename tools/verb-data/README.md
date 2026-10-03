# Verb data

`SpanishFlashcards/wwwroot/data/verbs.json` holds full conjugations for every verb among the first 1000 words of `words.json` (222 verbs). It is generated, not hand-edited. Verbs in words 1001 and up are not in the Verbs tab; their word cards still show a short conjugation line (`conj` in `words.json`), made from the [verbecc](https://github.com/bretttolbert/verbecc) conjugation tables.

## Regenerate

```bash
python3 tools/verb-data/gen.py SpanishFlashcards/wwwroot/data/words.json SpanishFlashcards/wwwroot/data/verbs.json
```

`gen.py` builds each form from explicit rules (regular endings plus per-verb features: stem changes, irregular yo forms, irregular preterite and future stems, irregular participles, spelling changes). It also stores, for each form, *why* it differs from the plain pattern (`why`), which the app uses to build the Verbs tree. To add a verb to the Verbs tab, put it among the first 1000 words of `words.json` (or raise `VERB_TAB_WORDS`), then add it to the right feature lists in `gen.py` if it is irregular in any way.

Stored per verb: the simple tenses (`pres`, `pret`, `impf`, `fut`, `cond`, `subj`, `cmd`, `impsubj`) and the participle (`pp`). The app builds the compound tenses from haber + participle, and the *-se* imperfect subjunctive from the *-ra* form. `a|b` means both answers are accepted (the first is shown).

## Check against verbecc

[verbecc](https://github.com/bretttolbert/verbecc) is an independent Spanish conjugation library. With it on your `PYTHONPATH`:

```bash
python3 tools/verb-data/check.py SpanishFlashcards/wwwroot/data/verbs.json          # every form vs verbecc
python3 tools/verb-data/check_analog.py SpanishFlashcards/wwwroot/data/verbs.json   # verbs verbecc crashes on
python3 tools/verb-data/check_refl.py SpanishFlashcards/wwwroot/data/verbs.json     # reflexive commands (verbecc has none)
```

Last run (verbecc commit a5cab52, Aug 2026): 19,684 forms checked. All 186 disagreements are verbecc quirks where `verbs.json` has the standard form:

- **haber, él present:** verbecc gives impersonal *hay*; the app drills auxiliary *ha* (as in *ha comido*).
- **mantén, obtén:** verbecc drops the written accent.
- **compound tenses of comprender, elegir, despertar, nacer, salvar:** verbecc uses old adjectival participles (*he comprenso, he electo, he despierto, he nato, he salvo*); modern Spanish uses *comprendido, elegido, despertado, nacido, salvado*.
- verbecc crashes on the present, present subjunctive and commands of *pasar, resultar, nevar*; `check_analog.py` checks those against *hablar* / *pensar* instead (0 disagreements).

Reflexive commands (42 forms) match the hand-written list in `check_refl.py`.
