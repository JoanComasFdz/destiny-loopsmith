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

Every pull request that adds or changes end-to-end scenarios also gets them posted on it, recorded by the session that
works it ([E2E videos on pull requests](#e2e-videos-on-pull-requests)).

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
(`/Platform/Destiny2/<type>/Profile/<id>/?components=102,200,201,205,300,305,310`: 310 is each weapon's reusable
plugs, the perks a column can switch to), each with `X-API-Key` and the token.
A PR preview is on the same origin, so the live site's login page answers its tab too; until the login page is on
the live site (the PR that adds it, before it is merged), point the Redirect URL at the preview's
(`…/destiny-loopsmith/pr-preview/pr-<n>/login`) to try it, then back. A browser that blocks pop-ups is asked to
allow them; a local run (`localhost`) needs an application registered for that origin and redirect URL.

## Bungie's manifest

When the app opens, the browser loads Bungie's full manifest ([ADRs D14](../ADRs.md)) and keeps it for as long as the
tab is open, as DIM does, and saves a copy for the next visit (below): the weapon and armor pickers' **Every item**, a DIM link's items the bundled excerpt
(`rules/manifest.yaml`) lacks, and a Bungie login's armor and weapons are all read from it, and no read downloads it
again. It downloads eight English component files from bungie.net (~9 MB compressed, the item table most of it;
public, cross-origin), indexes them in the page
(`wwwroot/js/manifest.js`), and Core reads the entries it needs. Their paths come from the manifest index
(`https://www.bungie.net/Platform/Destiny2/Manifest/`, sent with `BungieApiKey` from `wwwroot/appsettings.json`:
Loopsmith's key, registered for the site's origin, so it isn't a secret — it ships in the site and Bungie answers it
from that origin); when the index doesn't answer, from `ManifestContentId` there — the content id of the manifest the
excerpt was made from. Update it when you regenerate the excerpt (`tools/manifest/get-manifest.sh` prints it). Only
paths in bungie.net's component folder are taken from the index. The tab remembers which items it read the manifest
for, so the designer doesn't read again what Home just read (or failed to).

**The saved copy.** Once the load is done, the browser saves the files (gzipped: ~9 MB of the ~213 MB unpacked) and
the listing of every weapon and armor piece in its IndexedDB (`loopsmith-manifest`), each in place of the one saved
before ([ADRs D25](../ADRs.md)). The next visit (a reload, a new tab) asks the manifest index for the current version
first: when the saved files are that version, it reads them instead of downloading (unpacking and indexing them as a
download does), and the listing too when the same build of Loopsmith's code made it (a deploy that changes the code
lists again from the saved files; nothing is downloaded). A new version downloads and lists as a first visit does, and
replaces the copy. When the index doesn't answer, the saved copy is read; without one, the pinned version downloads.
A browser that can't save (storage blocked or full, no IndexedDB) downloads at every visit, as before; a saved copy
that doesn't read is downloaded again.

**The startup screen.** Until the load is done, a screen stands in for every page (Home, the designer opened from a
share link, Compare) and the screen tabs are hidden: "Checking Bungie's manifest" while it asks for the version, then
"Downloading Bungie's manifest". Its first bar is the files' alone ("40 MB", unpacked: the bar can't know their size,
bungie.net sends them compressed, so it fills ever more slowly towards the size they had in 2026), full once they're
all in ("213 MB · done"). Under **Listing every weapon and armor piece**, each of the listing's four steps has its own
bar, waiting ("next"), at work ("at work…") or done: reading the slots, armor sets and breakers; finding every weapon and
armor piece (the long one, a few seconds); reading the weapons' frames; finding each weapon's breaker. A step can't say
how far it got, so a sheen runs over the bar at work: a CSS animation the browser keeps playing while .NET is busy with
the step. Before each step the page waits for the browser to paint, so the bars move as the steps do, and a full bar
fills at once (a width transition would stop half-way while the next step holds the page's thread). With a saved copy
it is "Reading Bungie's manifest": the first bar reads **Reading the saved copy** ("84 of 213 MB") and the steps are
full at once ("from the saved copy"), unless the code changed and they run again. Then the page the link points to
opens. Measured from a session on a desktop: the screen shows for ~10 s (~4 s the download, ~5 s the listing), after
the app's own start (~7 s, as before); with a saved copy, ~4 s (the version check, then ~2 s reading the copy).
Bungie's login redirect (`login`, should the host serve the app there instead of `login.html`) neither loads the
manifest nor waits for it.

**When the connection breaks.** A file whose download breaks is downloaded again from its start, up to 3 times, a
moment later each time; the files already in stay, and the bar says so ("connection lost, trying again: 2 of 3"). A
broken file isn't resumed where it stopped: bungie.net sends it compressed, and a browser can ask for the rest of a file
only uncompressed (from the middle of the item table, ~99 MB instead of the whole file's 8.4 MB). When it still fails,
the screen says why with **Try again** (the load runs again; the files already in stay) and **Continue without it**:
the app opens with its bundled data, and the next read that needs the manifest (a DIM link, the login, Every item)
loads it then, with the read's progress bar, and keeps it.

The pickers' **Every item** ([loop-format.md](loop-format.md#every-item)) lists every weapon and armor piece of the
game from the item table, the sandbox perks and four small components. Core asks, in four rounds, for the small
components, then the items of the weapon and armor buckets, each cut down in the page to the fields a square needs and,
for a weapon, the plugs its sockets start with (one pass over the file, `readEntryParts`: ~10,000 entries, ~5 MB of
JSON), then the weapons' intrinsic frames and their perks — a weapon's intrinsic is its first named intrinsic plug (the
rules know an exotic weapon by it), and its breaker is its own, else its frame's ("[Shield-Piercing] Barrier"), as the
join reads them. The startup load makes the list and the tab keeps it, so a picker opens at once (when the load was
skipped, the first picker reads it, and a failed read is tried again at the next opening). A pick then reads the full
manifest for that one item (its perks, catalyst or set), as above, even when the tab asked for it before.

The tab holds the files (~215 MB of JSON once unpacked, held once, never copied into one buffer) and their index for as
long as it is open, as DIM holds its own copy. The page keeps answering taps while a read names items: the join gives
the browser a turn before each of its rounds (five for a vault of ~600 items). After a Bungie login, the **Bungie login
section** shows a progress bar — "Reading Bungie's manifest for 609 of your items", "naming them (step 2)" — and so does
the ⋯ picker opened meanwhile (a DIM link's read says "of this build's items"; a pick "for the item you picked"). When
the startup load was skipped, the first read also downloads the files ("reading its files… 84 MB"). One read runs at a
time (a login during a DIM link's read waits for it).

## E2E videos on pull requests

Every pull request from a branch of this repository shows its end-to-end scenarios
([tests/Loopsmith.Web.E2E](../tests/Loopsmith.Web.E2E/README.md)) as GIFs, so a reviewer sees them working without
running anything. The session that works the PR records them; CI doesn't run, record or build anything for them.

- **Who records:** the session, before each push that adds or changes scenarios:
  `tools/e2e/record.sh --changed-since origin/main`, i.e. every scenario the branch adds or changes from where it left
  `main` (a class whose file changed, or that names a changed fixture file), not only those the push touched. Every
  scenario passes before the push. A PR that changes nothing under `tests/Loopsmith.Web.E2E/Scenarios` records and posts
  nothing, and runs no e2e workflow.
- **How the GIFs get to GitHub:** a session can't upload a file to GitHub (its proxy only lets JSON request bodies
  through: a release upload answers 415), but it can push. Right after the push (the first one once the PR is open),
  `tools/e2e/push-gifs.sh` pushes a throwaway tag **`e2e-gifs/pr-<n>-<short sha>`** on one commit that sits on the
  branch's head, already on GitHub, and adds `e2e-gifs/scenarios.json` and the GIFs
  (`e2e-gifs/<domain folder>/<gif name>.gif`). It finds the PR from the branch (or takes its number), stops when the
  head isn't pushed yet or that tag already exists, and never touches the working tree or makes a local tag. The commit
  sits on the PR's head, not on an empty history, because a push runs the workflow file inside the pushed commit: the
  PR's own `.github/workflows/e2e-videos.yml`.
- **Why a tag, not a branch:** GitHub's Pull requests page shows a "<branch> had recent pushes · Compare & pull
  request" banner for about an hour after a push to any branch, even one deleted seconds later (the session pushes as
  the owner); a tag push gets no banner. The PRs before this change pushed branches, which is why the cleanup still
  deletes leftover `e2e-gifs/pr-<n>-…` branches too. No ruleset may cover `refs/tags/e2e-gifs/*` (or all tags): it
  would refuse the session's push, or the workflow's deletion. On 2026-10-15 the only rulesets were "Protect gh-pages"
  and "main-protection", both on branches. A PR whose branch left `main` before this change still carries the old
  workflow, which only runs on branches: merge `main` into it before pushing its GIFs, or its tag triggers nothing (and
  stays until the PR closes).
- **What the workflow does** (`.github/workflows/e2e-videos.yml`, on a push of a tag `e2e-gifs/**`): it checks the push
  (the name is `e2e-gifs/pr-<n>-<hex>`; PR #n is open and from this repository; the commit sits on a commit of the PR
  and only adds `e2e-gifs/scenarios.json` and real GIFs), then `tools/e2e/post-scenarios.py --out e2e-gifs` uploads the
  GIFs and posts them, and the tag is deleted whatever happened (the run's log says why one failed). A push that fails
  the checks uploads and posts nothing. No `dotnet`, no Chromium, no ffmpeg: a run takes seconds.
- **Where the GIFs go:** assets of the pre-release **`prs-resources`** (created once by the owner; never committed
  anywhere). A release asset embedded as `![title](https://github.com/<owner>/<repo>/releases/download/prs-resources/<name>)`
  shows inline in GitHub (an MP4 there only shows as a link, so they are GIFs). Uploads use the workflow's
  `GITHUB_TOKEN` (contents and pull-requests write); no personal token.
- **Naming:** `pr-<PR number>-v<version>-<scenario>.gif`, e.g.
  `pr-82-v1-given-app-is-open-on-home-when-i-click-start-from-scratch-then-the-designer-shows-an-arc-hunter-with-nothing-equipped.gif`.
  - `pr-<n>-` first, so a PR's assets sit together and its cleanup finds them (the dash after the number keeps
    `pr-7-` from matching `pr-70-`);
  - `v<version>`: 1, 2, 3… (no padding), one per `push-gifs.sh`, before the scenario so one version's GIFs sit
    together. It is one more than the highest `## Scenarios (V<N>)` already in the workflow's comments on the PR
    (those by github-actions ending with its footer): nothing is stored anywhere else, and a reopened PR carries on
    from its last number;
  - `<scenario>`: the scenario's GIF name, the same as in `record.sh`'s output (`ScenarioNaming.ToGifName`: the
    `Given…_When…_Then…` method name in kebab-case, only a–z, 0–9 and `-`, which GitHub keeps as they are). Only when
    the whole name would pass 200 characters does it keep its start and end with `-<8 hex>`, the start of the SHA-256
    of the method name, so it stays unique and the same on every push.
- **The posts:** every version, V1 included, is a new comment on the PR, `## Scenarios (V<N>)`, that starts with
  **What changed**: the body of the GIFs' commit message without its trailers, or its subject when it has no body.
  `push-gifs.sh` copies the PR head's commit message there (a session writes a short plain description of the round in
  its body), or writes `--message "<text>"` as the body. Each scenario is its Given/When/Then title as a
  small heading with its GIF below; a failed one has ❌ before its title. Every post ends with `---` and
  `_Recorded by the e2e workflow · Claude Code_`. The workflow never touches the PR's description, so editing the
  description can't break a post.
- **Failures:** a failing scenario still gets its GIF and its ❌ (the session fixes it before pushing, so it shouldn't
  happen), and never turns the run red. The run is red only when the workflow itself failed: a push that isn't a PR's
  GIFs, or GitHub refused an upload or a post.
- **One post per PR at a time** (the posting job's concurrency group is per PR, never cancelled midway), so two runs
  can't post the same version. GitHub keeps one waiting job per group: a post that is still waiting when a newer one
  arrives is skipped (its tag is deleted all the same), so push the GIFs once per round.
- **Cleanup:** when the PR is closed or merged, the workflow deletes any `e2e-gifs/pr-<n>-…` tag or branch left behind, keeps every GIF of the PR's newest version and deletes
  its older versions' (`pr-<n>-v1-…` to `pr-<n>-v<N-1>-…`), so the PR's last `## Scenarios (V<N>)` post keeps showing
  its GIFs for good, and the older posts show broken images. The newest version is the highest `v<N>` among the PR's
  assets, read as a number (`v10` is newer than `v2`, and `v1` never matches `v10`). Only names of the form
  `pr-<n>-v<N>-…` are ever deleted: the owner's own assets (not starting with `pr-`), other PRs' assets and any other
  name are never touched. A reopened PR carries on from its last version, so closing it again keeps only its newest
  version's GIFs. The job's log names each deleted asset, the version it kept and how many assets the release holds.
- **The release's limit:** GitHub sets no total size limit on a release, but a release holds at most 1,000 assets. The
  cleanup's log says how many `prs-resources` holds, with a warning above 900. Near the limit, the owner creates a new
  pre-release (e.g. `prs-resources-2`) and points `RELEASE` in `tools/e2e/post-scenarios.py` and the `clean` job of
  `.github/workflows/e2e-videos.yml` at it. The old release stays, so the posts that embed its GIFs keep showing
  them. Nothing does this automatically yet.
- Pull requests from forks are skipped (they never get a write token).

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
