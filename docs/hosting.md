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
keep the old base; Pages compresses on its own). It also adds each own stylesheet's and script's content hash to
its URL in `index.html` and `login.html` (`css/app.css?v=…`): Pages lets a browser keep a file for ten minutes, and
without it a fresh deploy's markup could meet the last deploy's cached `app.css` (`_framework/` is fingerprinted
already). For the main site only it adds `.nojekyll` (Pages
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
Share button copies — is on DIM's servers, and DIM's API hands it to any caller: `GET
https://api.destinyitemmanager.com/loadout_share?shareId=…` needs no API key and answers any origin
(dim-api's `api/server.ts` declares it before its API-key check, with CORS open to every origin; since
2022). The web app asks with a plain GET and **no header**: a header such as `X-API-Key` would make the
browser ask DIM first (a CORS preflight), which DIM answers only for the origins of apps registered
with it. Nothing to set up, on the site, in a PR preview or in a local run.

A DIM API key (an app registered with DIM: self-service `new_app` for local origins, from DIM's
maintainers for a public site — dim-api's README) is only for DIM Sync, a player's own DIM data
(loadouts, tags, notes, searches), which Loopsmith doesn't read. Loopsmith's **Bungie** API key
(`BungieApiKey` in `wwwroot/appsettings.json`, an application at https://www.bungie.net/en/Application
with **Origin header** `https://joancomasfdz.github.io`) is for Bungie's manifest and login, below.

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

The pickers' **Every item** ([loop-format.md](loop-format.md#every-item)) lists every weapon and armor piece of the
game from the same files, read once per tab when a picker first opens: the item table, the sandbox perks and four small
components (~8 MB compressed). Core asks, in four rounds, for the small components, then the items of the weapon and
armor buckets, each cut down in the page to the fields a square needs and, for a weapon, the plugs its sockets start
with (one pass over the file, `readEntryParts`: ~10,000 entries, ~5 MB of JSON), then the weapons' intrinsic frames and
their perks — a weapon's intrinsic is its first named intrinsic plug (the rules know an exotic weapon by it), and its
breaker is its own, else its frame's ("[Shield-Piercing] Barrier"), as the join reads them.
The tab keeps the list it reads (a failed read is tried again at the next opening). A pick then reads the full manifest
for that one item (its perks, catalyst or set), as above, even when the tab asked for it before.

The page keeps answering taps while it reads: each file is indexed as its chunks arrive (~215 MB of JSON once
unpacked, held once, never copied into one buffer), with a pause every 8 MB, and the join gives the browser a turn
before each of its rounds (five for a vault of ~600 items). After a Bungie login, the **Bungie login section** shows a
progress bar — "Reading Bungie's manifest for 609 of your items", "reading its files… 84 MB" (unpacked), then "naming
them (step 2)" — and so does the ⋯ picker opened meanwhile (a DIM link's read says "of this build's items"; a
picker's first opening "for every weapon and armor piece", a pick "for the item you picked"); the bar
can't know the files' unpacked size (bungie.net sends them compressed), so it fills ever more slowly towards the size
they had in 2026. One read runs at a time (a login during a DIM link's read waits for it). Once the join has what it
asked for, the files are let go, unless another read waits for them: a later read downloads them again, from the
browser's cache.

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
