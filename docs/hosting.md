# Hosting the web app

The web app (`src/Loopsmith.Web`) is a Blazor WebAssembly app: after `dotnet publish` it is a
folder of static files. The engine runs in the browser; there is no server, and shared loops
travel inside the link (`#loop=…`), so any static host works.

## GitHub Pages (default)

The site is served from the **`gh-pages` branch**:

| What | Where | Workflow |
|---|---|---|
| `main` — the live app | `https://<owner>.github.io/<repo>/` | `.github/workflows/pages.yml` (push to `main`, or run by hand) |
| every open pull request — its dev preview | `https://<owner>.github.io/<repo>/pr-preview/pr-<number>/` | `.github/workflows/pr-preview.yml` (PR opened / updated; removed when closed) |

For this repository: https://joancomasfdz.github.io/destiny-loopsmith/ and
`https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<number>/`.

Both workflows test the engine, publish the app and run `tools/web/prepare-pages.sh`, which sets
`<base href>` to where the copy is served and drops the pre-compressed `index.html.br/.gz` (they'd
keep the old base; Pages compresses on its own). For the main site only it adds `.nojekyll` (Pages
would otherwise hide `_framework/`; it reads the file only at the site root, so previews don't get
one) and a `404.html` deep-link fallback that also sends deep links inside a preview
(`…/pr-preview/pr-7/compare`) back to that preview's start page. The main deploy
never touches `pr-preview/`; previews never touch the root. The preview workflow comments the
link on the pull request. Pull requests from forks get no preview (no write access).

One-time setup:

1. Merge to `main` once so `pages.yml` creates the `gh-pages` branch with the app at its root.
2. **Settings → Pages → Build and deployment → Source: "Deploy from a branch"**, branch
   **`gh-pages`**, folder **`/ (root)`**. (The "GitHub Actions" source can't host previews.)
3. If a deploy fails with a permission error: **Settings → Actions → General → Workflow
   permissions → "Read and write permissions"**.

The site is public on the internet (the repository is public). It contains the authored rules —
numbers from Clarity and the Compendium snapshot, with paraphrased descriptions and row citations —
but no Compendium snapshot text and no transcripts.

## DIM links

Home starts a loop from a DIM link ([loop-format.md](loop-format.md#starting-from-a-dim-link)). A link
that carries its loadout (`…/loadouts?loadout=…`) needs nothing. A **dim.gg** share — what DIM's
Share button copies — is on DIM's servers, and DIM's API hands it only to registered apps: the web
app sends the app's key as `X-API-Key` from the origin the key was registered for. Until a key is
set, a dim.gg link says Loopsmith can't open it yet. One-time setup (the owner):

1. **A Bungie API key.** At https://www.bungie.net/en/Application create an application with
   **Origin header** `https://joancomasfdz.github.io` (no OAuth needed for this).
2. **Register Loopsmith with DIM's API** (what DIM's own `/developer` page does; the app id is
   3+ lowercase letters, digits or dashes):
   ```bash
   curl -sS https://api.destinyitemmanager.com/new_app -H 'Content-Type: application/json' \
     -d '{"id": "loopsmith", "bungieApiKey": "<the Bungie API key>", "origin": "https://joancomasfdz.github.io"}'
   ```
   The answer's `app.dimApiKey` is the key.
3. **Set it** in `src/Loopsmith.Web/wwwroot/appsettings.json` (`"DimApiKey": "<key>"`) and merge. It
   isn't a secret: it ships in the site, as DIM's own key ships in DIM, and only works from the
   registered origin. PR previews share that origin, so they open dim.gg links too; a local run
   (`localhost`) doesn't, unless the key is registered for that origin.

## Alternatives

* **Cloudflare Pages / Netlify:** free for private repositories. Build in GitHub Actions with
  the same publish + prepare steps (use base href `/` on a root domain) and deploy the
  `publish/wwwroot` folder with the host's CLI action and an API token stored as a
  repository secret.
* **Azure Static Web Apps (free tier):** has a GitHub Action and supports Blazor WebAssembly.

## Run it locally

```bash
dotnet run --project src/Loopsmith.Web        # dev server, http://localhost:5000 (dotnet watch for hot reload)
# or the published static files, served at the root:
dotnet publish src/Loopsmith.Web -c Release -o publish && python3 -m http.server -d publish/wwwroot 8080
# at a sub-path, as Pages serves it: tools/web/prepare-pages.sh publish/wwwroot /destiny-loopsmith/ --root,
# then serve the parent of a destiny-loopsmith/ copy of publish/wwwroot
```
