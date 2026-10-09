# Hosting the web app

The web app (`src/Loopsmith.Web`) is a Blazor WebAssembly app: after `dotnet publish` it is a
folder of static files. The engine runs in the browser; there is no server, and shared loops
travel inside the link (`#loop=…`), so any static host works.

## GitHub Pages (default)

`.github/workflows/pages.yml` tests the engine, publishes the app, rewrites `<base href>` to
`/<repo>/`, adds `404.html` (deep-link fallback) and `.nojekyll` (Pages would otherwise hide
`_framework/`), and deploys.

One-time setup:

1. **Settings → Pages → Build and deployment → Source: GitHub Actions.**
2. Merge to `main` (the workflow deploys from `main`; it can also be started from the
   Actions tab with *Run workflow*).
3. The site appears at `https://<owner>.github.io/<repo>/`, e.g.
   `https://joancomasfdz.github.io/destiny-loopsmith/`.

Things to know:

* **Private repositories** can publish Pages only on a paid plan (GitHub Pro, Team or
  Enterprise). On the free plan, the Pages settings won't offer it for a private repo.
* A Pages site is **public on the internet** even when the repository is private (only
  Enterprise Cloud can restrict access). The site contains the authored rules (with numbers
  taken from Clarity) but no Compendium data and no transcripts. Check Clarity's
  partnerships page before sharing the URL widely.

## Alternatives (private repo on the free plan)

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
