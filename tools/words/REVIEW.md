# Review brief: check and fix Spanish flashcard entries

You are the second, independent reviewer of entries for a Spanish vocabulary app for English speakers. Another
writer produced them following BRIEF.md (same folder: read it for the field rules). Your job is to find and FIX
mistakes. Be a careful, native-level Spanish editor.

Files in the folder:
- `existing_words.txt`: the app's current 1000 words (Spanish, part of speech, English). These must not change.
- `new_words.txt`: all 1000 new words being added (yours are a part of them).
- your input `reviewNN_in.json`: 100 entries.

Check every entry:
1. `ex` is correct, natural Spanish a native speaker would say (grammar, agreement, accents, ¿ ¡, punctuation,
   word choice). Neutral Spanish (no vosotros, no strong regionalisms). 3 to 10 words. Simple vocabulary.
2. `exEn` is an accurate, natural English translation of `ex`.
3. `en` gives the most common everyday meaning(s) first, short (1 to 3), verbs start with "to ". Correct any wrong
   or odd sense.
4. `pos`, and for nouns `g` and `art`, are right (feminine nouns with stressed a- take "el": el arma, g "f").
5. `form` is exactly the word as it appears in `ex` (case and inflection), appears exactly once, and the sentence
   still gives a clue when it is blanked out.
6. `topic` is a sensible choice from the allowed list (all verbs: "Everyday verbs").
7. SYNONYMS: the app also shows the English and asks for the Spanish, so two cards must not have the same
   English. If an entry's meaning is the same as (or nearly the same as) another word in `existing_words.txt` or
   `new_words.txt`, change THIS entry's `en` to tell them apart with a short note in parentheses, e.g.
   "husband (also esposo)", "there (near you)", "face (formal)", "hair (on your head, formal)",
   "computer (Spain)", "school (primary or secondary)". Only change the new entry, never the existing word.
8. No em dash (—) or en dash (–) anywhere.

Do not drop or reorder entries and do not change `rank` or `es` (unless `es` itself is clearly misspelled; then
fix it and say so). If an entry is so bad it should be removed from the app (offensive, not a real word, an exact
duplicate), add `"drop": "<reason>"` to it instead of deleting it.

Write the full corrected list (all 100 entries, same order) as JSON (UTF-8, not ASCII-escaped) to your output path,
then validate it with Python (valid JSON, 100 entries, same ranks in the same order, required fields present,
`form` occurs exactly once in `ex`, nouns have g/art and others don't, no dashes).

Reply with: how many entries you changed, then a compact list of the changes that matter (wrong meaning,
wrong gender, grammar errors, synonym notes added), one line each.
