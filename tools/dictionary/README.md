# Dictionary for the Read tab

`build_dict.py` makes `SpanishFlashcards/wwwroot/data/dict.tsv`: about 17,000 Spanish headwords, most common
first, each with a short English meaning, part of speech, gender (nouns) and the forms seen for it
("casas", "dijeron", "buena"), so the Read tab can find the headword for any form.

```
git clone --depth 1 https://github.com/doozan/spanish_data.git /tmp/spanish_data
python tools/dictionary/build_dict.py /tmp/spanish_data
```

Sources and licences: see `SpanishFlashcards/wwwroot/data/dict-LICENSE.txt` (Wiktionary, CC BY-SA). The app
shows the credit under every text in the Read tab.
