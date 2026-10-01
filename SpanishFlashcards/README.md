# Palabras: Spanish flashcards (Blazor WebAssembly)

Learn 1,000 common Spanish words as flashcards. Cards you know go to **Learned**; the rest stay in **To learn**. Progress is saved in your browser (localStorage).

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
- **Mark learned** in the To learn list is for words you already know: the word counts as learned and **never comes back**. **Relearn** in the Learned list brings it back (at level 2, answering in Spanish).
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

## Settings tab

Daily review limit, extra practice direction, backup/transfer (below), a voice test, a short how-it-works, and *Reset all progress* (tap twice to confirm).

## Export and import your progress

On the **Settings** tab, under *Backup and transfer*:

- **Export JSON** saves three readable lists (`learned`, `stillLearning`, `notStarted`) plus each word's level and next review date. Use it for backups or for moving your progress to another browser or phone.
- **Export CSV** saves the same data as a spreadsheet (`rank, spanish, english, part_of_speech, topic, status, level, next_review`), which opens in Excel or Google Sheets.
- **Import…** loads either kind of file and **replaces** your current progress. In a CSV, the `status` column can be `learned`, `still learning` or `not started` (`known` and `practicing` also work). Words that aren't in the app's list are skipped.

## Project layout

| Path | What it is |
|---|---|
| `Pages/Home.razor` | The whole app: flashcards, lists, tab bar |
| `Models/Word.cs` | Word model: gender, verb forms, topic, fill-in-the-blank |
| `Models/Srs.cs` | The simple spaced-repetition schedule |
| `Components/SpanishWord.razor` | Spanish word with coloured article |
| `Components/Games/` | Games area (`GamesHub.razor`) and games (`MatchRace.razor`) |
| `Services/WordRepository.cs` | Loads `wwwroot/data/words.json` |
| `Services/ProgressTransfer.cs` | Export to JSON/CSV and import from them |
| `wwwroot/js/download.js` | Saves exported files and reads Spanish aloud |
| `Services/ProgressStore.cs` | Saves known/practicing words and shuffle order to localStorage |
| `wwwroot/data/words.json` | 1,000 words with example, topic, gender/article, verb forms |
| `wwwroot/css/app.css` | Styles (mobile-first) |

To add or edit words, change `words.json`; order in the file is the rank.
