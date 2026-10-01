# Palabras – Spanish flashcards (Blazor WebAssembly)

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

Upload the contents of `bin/Release/net10.0/publish/wwwroot` to any static host (GitHub Pages, Azure Static Web Apps, Netlify…). Open the site on your phone and choose **Add to Home Screen** – it launches full-screen like an app.

(If you host under a sub-path such as `/palabras/`, change `<base href="/" />` in `wwwroot/index.html` to match.)

## How studying works

- **Review** shows today's required reviews in the chosen group, then new words, then optional extra reviews. Tap **I know it** or **Still learning**.
  - **Multiple choice on the early levels:** new and level-1 cards show the Spanish word – pick the English meaning from 4. Level-2 cards show the English and the sentence with the word blanked out – pick the Spanish word from 4. From level 3 you recall the word yourself.
  - **Daily review limit** (To learn tab → *Daily reviews*, default 30): the most reviews you *have* to do each day. Anything over it is optional extra and rolls forward. New review dates are also spread over quieter days so reviews don't bunch up. You can still learn as many new words as you like.
  - **Tricky words:** words you forget repeatedly (2+ times) get a *tricky* badge and their own group in the picker.
  - Each time you remember a due card it moves up a level and comes back later: 1 day → 3 days → 1 week → 2 weeks → 1 month.
  - A word counts as **Learned** at level 4 (you've remembered it on schedule for about two weeks).
  - **Still learning** sends a card back to level 1; it comes up again the same day.
- **Practice** lets you go through any cards, any time. Remembering a card that isn't due yet is practice only: its level doesn't change, so it doesn't count toward Learned.
- **Which way you answer depends on the level.** New and level-1 cards show the Spanish word – answer in English. From level 2 on, cards show the English and the Spanish example with the word blanked out – answer in Spanish. So a word only reaches **Learned** (level 4) after you've produced the Spanish on schedule.
  - **Still learning** on a Spanish-answer card drops it to level 2 (you keep answering in Spanish); on an English-answer card it drops to level 1.
  - In **Practice**, the direction button cycles **Auto** (follow the level) → **ES → EN** → **EN → ES**.
- **Mark learned** in the To learn list is for words you already know: the word counts as learned and **never comes back** in Review or Practice. **Relearn** in the Learned list brings it back (at level 2, answering in Spanish).
- The speaker buttons read the word and sentence aloud with your device's Spanish voice (if it has one).
- Nouns show their article with a colour: teal = masculine (el), pink = feminine (la), purple = either. Verbs show present (yo / tú / él) and past forms on the answer side.
- Pick a group from the dropdown: frequency groups of 100, topics (food, travel, family…), or *Tricky words*.
- **Games** tab: practice that doesn't change your levels. *Match race*: match Spanish to English against a 60-second clock (wrong pairs cost 3 seconds); words you've seen but don't know yet come up most. New games go in `Components/Games/` and get listed in `GamesHub.razor`.

## Export and import your progress

On the **To learn** or **Learned** tab, use the buttons under *Your progress*:

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

To add or edit words, change `words.json` – order in the file is the rank.
