# Example stories

`SpanishFlashcards/wwwroot/data/stories.json` holds one short story per tense for the Verbs tab. It's generated from `stories_src.py`:

```bash
python3 tools/stories/build.py
```

## Writing or editing a story

- Put every conjugated verb in [brackets]: `Ayer Ana [perdió] el autobús.` The script finds its verb, tense and person in `verbs.json` (compound tenses like `[había salido]` work too).
- If a form could be more than one thing, the script stops and asks for a hint after a colon: the verb (`[creo:creer]`, `[fue:ir]`), the tense (`[habla:cmd]`, `[dice:pres]`) or the person (`[era:yo]`). When a form could be the story's own tense or another one, the story's tense wins; outside the commands story a narrated verb like `pregunta` counts as present, not a command.
- Everything else must be a word from `words.json` (plurals and feminine forms count), a small word in `SMALL_WORDS`, or a name in `NAMES`. The script lists any word that isn't, so stories stay within the app's vocabulary.
- Use « » for dialogue, so no dashes are needed.
- Add the English for every sentence.
