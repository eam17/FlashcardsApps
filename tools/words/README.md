# Adding words to words.json

`SpanishFlashcards/wwwroot/data/words.json` is the word list; its order is the rank (word 1 = most common).
Progress is saved by the Spanish word (`es`), so adding words never touches anyone's progress. **Never change
the `es` of an existing word** (its progress would no longer show); fixing its meaning, example or gender is fine.

Words 1001 to 2000 were added in October 2026 like this:

1. **Pick candidates.** `python tools/words/pick_candidates.py 1500 > candidates.json` takes the next words by
   frequency from the Read tab's dictionary (`dict.tsv`, ranked from film and TV subtitles), skipping list words,
   their forms and participles, contractions, non-words and vulgar words.
2. **Write the entries** in batches of about 125 candidates, following `BRIEF.md` (keep or skip each candidate,
   then meaning, part of speech, gender and article, topic, example sentence and translation, and the form to
   blank out). Some candidates become phrases (embargo → *sin embargo*, supuesto → *por supuesto*).
3. **Review** every entry independently, following `REVIEW.md`: natural Spanish, correct meanings and genders,
   and different English for near-synonyms ("husband (also esposo)"), since cards also ask for the Spanish from
   the English.
4. **Take the first 1000** kept words by frequency, check them (form appears once in the example, nouns have
   gender and article, allowed topics, no duplicates, no em or en dashes) and append them to `words.json`.
5. **Verb cards** get a short conjugation line (`conj`) from `conj.py`, which reads verbecc's conjugation tables.
   Check its output and fix by hand the verbs it doesn't know or gets old-fashioned (*crio*, *-se* verbs it only
   lists reflexively, *gustar*-type verbs like *interesar*).

The Verbs tab (full conjugation, rules, practice) only covers the 222 verbs among the first 1000 words; see
`tools/verb-data/README.md`.
