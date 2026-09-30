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

- **Review** shows the cards that are *due* in the chosen group, then new words. Tap **I know it** or **Still learning**.
  - Each time you remember a due card it moves up a level and comes back later: 1 day → 3 days → 1 week → 2 weeks → 1 month.
  - A word counts as **Learned** at level 4 (you've remembered it on schedule for about two weeks).
  - **Still learning** sends a card back to level 1; it comes up again the same day.
- **Practice** lets you go through any cards, any time. Remembering a card that isn't due yet is practice only: its level doesn't change, so it doesn't count toward Learned.
- **ES → EN / EN → ES** switches which side is shown first. English-first shows the meaning and the Spanish example with the word blanked out.
- The speaker buttons read the word and sentence aloud with your device's Spanish voice (if it has one).
- Nouns show their article with a colour: teal = masculine (el), pink = feminine (la), purple = either. Verbs show present (yo / tú / él) and past forms on the answer side.
- Pick a group from the dropdown: frequency groups of 100, or topics (food, travel, family…).

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
| `Services/WordRepository.cs` | Loads `wwwroot/data/words.json` |
| `Services/ProgressTransfer.cs` | Export to JSON/CSV and import from them |
| `wwwroot/js/download.js` | Saves exported files and reads Spanish aloud |
| `Services/ProgressStore.cs` | Saves known/practicing words and shuffle order to localStorage |
| `wwwroot/data/words.json` | 1,000 words with example, topic, gender/article, verb forms |
| `wwwroot/css/app.css` | Styles (mobile-first) |

To add or edit words, change `words.json` – order in the file is the rank.
