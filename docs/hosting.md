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
   **Origin header** `https://joancomasfdz.github.io` (no OAuth needed for this). Done: it is the
   `BungieApiKey` in `wwwroot/appsettings.json`.
2. **A DIM API key for the site's origin, from the DIM team.** DIM's self-service registration
   (`new_app`, what its `/developer` page does) only registers local development origins — localhost,
   private addresses, `.lan`/`.local`/`.internal` hosts. It refused `https://joancomasfdz.github.io`
   (2026-10-11, HTTP 400 `InvalidRequest`). A key for a public site comes from DIM's maintainers: ask
   them (DIM's GitHub, https://github.com/DestinyItemManager/DIM, or its Discord) for an app `loopsmith`
   with origin `https://joancomasfdz.github.io`. For a key that works in a local run only:
   ```bash
   curl -sS https://api.destinyitemmanager.com/new_app -H 'Content-Type: application/json' \
     -d '{"id": "loopsmith-local", "bungieApiKey": "<a Bungie API key for http://localhost:5000>", "origin": "http://localhost:5000"}'
   ```
   The answer's `app.dimApiKey` is the key (don't merge a localhost key: it doesn't work on the site).
3. **Set it** in `src/Loopsmith.Web/wwwroot/appsettings.json` (`"DimApiKey": "<key>"`) and merge. It
   isn't a secret: it ships in the site, as DIM's own key ships in DIM, and only works from the
   registered origin. PR previews share that origin, so they open dim.gg links too; a local run
   (`localhost`) doesn't, unless the key is registered for that origin.

## Bungie login

The designer's Stats section loads a build's stats from the player's own armor
([loop-format.md](loop-format.md#stats)): the player logs in with Bungie, and Loopsmith reads their characters,
inventory and vault. Bungie's login is OAuth; Loopsmith is a **public** client (a site has no secret to keep), so
an access token lasts an hour and comes with no refresh token, which is all it needs: the token serves two reads
and is dropped, and nothing is kept, in the tab or anywhere else. One-time setup (the owner, at
https://www.bungie.net/en/Application, on the application whose key is `BungieApiKey`):

* **OAuth Client Type** Public; **Redirect URL** `https://joancomasfdz.github.io/destiny-loopsmith/login`
  (Bungie allows one; it must match exactly); **Scope** "Read your Destiny 2 information (Vault, Inventory, and
  Vendors)"; **Origin Header** `https://joancomasfdz.github.io`.
* Its **OAuth client_id** goes in `wwwroot/appsettings.json` as `BungieClientId` (it isn't a secret: it ships
  in the site). Without it (or without `BungieApiKey`) the Stats section offers only typing.

How it runs: "Log in with Bungie" opens Bungie's authorize page in a window of its own
(`https://www.bungie.net/en/oauth/authorize?client_id=…&response_type=code&state=…`, the state a random value
the tab checks). Bungie sends the player back to the redirect URL — `wwwroot/login.html`, which GitHub Pages
serves at `…/login` (the app's `/login` route does the same, should a host serve the app there instead) — and
that page hands Bungie's answer to the tab that asked over a same-origin `BroadcastChannel`, then closes. The tab
trades the code for a token (`POST https://www.bungie.net/platform/app/oauth/token/`, the client id and no
secret), reads the account's Destiny membership (`/Platform/User/GetMembershipsForCurrentUser/`) and its profile
(`/Platform/Destiny2/<type>/Profile/<id>/?components=102,200,201,205,300,305`), each with `X-API-Key` and the token.
A PR preview is on the same origin, so the live site's login page answers its tab too; until the login page is on
the live site (the PR that adds it, before it is merged), point the Redirect URL at the preview's
(`…/destiny-loopsmith/pr-preview/pr-<n>/login`) to try it, then back. A browser that blocks pop-ups is asked to
allow them; a local run (`localhost`) needs an application registered for that origin and redirect URL.

## Bungie's manifest

For a DIM link's items the bundled excerpt (`rules/manifest.yaml`) lacks, the browser reads Bungie's full
manifest ([ADRs D14](../ADRs.md)): it downloads seven English component files from bungie.net (~9 MB
compressed, the item table most of it; public, cross-origin, cached by the browser for hours — the app stores
nothing itself), indexes them in the page (`wwwroot/js/manifest.js`), and Core reads the entries it needs.
Their paths come from the manifest index (`https://www.bungie.net/Platform/Destiny2/Manifest/`, sent with
`BungieApiKey` from `wwwroot/appsettings.json`: Loopsmith's key, registered for the site's origin, so it
isn't a secret — it ships in the site and Bungie answers it from that origin); when the index doesn't
answer, from `ManifestContentId` there — the content id of the manifest the excerpt was made from. Update it when you
regenerate the excerpt (`tools/manifest/get-manifest.sh` prints it). Only paths in bungie.net's component
folder are taken from the index. The tab remembers which items it read the manifest for, so the designer
doesn't read again what Home just read (or failed to).

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
