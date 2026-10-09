# Hosting the web app

The web app (`src/Loopsmith.Web`) is a Blazor WebAssembly app: after `dotnet publish` it is a
folder of static files. The engine runs in the browser; there is no server, and shared loops
travel inside the link (`#loop=…`), so any static host works.

## GitHub Pages (default)

The site is served from the **`gh-pages` branch**:

| What | Where | Workflow |
|---|---|---|
| `main` — the live app | `https://<owner>.github.io/<repo>/` | `.github/workflows/pages.yml` (push to `main`) |
| every open pull request — its dev preview | `https://<owner>.github.io/<repo>/pr-preview/pr-<number>/` | `.github/workflows/pr-preview.yml` (PR opened / updated; removed when closed) |

Both workflows test the engine, publish the app and run `tools/web/prepare-pages.sh`, which sets
`<base href>` to where the copy is served, adds `.nojekyll` (Pages would otherwise hide
`_framework/`) and, for the main site, a `404.html` deep-link fallback that also sends deep links
inside a preview (`…/pr-preview/pr-7/compare`) back to that preview's start page. The main deploy
never touches `pr-preview/`; previews never touch the root. The preview workflow comments the
link on the pull request. Pull requests from forks get no preview (no write access).

One-time setup:

1. Merge to `main` once so `pages.yml` creates the `gh-pages` branch with the app at its root.
2. **Settings → Pages → Build and deployment → Source: "Deploy from a branch"**, branch
   **`gh-pages`**, folder **`/ (root)`**. (The "GitHub Actions" source can't host previews.)
3. If a deploy fails with a permission error: **Settings → Actions → General → Workflow
   permissions → "Read and write permissions"**.

The site is public on the internet (the repository is public). It contains the authored rules
(numbers taken from Clarity) but no Compendium data and no transcripts.

## Alternatives

* **Cloudflare Pages / Netlify:** free for private repositories. Build in GitHub Actions with
  the same publish + prepare steps (use base href `/` on a root domain) and deploy the
  `publish/wwwroot` folder with the host's CLI action and an API token stored as a
  repository secret.
* **Azure Static Web Apps (free tier):** has a GitHub Action and supports Blazor WebAssembly.

## Run it locally

```bash
dotnet run --project src/Loopsmith.Web        # dev server with hot reload
# or exactly what Pages serves:
dotnet publish src/Loopsmith.Web -c Release -o publish && python3 -m http.server -d publish/wwwroot 8080
```
