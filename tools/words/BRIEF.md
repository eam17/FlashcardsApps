# Brief: write flashcard entries for a Spanish vocabulary app

The app ("Palabras") teaches English speakers the most common Spanish words as flashcards. It already has 1000 words
(`existing_words.txt`: Spanish, part of speech, English). You are writing entries for the next words, taken from a
frequency list built from film and TV subtitles. Your input file is a JSON list of candidates in frequency order:
`rank`, `lemma`, `pos` (n, v, adj, adv, pron, determiner, interj, prep, num), `gender` (nouns), `dict_meaning`
(a raw Wiktionary gloss, often messy or listing rare senses first) and sometimes a `note`.

## 1. Keep or skip each candidate

Skip a candidate (with a short reason) when it is:
- a duplicate or variant of a word already in `existing_words.txt` (e.g. "ninguno" when "ningún" is there;
  "ése"/"éste" accented spellings when "ese"/"este" exist; a feminine/plural form of a list word; "buena" = bueno);
- junk from the frequency list: interjection noises (oh, uh, eh, ah, mm), typos, non-words, odd lemmas
  ("tranquilar", "aforar"), Latin/English loans that are not really Spanish vocabulary (OK, club is fine though);
- vulgar or offensive (insults, slurs, sexual terms);
- a word that only exists inside a fixed phrase. For those, prefer CONVERTING to the phrase when it is common and
  useful and not already in the list: "embargo" → "sin embargo" (however), "supuesto" → "por supuesto"
  (of course). A phrase entry uses the phrase as `es` and the most fitting `pos` (adverb, interjection,
  preposition…).
- a noun that also is a participle/form of a list word (see `note`): KEEP it only if the noun meaning is a common
  word worth learning on its own (la vista = view, la mirada = look/gaze, la bebida = drink, la parada = stop,
  el significado = meaning). Otherwise skip.

Keep everything else, even if it sounds dramatic (asesino, crimen, policía are genuinely common). Most
candidates should be kept.

## 2. Write each kept entry

Match the style of the existing list exactly. Examples of existing entries:

{"es": "centro", "en": "center, downtown", "pos": "noun", "ex": "Vamos al centro.", "exEn": "Let's go downtown.", "g": "m", "art": "el", "topic": "Places & travel", "form": "centro"}
{"es": "tratar", "en": "to try; to treat", "pos": "verb", "ex": "Trato de aprender.", "exEn": "I try to learn.", "topic": "Everyday verbs", "form": "Trato"}
{"es": "costar", "en": "to cost", "pos": "verb", "ex": "¿Cuánto cuesta?", "exEn": "How much does it cost?", "topic": "Everyday verbs", "form": "cuesta"}
{"es": "próximo", "en": "next", "pos": "adjective", "ex": "Nos vemos la próxima semana.", "exEn": "See you next week.", "topic": "Time & calendar", "form": "próxima"}
{"es": "alrededor", "en": "around", "pos": "adverb", "ex": "Miré alrededor.", "exEn": "I looked around.", "topic": "Places & travel", "form": "alrededor"}
{"es": "perdón", "en": "sorry, excuse me", "pos": "interjection", "ex": "Perdón, no te vi.", "exEn": "Sorry, I didn't see you.", "topic": "Basics & grammar", "form": "Perdón"}
{"es": "agua", "en": "water", "pos": "noun", "g": "f", "art": "el", ...}   (feminine noun that takes "el")
{"es": "estudiante", "en": "student", "pos": "noun", "g": "mf", "art": "el / la", ...}   (same form for both)

Fields:
- `rank`: copy from the candidate (so entries can be put in frequency order).
- `es`: the dictionary form, lowercase: masculine singular for nouns and adjectives (for people nouns with
  -o/-a pairs use the masculine: "muchacho"), infinitive for verbs (reflexive "-se" only when the verb is mainly
  used that way, e.g. "quejarse"), or the phrase.
- `en`: the most common meaning(s) for a learner, short: 1 to 3 meanings, separated by ", " (or "; " for clearly
  different senses). Verbs start with "to ". Order by how common the sense is in everyday Spanish, NOT the
  dictionary order. Do not copy rare or odd senses from `dict_meaning`.
- `pos`: one of noun, verb, adjective, adverb, pronoun, preposition, number, determiner, conjunction,
  interjection.
- Nouns only: `g` ("m", "f" or "mf") and `art` ("el", "la", "el / la"; "el" for feminine nouns starting with a
  stressed a, like "el arma"; "los"/"las" only for plural-only nouns). Leave both out for every other part of
  speech. Double-check gender: the candidate `gender` is usually right but not always.
- `topic`: exactly one of: Everyday verbs (use this for ALL verbs), Basics & grammar, Places & travel,
  Work, school & tech, Home & things, Time & calendar, Feelings & personality, Food & drink, People & family,
  Numbers & amounts, Body & health, Nature, animals & weather, Society & ideas, Money & shopping,
  Free time & culture, Colors.
- `ex`: ONE short, natural, everyday Spanish sentence (3 to 8 words, never more than 10) that shows the main
  meaning. Use simple, common vocabulary and mostly present tense (past is fine when natural). Neutral
  Spanish understood everywhere (no vosotros, no regional slang). Keep it friendly and everyday; for words
  about crime or violence keep the sentence factual and mild ("La policía busca al ladrón.").
  Correct accents, ¿ ¡ and punctuation. Vary the sentences; don't start them all the same way.
- `exEn`: a natural English translation of `ex`.
- `form`: the exact text of the word as it appears in `ex` (same capitalisation and inflection, e.g. "Trato",
  "próxima", "cuesta"; for a phrase, the phrase). It must appear exactly once in `ex` as a whole word. It is
  blanked out on the card, so the rest of the sentence should still hint at it.
- Do NOT add a `conj` field.

Never use an em dash (—) or en dash (–) anywhere. Use straight apostrophes in English ("I'm").

## 3. Output

Write ONE JSON file (UTF-8, not ASCII-escaped) to the output path you are given:

{"keep": [ {entry}, ... ], "skip": [ {"rank": 123, "lemma": "ox", "reason": "noise"}, ... ]}

Every candidate must appear in exactly one of the two lists. Keep the input order. Then validate your file with a
short Python check (valid JSON, every kept entry has all required fields, `form` occurs in `ex`, nouns have g and
art, non-nouns don't, topic is from the list, no "—" or "–") and fix anything it finds. Finish by replying with
just: kept count, skipped count, and any entries you were unsure about.
