# Palabras: Spanish flashcards (Blazor WebAssembly)

Learn 2,000 common Spanish words as flashcards, and Spanish verb conjugation. The **Words** tab lists the words in a group, filtered by *To learn*, *Learned* or *All*. Progress is saved in your browser (localStorage).

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

- The **Cards** tab shows, in the chosen group: learning steps that are due, then today's required reviews, then new words (up to your daily goal), then optional bonus reviews (due words over your daily limit). The schedule is in `Models/Srs.cs`.
  - **A new word** first appears as an **introduction**: the Spanish with its article, the meaning, the example sentence with its translation and the sound. Tap **Got it** (or **I already know it**: no steps, rated Easy, a first review in 4 days, not counted as a new word).
  - **Learning steps, the same session:** a minute later you pick the English meaning from 6; five minutes after that you see the English and the sentence with the word blanked, and pick the Spanish from 6; ten minutes after that you **recall the Spanish yourself** (no options), because producing the Spanish is what really shows you know it. A miss (or a slow right answer, or Hard on the recall) repeats that step a minute later. When only learning steps are left, Cards waits for the soonest (*Un momento…*, with **Show it now**) instead of asking a word again straight away. Each step answer also updates the word's memory (see below); after the three steps, the first review is always tomorrow.
  - **Check-in later the same day:** about 4 hours after you finish learning a word, it comes back once for a quick recall of the Spanish (shown first in the queue, after due learning steps). Remember it (Got it or Hard, or a near miss when typing) and the first review stays tomorrow. Forget it and you redo the recall step a minute later. Words learned too late in the day (when 4 hours later would be past 11 pm) skip it, since the next morning's review comes after a night's sleep anyway. Check-ins don't count toward the review limit or the new-word goal, and when you're done for now Cards says when the next check-in is due. Settings → *Check-in later today* turns them off.
  - **Reviews** always ask for the Spanish: you see the English and the blanked sentence and recall the word.
    - **Type the Spanish** (Settings → *Answering in Spanish*, on by default): type it (accent buttons below the box, Enter to check) and the app checks it. The word or the form that fits the sentence counts (*hablar* or *Hablo*), with or without the article. Right, or right except for a missing accent or ñ → **Got it** (the message shows the right spelling: you remembered the word, so a slip on the accent doesn't hold it back); the wrong article (*el mano*) or a one-letter slip → **Hard**, with what was off; wrong or **I don't know it** → **Forgot**. **Count it as right** overrides the check (another word that fits, a typo you don't care about). The checker is `Models/WordCheck.cs`.
    - With typing off: say the Spanish, tap **Show answer** and grade yourself **Forgot**, **Hard** or **Got it** (the buttons only appear after the answer; under them it says when each would bring the word back).
    - **The schedule is FSRS** (FSRS-6, the memory model Anki uses; `Models/Fsrs.cs`, checked against the reference implementation `py-fsrs`). Each word has a **stability** (days until the chance of remembering it falls to 90%) and a **difficulty** (1 to 10). Every answer updates them: the first answer sets them, answers on the same day (learning steps, the check-in) use FSRS's short-term formula, answers on later days the long-term one, which takes into account how likely you were to remember it (a word you remember after a long or late gap grows more).
    - The next review is planned for the day the chance of remembering falls to the **aim** (Settings → *How well you remember*, 90% by default; 85% or 95% also offered), then moved a day or so to a quieter day. With the standard parameters, a word you always get right comes back after 1 day, then about 7, 30 and 120 days (up to a year); a word you found hard at first goes about 1 → 2 → 7 → 19 → 49 days.
    - **Hard** grows the gap less and raises the word's difficulty. A slow **Got it** (see the timer) counts as Hard.
    - **Forgot** counts as a lapse, lowers the stability a lot and raises the difficulty, and gives one relearning step a few minutes later (recall the Spanish again); the word then comes back after the gap its new stability gives (usually 1 to 3 days).
  - A word counts as **Learned** once its stability reaches 20 days (about three weeks; at the 90% aim that's a 20-day gap), or when marked as known. Stability doesn't depend on the aim, so changing the aim can't un-learn words; only forgetting one does. Learned words keep coming back, further apart each time.
  - **Daily review limit** (Settings tab → *Daily reviews*, default 30): the most reviews you *have* to do each day, shortest gaps first. Anything over it is optional bonus and rolls forward. Each word counts once a day. New review dates are spread over quieter days so reviews don't bunch up.
  - **New words a day** (Settings tab → *New words a day*, default 10; *No goal* turns it off): the line above the card shows how many new words you've started today (*3/10 new today*). Once you reach the goal and your required reviews are done, Cards shows *You're good for now* instead of more new words, with **Learn more new words** (new words come back for the rest of the day) and **Practise words I've seen** (extra practice). A new word counts when you tap Got it on its introduction.
  - **The day starts at 4 am**, so a late-night session belongs to the evening it started in.
  - **Tricky words:** words you forget repeatedly (2+ times) get a *tricky* badge and their own group in the picker.
  - **Answer timer** (Settings → *Answer timer*; by default 4 seconds when you answer in English and 6 in Spanish; can be turned off): runs until you pick an option, tap *Check* or tap *Show answer*. A slow right answer repeats a learning step, or counts as Hard in a review. Time with the app in the background doesn't count.
- **Keep going:** when a group has nothing left that counts, the Cards tab offers **Keep going** for extra practice with words in reviews that aren't due. Hard and Got it change nothing there, so extra practice can't make a word look learned; **Forgot** still sends a word back to relearn. Settings → *Extra practice* picks which side those cards show first (by default the English, answering in Spanish).
- **Words** tab: one list with a *To learn / Learned / All* filter. **Mark learned** is for words you already know: the word counts as learned and **never comes back**. **Relearn** on a learned word brings it back to the first learning step (a word that was only marked as known goes back to New).
- **Progress from before October 2026** (levels 1 to 5) is converted the first time the app opens: marked-as-known words stay marked; other words keep their next review date with a matching gap (level 1 → 1 day, 2 → 3, 3 → 8, 4 → 20, 5 → 45, so learned words stay learned). Each gap then becomes the word's stability, and its ease a difficulty, the way Anki converts when you switch to FSRS. A copy of the old progress is kept in the browser under `palabras-flashcards-v1-before-new-schedule` (or `-before-fsrs` when converting from the ease-based schedule some test copies had). Old export files are converted the same way on import.
- **Review history:** every answer on a word card (the word, when, the kind of answer, the rating, the gap, the memory after it, the time taken, and for typed answers what the check found) is saved in the browser's IndexedDB (`palabras-history`; `wwwroot/js/reviewlog.js`, `Models/ReviewLog.cs`). It isn't needed to study; it's for:
  - **How well you remember** (Settings): the share of due reviews you remembered in the last 30 days, overall and by gap, against the aim; check-ins; and an estimate of how much of your review words you'd remember right now.
  - **Fitting the schedule to you:** **Download history** saves the answers as the CSV an FSRS optimizer reads (`card_id, review_time, review_rating, review_state, review_duration`, plus the word and kind). After a few months, the 21 parameters an optimizer fits can be pasted into *Fit the schedule to you* (**Back to standard** undoes it).
  - Export JSON includes the history and the schedule settings, and Import restores them. *Reset all progress* deletes the history too.
- The speaker buttons read the word and sentence aloud with your device's Spanish voice (if it has one).
- Nouns show their article with a colour: blue = masculine (el), pink = feminine (la), purple = either. Verbs show present (yo / tú / él) and past forms on the answer side.
- Pick a group from the dropdown: frequency groups of 100, topics (food, travel, family…), or *Tricky words*.
- **Games** tab, in two sections. **Vocabulary** games don't change your levels; tricky words and words you're currently learning come up most, with an easy one now and then, from the group in the picker:
  - *Match race*: match Spanish to English (60 s, wrong pairs −3 s). Matched spots stay empty for a moment and then refill in place; one unmatched word waits on each side so the two new tiles never match each other.
  - *Gender sort*: tap el or la for each noun (60 s).
  - *True or false*: does the Spanish match the English? Swipe right for True, left for False, or tap the buttons (60 s).
  - *Listen and tap*: hear the word, pick its meaning (10 words).
  - *Fill the gap*: Spanish sentence with a missing word, 6 Spanish options, no English (60 s). The wrong options are a different kind of word (for example verbs when a noun is missing), so only one choice fits the sentence.
  - *Word builder*: spell the Spanish from scrambled letters (8 words).
- **Verbs** games use the most common verbs (rank-weighted, top 120) and the vosotros setting. A miss is recorded as a wrong answer for that form (Decode skill for the reading games, practice skill for the others) so it comes up again in the Verbs tab; right answers aren't recorded. Shared code in `Components/Games/VerbGameKit.cs`, tense picker `TenseChips.razor`, results `VerbGameResults.razor`.
  - *Who's talking?*: a form appears, tap the person; any person with that form counts (*hablaba*: yo or él). Pick tenses (not commands). 60 s, wrong −3 s.
  - *What does it mean?*: a form appears, pick the English (*tuvimos* → "we had"). Any reading counts (*fue*: he was or he went). Wrong options are the same verb in look-alike tenses or other persons (never "you" for an *él* form, since *usted* uses it). English comes from `Models/Verbs/VerbEnglish.cs` and `VerbEnglish.Data.cs` (made by `tools/verb-english/gen_english.py`); *gustar*-type verbs are left out. 60 s.
  - *Tense sort*: pick a pair (preterite/imperfect, future/conditional, present/subjunctive, present/preterite) and sort each form; forms that could be either (*hablamos*) are left out. 60 s.
  - *Stem match*: five strange stems (from Decode's table) and their verbs; tap one on each side. Clear the board for a new one. 60 s, wrong pair −3 s.
  - *Clue words*: 10 sentences with a missing verb and a clue (*ayer*, *de niño*, *ojalá*, *si tuviera*…) that decides the tense; four forms of the same verb in rival tenses; the reason after each. 41 sentences in `tools/verb-games/clues_src.py`, which checks them against verbs.json and writes `Models/Verbs/VerbClues.Data.cs`.
  - *Ping-pong*: the classroom game. A person is called out (*yo*, *ella*, *ustedes*…, spoken aloud if the device can) and you tap the matching form from four forms of the same verb. Each verb goes round every person in random order, then a new verb comes in; a missed person comes back two calls later. Pick the tense (any of the 15) and the verbs (top 30, irregular in that tense, or all). 60 s, wrong −3 s. Misses are recorded in your verb progress (the form comes up in practice soon); right answers aren't, so a fast game can't make a form look learned. Logic in `Components/Games/PingPongGame.cs`.
  - *Build it*: verb, person and tense; tap the stem, then the ending (*habl* + *ábamos*). Decoys are real stems and endings of the same verb in other persons and tenses. 10 forms.
  - *Boss battle*: pick one of 15 tough verbs and the tenses. Boss has 12 health, you have 3 hearts; a right answer hits once (twice on a streak of 3+), a wrong one costs a heart. Beaten bosses get a crown (`boss-<verb>` in GameBests); the card shows how many you've beaten.
  - To add a game: put its component in `Components/Games/`, list it in `GamesHub.razor`; `GameKit.cs` has the shared word picker and timer.

## Read tab

Paste any Spanish text (up to 20,000 characters), or find a song's lyrics, and see how much of it you can read.

- **Songs** (the card at the top):
  - **Listen:** tap the microphone and hold the phone near the music. It records 8 seconds (tap again to stop), sends them to [AudD](https://audd.io) to name the song, then finds its lyrics. It needs your own AudD key, pasted in Settings → *Song recognition* (300 songs free on signup, then about $5 per 1,000). The key is kept only in this browser (`localStorage` key `palabras-audd-key`, outside the progress, so it's never in a backup or export) and is only sent to `api.audd.io`. Settings shows how many songs this device has sent.
  - **Search:** type a song or artist. Free, no key.
  - Lyrics come from [LRCLIB](https://lrclib.net), a free crowdsourced lyrics database (no key; its API allows calls from any web page). Results are one per song, songs with lyrics first. Tap one and the lyrics open as a text with everything below, with the artist, album and length under the title and a note in *Your texts*. Opening the same song again opens the text you already have. Time-synced lyrics are saved too (`Song.Synced`), ready for following along.
  - Code: `Components/Read/ReadHub.razor` (the card), `Services/SongService.cs` (LRCLIB), `Models/Reading/Songs.cs` (LRCLIB and AudD shapes, tidying results), `wwwroot/js/songs.js` (recording and sending to AudD; no echo cancelling or noise suppression, since it's music).

- **Coverage:** the share of words you know (learned) or are learning, as a bar (known / learning / new). Names (a capital letter mid-sentence) and words found nowhere don't count.
- **Words to learn first:** the fewest new words that take you to 95%, most useful first (most often in the text, then most common in Spanish), each with its meaning and how often it appears. Tick or untick, then *Study these N words*: words from the app's list are added to the text's study list; words outside it become your own cards (topic *My words*) with the sentence from your text as the example. Each text with words to study gets its own group in the Cards picker (*From your texts*), and *Practise this text's words* opens it. *Other new words* lists the rest.
- **Verbs in this text:** which tenses the text uses (verbs from the Verbs tab, regular forms of other verbs worked out from the endings, and *haber* + participle), with your strength in each tense. Below 40%: *Learn the preterite to read these*, with buttons for its Rules and for Decode practice on that tense.
- **The text:** every word marked (known plain, learning shaded, new underlined, outside the list dotted, picked to study double-underlined). Tap a word (or phrase: *por favor*, *he comido*, *me llamo*) for its headword, meaning, verb reading (*fue*: ser or ir, preterite, él), and *Study it*, *I know it*, *Add to my words*, your own meaning for words the dictionary doesn't have, or *Look it up* (SpanishDict).
- **Matching:** the word list first (with phrases up to four words, *del*/*al*), then verb forms from the Verbs tab (also two- and three-word ones), then the dictionary (forms → headword), then feminine/plural guesses (*tanta* → *tanto*), then pronouns taken off the end (*dámelo*).
- **Dictionary:** `wwwroot/data/dict.tsv` (about 2 MB, loaded the first time you open a text), made by `tools/dictionary/build_dict.py` from Wiktionary data (CC BY-SA, credit shown in the app; see `wwwroot/data/dict-LICENSE.txt`).
- Texts (`Texts`) and your own words (`MyWords`) are saved with your progress and included in Export JSON / Import. Code: `Models/Reading/` (analysis, dictionary, saved texts), `Components/Read/` (screens), `Services/DictionaryRepository.cs`.

## Verbs tab

Conjugation practice, separate from the word cards. It covers the 222 verbs among the first 1,000 words of `words.json`. Verbs in words 1001 to 2000 are word cards only (with a short conjugation line).

- **Tree:** tense → type → group → verb. For example *Present → Stem-changing verbs → o → ue → poder*, or *Preterite → Irregular stems → u-stem*. When a type has only one group (like *Unique verbs*), the extra level is skipped.
- **Top page:** an *Up next* card (reviews due first, otherwise the next tense in the learning order: start it, or keep going until its regular endings are at 60% and the tense at 20%, or it's mastered; after stage 3, the rare tenses in Decode). Below it the tenses in four folding stages: *Start here* (present, going to, preterite, imperfect), *Next* (present perfect, commands, present subjunctive, future), *Later* (conditional, imperfect subjunctive, pluperfect) and *Recognise only* (the four rare compound tenses). Stages 1 and 2 start open, later ones once you've started something in them. Dots show how often a tense is met (●●●● everyday to ● rare). Rows you haven't started say *Not started* instead of an empty bar. *Mistakes by person* is folded. Tenses in *Recognise only* lead with *Practise reading (Decode)*. Stages and frequencies are in `VerbGrammar.Tenses` (`VerbData.cs`).
- **Going to (ir a):** *voy a hablar*: built for every verb from the present of *ir* (not in verbs.json). One *Every verb* pattern plus reflexive verbs (*me voy a levantar*, *voy a levantarme* also accepted). Near misses: no *a* (*voy hablar*), a conjugated second verb (*voy a hablo*), *iba a*. Not in the grid or Decode.
- **Tenses:** present, going to, preterite, imperfect, future, conditional, present perfect, present subjunctive, commands, imperfect subjunctive, pluperfect, future perfect, conditional perfect, present perfect subjunctive, pluperfect subjunctive. Not taught: the future subjunctive and the preterite perfect (*hube hablado*). The *-se* imperfect subjunctive (*hablase*, *hubiese*) is explained and accepted in answers (the letter tiles include what it needs), but not drilled.
- **Colours:** every table colours each form: stem (dark), ending (teal, bold), changed letters (orange highlight), reflexive pronoun (green, italic) and *haber* (grey). A key sits under each table; the top Verbs Rules page explains it. The split is worked out from the data in `Models/Verbs/VerbParts.cs`.
- **Rules pages** all have the same shape: an *In short* summary, the coloured table (singular on the left, plural on the right; stem-changers get a boot outline), a recipe line showing how the form is built (*hablar → habl + o = hablo*), example sentences with clue words, memory tricks and side-by-side comparisons (preterite or imperfect, future or conditional…), details folded away, and a one-tap quick check at the end. Each level only says what's new at that level. The top Verbs page explains stem, ending, person and so on.
- **Compared with regular verbs:** group and verb pages start with a short card saying what's the same as the plain pattern and what's different, worked out from the data (`Models/Verbs/VerbCompare.cs`). For example *enviar*, present: same endings as regular *-ar* verbs; the stem *envi-* becomes *enví-* in yo, tú, él and ellos; nosotros and vosotros are completely regular. Group pages use the clearest member as their example (for *-go* verbs that's *hacer*, whose only change is *hago*, not *tener*, which also has *tienes*). A folded *Side by side with a regular verb* section shows both tables together.
- **Decode** (*Tree / Grid / Decode* switch): recognising forms rather than producing them. You see a form (on its own, in a story sentence, or read aloud), then pick which verb, who and when; any correct reading counts (*fue* is *ser* or *ir*; *hablamos* is present or preterite). All three right is a right answer, right verb with one of who / when wrong is half. Rounds of 10 lean towards forms that don't look like their verb: about half are forms like *fue, tuvo, iba*, a third have a change (*quiero, busqué*), a fifth are regular. After each answer, clues built from that form say what gave it away: where the stem comes from (*quer(e)r* → *querr-*), the form split into stem + ending (*querr* + *-ás*), what the ending says about who, the tense sign, and, if you picked the right verb but the wrong tense or person, what that would have looked like (*quieres*). Buttons open the Rules for that verb and that tense; the round waits for you. Progress is kept per form under `dec:verb|tense|person`. **Which tenses:** *Smart* (default) uses the *Start here* tenses plus any tense you've practised in the tree or decoded; *My choice* lets you tick tenses by stage (saved as `DecodeTenses` in progress; null = Smart). **Easy first:** in each tense, forms come up as often as they exist (mostly regular: about 1 in 10 present forms doesn't look like its verb) until you've decoded some of it correctly; the weighting towards hard forms then grows until 15 forms of that tense are solid (strength 0.4+), reaching about 2 in 3. *Strange stems only* skips the ramp. The *When?* options favour your chosen tenses. Compound tenses now include regular participles (you learn to spot the tense from *haber*). Also on Decode: *Endings*, a cheat sheet of the signs that say who and when, and *Strange stems*, a table of stems that don't look like their verb (*tuv-* → tener, *fu-* → ser / ir, *ib-* → ir, *tendr-* → tener, *hech-* → hacer…), with a *Strange stems only* practice option. Logic in `Models/Verbs/VerbDecode.cs`.
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
- **Tests:** every tense, type, group and verb has a *Test* view (not the top Verbs item: a test on all 15 tenses would be too long or too thin). Spelling only, no feedback until the end, a *Don't know* button. Every group underneath gets at least one question, bigger groups more (8 to 20 questions for a tense; all forms for a single verb), leaning towards the forms that change. Results: score, each group passed or weak, mistakes by person, every answer. Logic in `Models/Verbs/VerbTests.cs`.
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

Daily review limit, new words a day, typing the Spanish, the same-day check-in, answer timer, how well you remember (with the aim and the schedule's parameters), extra practice direction, verbs (*vosotros* on or off), backup/transfer (below), a voice test, a short how-it-works, and *Reset all progress* (tap twice to confirm).

## Export and import your progress

On the **Settings** tab, under *Backup and transfer*:

- **Export JSON** saves three readable lists (`learned`, `stillLearning`, `notStarted`) plus each word's schedule (phase, gap, next review date, stability, difficulty, lapses), the schedule settings (`scheduler`) and the review history (`history`). Use it for backups or for moving your progress to another browser or phone.
- **Export CSV** saves the same data as a spreadsheet (`rank, spanish, english, part_of_speech, topic, status, level, next_review, interval_days`; level is 0 to 5, how well it's known), which opens in Excel or Google Sheets.
- **Import…** loads either kind of file and **replaces** your current progress. In a CSV, the `status` column can be `learned`, `still learning` or `not started` (`known` and `practicing` also work). Words that aren't in the app's list are skipped.

## Project layout

| Path | What it is |
|---|---|
| `Pages/Home.razor` | The app shell: flashcards, lists, tab bar |
| `Models/Word.cs` | Word model: gender, verb forms, topic, fill-in-the-blank |
| `Models/Srs.cs` | The word schedule: learning steps, check-in, reviews |
| `Models/Fsrs.cs` | The FSRS-6 memory model the schedule uses |
| `Models/ReviewLog.cs` | Review history entries, stats and the optimizer CSV |
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
| `wwwroot/data/words.json` | 2,000 words with example, topic, gender/article, verb forms |
| `wwwroot/css/app.css` | Styles (mobile-first) |

To add or edit words, change `words.json`; order in the file is the rank. Progress is saved by the Spanish word, so never change an existing word's `es` (fixing its meaning or example is fine). Words 1001 to 2000 were added with the tools and briefs in `tools/words/` (see its README).
