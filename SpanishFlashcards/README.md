# Palabras: Spanish flashcards (Blazor WebAssembly)

Learn 1,000 common Spanish words as flashcards, and Spanish verb conjugation. The **Words** tab lists the words in a group, filtered by *To learn*, *Learned* or *All*. Progress is saved in your browser (localStorage).

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd SpanishFlashcards
dotnet run
```

Then open the URL it prints (e.g. http://localhost:5000).

Using .NET 8 or 9 instead? Change `net10.0` in `SpanishFlashcards.csproj` to `net8.0` / `net9.0` and the two package versions to `8.0.*` / `9.0.*`.

## Put it on your phone

```bash
dotnet publish -c Release
```

Upload the contents of `bin/Release/net10.0/publish/wwwroot` to any static host (GitHub Pages, Azure Static Web Apps, Netlify…). Open the site on your phone and choose **Add to Home Screen** and it launches full-screen like an app.

(If you host under a sub-path such as `/palabras/`, change `<base href="/" />` in `wwwroot/index.html` to match.)

## How studying works

- The **Cards** tab shows today's required reviews in the chosen group, then new words, then optional bonus reviews (due words over your daily limit). Tap **I know it** or **Still learning**.
  - **Multiple choice on the early levels:** new and level-1 cards show the Spanish word; pick the English meaning from 6. Level-2 cards show the English and the sentence with the word blanked out; pick the Spanish word from 6. From level 3 you recall the word yourself.
  - **Daily review limit** (Settings tab → *Daily reviews*, default 30): the most reviews you *have* to do each day. Anything over it is optional extra and rolls forward. New review dates are also spread over quieter days so reviews don't bunch up. You can still learn as many new words as you like.
  - **Tricky words:** words you forget repeatedly (2+ times) get a *tricky* badge and their own group in the picker.
  - **Answer timer:** a right answer only moves a word up if it's quick: by default 4 seconds when you answer in English and 6 seconds when you answer in Spanish (change or turn off in Settings → *Answer timer*) (for multiple choice until you tap an option, otherwise until you tap *I know it*). A slower right answer shows a small clock: the word keeps its level and comes back tomorrow (a new word comes back later today). It doesn't count as forgotten. Time with the app in the background doesn't count.
  - Each time you remember a due card it moves up a level and comes back later: 1 day → 3 days → 1 week → 2 weeks → 1 month.
  - A word counts as **Learned** at level 4 (you've remembered it on schedule for about two weeks).
  - **Still learning** sends a card back to level 1; it comes up again the same day.
- **Keep going:** when a group has nothing left that counts (no reviews, no new words), the Cards tab offers **Keep going** for extra practice with words you've already seen. Extra practice never moves a word up, so it can't count toward Learned. *Still learning* still sends a word back down. Settings → *Extra practice* picks which side those cards show first.
- **Which way you answer depends on the level.** New and level-1 cards show the Spanish word; answer in English. From level 2 on, cards show the English and the Spanish example with the word blanked out; answer in Spanish. So a word only reaches **Learned** (level 4) after you've produced the Spanish on schedule.
  - **Still learning** on a Spanish-answer card drops it to level 2 (you keep answering in Spanish); on an English-answer card it drops to level 1.
- **Words** tab: one list with a *To learn / Learned / All* filter. **Mark learned** is for words you already know: the word counts as learned and **never comes back**. **Relearn** on a learned word brings it back (at level 2, answering in Spanish).
- The speaker buttons read the word and sentence aloud with your device's Spanish voice (if it has one).
- Nouns show their article with a colour: blue = masculine (el), pink = feminine (la), purple = either. Verbs show present (yo / tú / él) and past forms on the answer side.
- Pick a group from the dropdown: frequency groups of 100, topics (food, travel, family…), or *Tricky words*.
- **Games** tab: practice that doesn't change your levels. Tricky words and words you're currently learning come up most, with an easy one now and then. Words come from the group in the picker.
  - *Match race*: match Spanish to English (60 s, wrong pairs −3 s). Matched spots stay empty for a moment and then refill in place; one unmatched word waits on each side so the two new tiles never match each other.
  - *Gender sort*: tap el or la for each noun (60 s).
  - *True or false*: does the Spanish match the English? Swipe right for True, left for False, or tap the buttons (60 s).
  - *Listen and tap*: hear the word, pick its meaning (10 words).
  - *Fill the gap*: Spanish sentence with a missing word, 6 Spanish options, no English (60 s). The wrong options are a different kind of word (for example verbs when a noun is missing), so only one choice fits the sentence.
  - *Word builder*: spell the Spanish from scrambled letters (8 words).
  - To add a game: put its component in `Components/Games/`, list it in `GamesHub.razor`; `GameKit.cs` has the shared word picker and timer.

## Verbs tab

Conjugation practice, separate from the word cards. It covers every verb in `words.json` (222 verbs).

- **Tree:** tense → type → group → verb. For example *Present → Stem-changing verbs → o → ue → poder*, or *Preterite → Irregular stems → u-stem*. When a type has only one group (like *Unique verbs*), the extra level is skipped.
- **Tenses:** present, preterite, imperfect, future, conditional, present perfect, present subjunctive, commands, imperfect subjunctive, pluperfect, future perfect, conditional perfect, present perfect subjunctive, pluperfect subjunctive. Not taught: the future subjunctive and the preterite perfect (*hube hablado*). The *-se* imperfect subjunctive (*hablase*, *hubiese*) is explained and accepted in answers (the letter tiles include what it needs), but not drilled.
- **Colours:** every table colours each form: stem (dark), ending (teal, bold), changed letters (orange highlight), reflexive pronoun (green, italic) and *haber* (grey). A key sits under each table; the top Verbs Rules page explains it. The split is worked out from the data in `Models/Verbs/VerbParts.cs`.
- **Rules pages** all have the same shape: an *In short* summary, the coloured table (singular on the left, plural on the right; stem-changers get a boot outline), a recipe line showing how the form is built (*hablar → habl + o = hablo*), example sentences with clue words, memory tricks and side-by-side comparisons (preterite or imperfect, future or conditional…), details folded away, and a one-tap quick check at the end. Each level only says what's new at that level. The top Verbs page explains stem, ending, person and so on.
- **Compared with regular verbs:** group and verb pages start with a short card saying what's the same as the plain pattern and what's different, worked out from the data (`Models/Verbs/VerbCompare.cs`). For example *enviar*, present: same endings as regular *-ar* verbs; the stem *envi-* becomes *enví-* in yo, tú, él and ellos; nosotros and vosotros are completely regular. Group pages use the clearest member as their example (for *-go* verbs that's *hacer*, whose only change is *hago*, not *tener*, which also has *tienes*). A folded *Side by side with a regular verb* section shows both tables together.
- **Decode** (*Tree / Grid / Decode* switch): recognising forms rather than producing them. You see a form (on its own, in a story sentence, or read aloud), then pick which verb, who and when; any correct reading counts (*fue* is *ser* or *ir*; *hablamos* is present or preterite). All three right is a right answer, right verb with one of who / when wrong is half. Rounds of 10 lean towards forms that don't look like their verb: about half are forms like *fue, tuvo, iba*, a third have a change (*quiero, busqué*), a fifth are regular. After each answer, short clues say what gave it away (*tuv-* → *tener*; *-mos* = we; *-aba-* = was …ing). Progress is kept per form under `dec:verb|tense|person`. Also on Decode: *Endings*, a cheat sheet of the signs that say who and when, and *Strange stems*, a table of stems that don't look like their verb (*tuv-* → tener, *fu-* → ser / ir, *ib-* → ir, *tendr-* → tener, *hech-* → hacer…), with a *Strange stems only* practice option. Logic in `Models/Verbs/VerbDecode.cs`.
- **Stories:** every tense has a *Story* tab: a short everyday story (9 to 13 sentences) using that tense, written with words from the app's word list only. Verbs in the story's tense are colour-coded; tap any verb to see its infinitive, tense and person, and open it in the tree. The English for each sentence is hidden until you tap *English* (or *Show all the English*); a speaker reads a sentence or the whole story. Group, pattern and verb Rules pages get an *In the stories* section with up to 6 sentences that use their forms. Stories are written in `tools/stories/stories_src.py` and built and checked by `tools/stories/build.py` (see `tools/stories/README.md`).
- **Every item** has *Overview*, *Rules* and *Practice* for everything underneath it, plus a progress bar.
- **What's tracked**, per form (verb · tense · person), with strength, due date and mistakes:
  - *Regular verbs* are tracked by pattern (the endings, e.g. `~ar|pres|yo`), practised with a rotating set of real regular verbs.
  - *Stem-changing, -go, -zco and spelling-change verbs* are tracked verb by verb, for the forms that change.
  - *Unique and reflexive verbs* are tracked form by form.
  - Branch scores are the average strength of everything underneath (forms not practised yet count as 0). Mistakes are also shown by person (yo, tú…).
- **Answering:** new forms are multiple choice (6 look-alike options: the "regularised" form, a stem change in the wrong place, the form without its accent, other persons, look-alike tenses). After a couple of right answers (ideally on different days), you spell the form from letter tiles: the letters of the answer plus 3 or 4 trap letters (the same vowel with or without an accent, letters from the near-miss forms, a common letter). No keyboard needed. Picking the vowel without its accent counts as half a mistake. A wrong answer brings the form back in a few minutes and again later in the same round.
- **Practising one verb** (e.g. *tener* under -go verbs) covers the whole verb in that tense, and each answer counts where it belongs: *tengo* toward -go verbs, *tienes* toward e → ie, *tenemos* toward the regular *-er* endings. Each form is asked once per round; a missed form comes back a few questions later.
- **Leaving a round:** you can switch to Rules or Overview, open other items or other app tabs mid-round and come back to the same question, letters already tapped included. A *Practice in progress* bar with **Resume** shows at the top of the Verbs tab. Rounds are kept while the app is open; every answer is saved to your progress as soon as you give it. Tests work the same way.
- **Tests:** every tense, type, group and verb has a *Test* view (not the top Verbs item: a test on all 14 tenses would be too long or too thin). Spelling only, no feedback until the end, a *Don't know* button. Every group underneath gets at least one question, bigger groups more (8 to 20 questions for a tense; all forms for a single verb), leaning towards the forms that change. Results: score, each group passed or weak, mistakes by person, every answer. Logic in `Models/Verbs/VerbTests.cs`.
  - *Testing out:* a group passes with no wrong answers (an accent slip is half a point, 75% needed). Every form in a passed group gets strength 0.7, so it comes back once in about a week to confirm.
  - *Weak groups:* forms you'd already seen in them come back for practice now, and the group shows under *Weak spots*.
  - *Mastered:* 90% or more on an item's test gives it a Mastered badge, kept while at least 80% of the forms you've seen under it stay reasonably strong (0.5+).
- **Grid** (switch *Tree / Grid* at the top of the Verbs tab): rows for the regular *-ar*, *-er* and *-ir* endings and for every verb with irregular forms (104 of 222); columns for the 14 tenses. Each cell is coloured by how well you know that verb's tracked forms in that tense: not started (grey), weak (red), learning (orange), good (light green), strong (dark green). A dot means the verb is regular in that tense (it follows its pattern row); an empty cell means the form isn't used (no commands for *llover*, say). Tap a cell to see the verb's table for that tense, then *Open in the tree* or *Practise*. Search by verb or meaning; sort by frequency or weakest first. Logic in `Models/Verbs/VerbGrid.cs`.
- **Vosotros** (Settings → Verbs, on by default): turned off, *vosotros* forms are hidden from the tables and left out of practice, tests, scores, mistakes by person, weak spots and the grid. Progress on them is kept.
- **Weak spots:** on the Verbs home, tense and type pages: up to 5 groups, those not passed in their last test first, then the weakest or most-missed ones you've practised. Each has a Practise button.
- **Schedule:** right answers push a form out to 1, 2, 4, 8, 16, then 30 days; spelled answers count more than multiple choice. Practice starts with what's due, then up to 4 new forms, then the weakest.
- **Export / Import:** verb progress is in the JSON export (`verbs`, and test results in `verbTests`). The CSV is the word list only. Importing a file without verb progress keeps the verb progress already on the device.
- **Verb data:** `wwwroot/data/verbs.json` is generated by `tools/verb-data/gen.py` and checked against the verbecc conjugation library; see `tools/verb-data/README.md`.

## Settings tab

Daily review limit, answer timer, extra practice direction, verbs (*vosotros* on or off), backup/transfer (below), a voice test, a short how-it-works, and *Reset all progress* (tap twice to confirm).

## Export and import your progress

On the **Settings** tab, under *Backup and transfer*:

- **Export JSON** saves three readable lists (`learned`, `stillLearning`, `notStarted`) plus each word's level and next review date. Use it for backups or for moving your progress to another browser or phone.
- **Export CSV** saves the same data as a spreadsheet (`rank, spanish, english, part_of_speech, topic, status, level, next_review`), which opens in Excel or Google Sheets.
- **Import…** loads either kind of file and **replaces** your current progress. In a CSV, the `status` column can be `learned`, `still learning` or `not started` (`known` and `practicing` also work). Words that aren't in the app's list are skipped.

## Project layout

| Path | What it is |
|---|---|
| `Pages/Home.razor` | The app shell: flashcards, lists, tab bar |
| `Models/Word.cs` | Word model: gender, verb forms, topic, fill-in-the-blank |
| `Models/Srs.cs` | The simple spaced-repetition schedule |
| `Components/SpanishWord.razor` | Spanish word with coloured article |
| `Components/Games/` | Games area (`GamesHub.razor`) and games (`MatchRace.razor`) |
| `Services/WordRepository.cs` | Loads `wwwroot/data/words.json` |
| `Services/ProgressTransfer.cs` | Export to JSON/CSV and import from them |
| `wwwroot/js/download.js` | Saves exported files and reads Spanish aloud |
| `Services/ProgressStore.cs` | Saves known/practicing words, verb skills and shuffle order to localStorage |
| `Models/Verbs/` | Verbs tab: verb data and tenses (`VerbData.cs`), tree (`VerbTree.cs`), skill tracking (`VerbSkills.cs`), practice (`VerbQuiz.cs`), Rules text (`VerbRules.cs`) |
| `Components/Verbs/` | Verbs tab screens: `VerbsHub.razor` (tree), `VerbPractice.razor`, `VerbRulesView.razor`, tables |
| `Services/VerbRepository.cs` | Loads `wwwroot/data/verbs.json` and builds the tree |
| `wwwroot/data/verbs.json` | Full conjugations for every verb (generated: `tools/verb-data/`) |
| `wwwroot/data/stories.json` | One example story per tense (generated: `tools/stories/`) |
| `wwwroot/data/words.json` | 1,000 words with example, topic, gender/article, verb forms |
| `wwwroot/css/app.css` | Styles (mobile-first) |

To add or edit words, change `words.json`; order in the file is the rank.
