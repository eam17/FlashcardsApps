# Putting Palabras on GitHub Pages

This folder is ready to be a GitHub repository. A workflow (`deploy.yml`) builds the app and publishes it every time you push changes. GitHub only runs it from a `.github/workflows` folder, so step 6 moves it there.

## One-time setup (about 10 minutes)

1. **Create a GitHub account** at github.com if you don't have one.
2. **Install GitHub Desktop** from desktop.github.com and sign in.
3. In GitHub Desktop: **File → Add local repository…** → choose this `FlashcardsApp` folder.
   It will say it isn't a repository yet — click **create a repository** and then **Create repository** (keep the name, e.g. `FlashcardsApp`).
4. Click **Publish repository**. Untick *Keep this code private* if you're on a free plan (Pages on private repos needs a paid plan). Click **Publish**.
5. On github.com, open the repository → **Settings → Pages** → under *Build and deployment*, set **Source** to **GitHub Actions**.
6. Add the workflow: on the repository's **Code** tab click **Add file → Create new file**. For the name type exactly
   `.github/workflows/deploy.yml`
   (typing the `/` creates the folders). Open `deploy.yml` from this folder in Notepad, copy everything, paste it in, and click **Commit changes**.
   Then in GitHub Desktop click **Fetch origin → Pull origin** so your computer has it too. (You can delete the loose `deploy.yml` afterwards.)
7. Go to the **Actions** tab and watch *Deploy to GitHub Pages* run. When it finishes (a green tick, ~2 minutes), your app is at:

   `https://<your-username>.github.io/FlashcardsApp/`

8. Open that on your phone → **Share → Add to Home Screen** (iPhone, Safari) or **⋮ → Add to Home screen** (Android, Chrome).

## Updating the app later

Change the files, then in GitHub Desktop write a short summary, click **Commit to main** and **Push origin**. The site updates a couple of minutes later.

## Notes

- Progress is saved separately on each device (in the browser). Use **Export JSON** / **Import** to move it between your PC and phone.
- `bin`, `obj` and `.vs` folders are ignored (see `.gitignore`) — they're build files and don't need to be uploaded.
