# Hosting the web app

The web app (`src/Loopsmith.Web`) is a Blazor WebAssembly app: after `dotnet publish` it is a
folder of static files. The engine runs in the browser; there is no server, and shared loops
travel inside the link (`#loop=…`), so any static host works.

## GitHub Pages (default)

The site is served from the **`gh-pages` branch**:

| What | Where | Workflow |
|---|---|---|
| `main` — the live app | `https://<owner>.github.io/<repo>/` | `.github/workflows/pages.yml` (push to `main`, or run by hand) |
| every open pull request — its dev preview | `https://<owner>.github.io/<repo>/pr-preview/pr-<number>/` | `.github/workflows/pr-preview.yml`, called by `ci.yml` with the site its `build` job built, tested and published (PR opened / updated, when it changes a file the site is built from; removed when closed) |

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

## The VS Code extension

Every deploy publishes the VS Code extension (`src/Loopsmith.VsCodeExtension`, its
[README](../src/Loopsmith.VsCodeExtension/README.md)) for desktop VS Code as `vscode-extension/loopsmith-e2e-recordings.vsix`:
`Loopsmith.Web.csproj` zips it after `dotnet publish` (the `PublishVsCodeExtension` target), so `pages.yml` and
`ci.yml` (for `pr-preview.yml`) publish it unchanged, the live site's and each PR's preview's (`…/pr-preview/pr-<n>/vscode-extension/`).

vscode.dev can't install it from the site: its content security policy lets the page fetch only from `localhost` and a
few hosts of its own (`github.io` isn't one: "Cannot find a valid extension from the location"), and it has no "Install
from VSIX". It installs it from `http://localhost:5000`, served by a container the owner builds from the extension's
folder (`Dockerfile`, `serve.js`; the README's "Install"). Once installed, the extension itself runs in vscode.dev: its
extension host may fetch any `https` address (GitHub's API), and its panel loads
what its own policy allows. It reads each scenario's recordings from the scenario's release: `prs-resources` for a
scenario under `Scenarios/`, the rule group's `rules-<group>-resources` for a rule scenario under `Rules/` (a rule release
that doesn't exist yet reads as "No recording yet"), as [E2E videos on pull requests](#e2e-videos-on-pull-requests) says.

A GitHub Codespace or desktop VS Code's Dev Containers needs neither: VS Code runs a server in the container, which
installs a `.vsix`. The repository's dev container (`.devcontainer/`) packs the extension from its folder, as the
`PublishVsCodeExtension` target packs the site's, and installs it with VS Code's CLI each time VS Code attaches
(`7-install-e2e-recordings-extension.sh`), so a branch's Codespace has that branch's extension and nothing comes from the Marketplace.
The dev container also holds everything the repository builds, tests and records with
([.devcontainer/README.md](../.devcontainer/README.md)).

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

When the app opens, the browser loads Bungie's full manifest ([ADRs D14](ADRs.md)) and keeps it for as long as the
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
before ([ADRs D25](ADRs.md)). The next visit (a reload, a new tab) asks the manifest index for the current version
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
([tests/Loopsmith.Web.E2E](../tests/Loopsmith.Web.E2E/README.md), the web app's, and
[tests/Loopsmith.VsCodeExtension.E2E](../tests/Loopsmith.VsCodeExtension.E2E/README.md), the VS Code extension's) as GIFs, each with a **▶ Play MP4** link to the
same run as a sharp video with controls, so a reviewer sees them working without running anything. The session that
works the PR records them; CI doesn't run, record or build anything for them.

- **What is recorded:** each run (a scenario played on a screen) from Home on, its last frame held 3 s, as a GIF (20 fps;
  800 px wide on desktop, 390 px on a phone) and as an MP4 (H.264 at the recording's own size and frame rate: 1280×800
  on desktop, 780×1688 on a phone, 25 fps from Playwright's recorder). The GIF shows in the comment; the MP4 can be
  paused, sought and seen full screen in the player page below. Playwright's recorder takes Chromium's screencast,
  which hands it a phone emulated at twice the pixels at its 390×844 CSS pixels (blurry on a phone; asking for a
  780×1688 video only pads it with grey), so the phone's browser is started drawing at twice the pixels itself
  (`--force-device-scale-factor=2`, `BrowserRun`), and its screencast is the phone's 780×1688.

- **Who records:** the session, once, right before each push that adds or changes scenarios:
  `tools/e2e/record.sh --changed-since origin/main`, i.e. every scenario the branch adds or changes from where it left
  `main` (a class whose file changed, or that names a changed fixture file), not only those the push touched.
  `out/scenarios.json` keeps why each was picked: `"selected": "changed"` (its own file changed, or it was named on
  the command line), or `"selected": "fixtures"` with the changed `"fixtures"` (paths in its project:
  `Scenarios/Loop/Weapons/….build.yaml`, `Workspace/…`) when only a fixture it uses changed; a class that did both is
  `changed`. Every
  scenario passes before the push. While developing, the session runs them unrecorded
  (`tools/e2e/record.sh --no-video …`: same selection and served publish, no video, no pacing, no GIF) and never
  records between pushes; when a scenario or the web app changes after the recording and before the push, it records
  again, so the posted GIFs match the pushed head. A PR that changes nothing under `tests/Loopsmith.Web.E2E/Scenarios`, `tests/Loopsmith.Web.E2E/Rules`,
  `tests/Loopsmith.VsCodeExtension.E2E/Scenarios` or `tests/Loopsmith.VsCodeExtension.E2E/Workspace` (the files the
  extension's scenarios open) records and posts nothing, and runs no e2e workflow.
- **How the GIFs get to GitHub:** a session can't upload a file to GitHub (its proxy only lets JSON request bodies
  through: a release upload answers 415), but it can push. Right after the push (the first one once the PR is open),
  `tools/e2e/push-gifs.sh` pushes a throwaway branch **`e2e-gifs/pr-<n>-<short sha>`**: the branch's head, already on
  GitHub, plus one commit that adds `e2e-gifs/scenarios.json`, the GIFs and the MP4s
  (`e2e-gifs/<domain folder>/<gif name>.gif` and `.mp4`; the branch keeps its `e2e-gifs/` name), and with `--message`
  `e2e-gifs/what-changed.md`, the post's "What changed".
  It finds the PR from the branch (or takes its number), stops when the head isn't pushed yet or that branch already
  exists, and never touches the working tree. The branch sits on the PR's head, not on an empty history, because a push
  runs the workflow file inside the pushed commit: the PR's own `.github/workflows/e2e-videos.yml`.
- **Why a branch, not a tag:** a push to any branch makes GitHub's Pull requests page show a "<branch> had recent
  pushes · Compare & pull request" banner for about an hour, even after the branch is deleted. A tag would avoid it, but
  a session can't make one: on 2026-10-15 its git proxy refused a tag push (`HTTP 403`, PR #90), and GitHub's REST API
  refused every Git-data write (`git/blobs`, `git/trees`, `git/refs`: "Write access to this GitHub API path is not
  permitted through this proxy"). So the GIFs travel on a branch, and the banner is the accepted cost: ignore it.
- **What the workflow does** (`.github/workflows/e2e-videos.yml`, on a push to `e2e-gifs/**`): it checks the push
  (the name is `e2e-gifs/pr-<n>-<hex>`; PR #n is open and from this repository; the commit sits on a commit of the PR
  and only adds `e2e-gifs/scenarios.json`, `e2e-gifs/what-changed.md`, real GIFs (they start with `GIF8`) and real MP4s (an `ftyp` box first)), then
  `tools/e2e/post-scenarios.py --out e2e-gifs` uploads the GIFs and MP4s, each to its release (below), and posts them, and the branch is deleted whatever happened (the run's log says why one failed). A push that fails
  the checks uploads and posts nothing. No `dotnet`, no Chromium, no ffmpeg: a run takes seconds.
- **Where the GIFs and MP4s go:** assets of a pre-release, never committed anywhere. Which one is the run's GIF's folder
  (its scenario's domain folder):
  - **a rule scenario's** (`tests/Loopsmith.Web.E2E/Rules/<…>/`, one scenario per rule claim, its GIF under `Rules/<…>/`)
    go to its **rule group's release**, one per rule file: `rules-` + the folders under `Rules/`, kebab-cased and joined
    by dashes, + `-resources`. `rules/hunter/arc/melees.yaml` → `Rules/Hunter/Arc/Melees/CombinationBlowTests.cs` →
    `rules-hunter-arc-melees-resources`; likewise `rules-all-classes-arc-fragments-resources`,
    `rules-hunter-class-abilities-resources`, `rules-mods-arms-resources`. The workflow creates a rule release on its
    first upload, as a pre-release titled with its tag (its `GITHUB_TOKEN` can; a session can't), so nobody creates them
    by hand. Each keeps the latest recording of every rule of its file (Cleanup, below);
  - **any other** (`Scenarios/`, the web app's loop scenarios and the extension's) go to **`prs-resources`** (created once
    by the owner).

  One push can feed several releases: the workflow uploads to each in turn. A release asset embedded as
  `![title](https://github.com/<owner>/<repo>/releases/download/<release>/<name>)` shows inline in GitHub (an MP4 there
  only shows as a link, so the comment shows the GIF and links the MP4 to the player page below). Uploads use the
  workflow's `GITHUB_TOKEN` (contents and pull-requests write); no personal token.
- **The player page for MP4s:** GitHub draws a video player only for a video it stored itself, from a file dropped
  into its web editor (`github.com/user-attachments/…`, which only a logged-in browser can upload). Its pages' content
  security policy lets them play media only from GitHub's own hosts, and a release asset redirects to one that isn't
  among them, so no markdown can show a player for an MP4 in a release. The site has a page
  that plays one: **`…/destiny-loopsmith/e2e-player.html?v=<asset>[&r=<release>]`** (`src/Loopsmith.Web/wwwroot/e2e-player.html`,
  published with the app on the live site and every preview), with the browser's own controls (play/pause, seeking,
  fullscreen). `v` is an asset's name only, `pr-<n>-v<k>-<run>.mp4` (letters, digits and `-` in `<run>`); `r` is the
  release, `prs-resources` or a rule group's `rules-<group>-resources`, and `prs-resources` when left out, so the links
  posted before rule releases keep playing (the posts give `r` only for a rule release). The page builds the URL itself,
  `https://github.com/<owner>/<repo>/releases/download/<r>/<v>`, so it never plays a URL someone else chose, and any
  other `v` or `r` shows a short message. GitHub sends release assets as downloads, but a
  `<video>` element reads the file itself and plays it. Above the video: the scenario's name from `<run>`, a link to
  PR #n, the version, the screen and the release. An asset that is gone shows a message instead: in `prs-resources`, an
  older version deleted when its PR closed; in a rule release, a recording replaced by a newer PR's, or of a PR closed
  without merging.
- **Naming:** `pr-<PR number>-v<version>-<run>.gif`, and the same name ending in `.mp4` for its MP4, e.g.
  `pr-82-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif` and `….mp4`.
  - `pr-<n>-` first, so a PR's assets sit together and its cleanup finds them (the dash after the number keeps
    `pr-7-` from matching `pr-70-`);
  - `v<version>`: 1, 2, 3… (no padding), one per `push-gifs.sh`, before the scenario so one version's GIFs sit
    together. It is one more than the highest `## Scenarios (V<N>)` already in the workflow's comments on the PR
    (those by github-actions ending with its footer): nothing is stored anywhere else, and a reopened PR carries on
    from its last number;
  - `<run>`: the run's GIF name, the same as in `record.sh`'s output (`ScenarioNaming.ToGifName`: the scenario's
    method name, its Gherkin's title, in kebab-case, then `-desktop` or `-mobile`; only a–z, 0–9 and `-`, which GitHub
    keeps as they are). Only when the whole name would pass 200 characters does it keep its start and end with
    `-<8 hex>`, the start of the SHA-256 of the GIF's path (`<domain folder>/<GIF name>.gif`), so it stays unique (a scenario's two screens apart) and the
    same on every push.
- **The posts:** every version, V1 included, is a new comment on the PR, `## Scenarios (V<N>)`, that starts with
  **What changed**: `push-gifs.sh --message "<text>"`'s text, word for word, or else the body of the PR head's commit
  message without its trailers (a session writes a short plain description of the round in its body), or its subject
  when it has no body. `--message` travels as `e2e-gifs/what-changed.md`, posted as it is, not in the commit message:
  there a one-paragraph text like `AFTER: …` looks like a trailer (`Key: value`) and was dropped, so the post fell back
  to the subject (PR #106's V2). Each post reads only its own push's commit, so two posts from the same head commit,
  or posts on several PRs at once, each show their own text. Each scenario shows once: its Gherkin's title as a small
  heading, its Gherkin in a `gherkin` block, then a `🖥️ Desktop` and/or `📱 Mobile` subtitle (the screens it was played
  on, desktop first) with that run's GIF below, and below it **▶ Play MP4**. The GIF and that line both link to the
  player page with that run's MP4 (`https://<owner, lowercase>.github.io/<repo>/e2e-player.html?v=<MP4 name>`), so a tap
  on the GIF opens the video instead of downloading the GIF (GitHub can't make a link open a new tab, so a long-press
  does). A run with no MP4 (a `scenarios.json` from before the MP4s) shows its GIF only. A failed run has ❌ before its subtitle, and its scenario before its
  title. The scenarios the PR added or changed come first; those only re-recorded because a fixture they use changed
  (`"selected": "fixtures"`) follow under `### Re-recorded: their fixtures changed`, each with a line naming its changed
  fixtures, so the owner can check that the fixture change left their behaviour as it was (PR #113's post listed the
  Weapons scenarios its `*.build.yaml` changes re-recorded like its own, and read as another PR's). With both kinds,
  the first sits under `### Added or changed by this PR`, a scenario's title is `####` and its screens `#####`; a
  section with no scenarios isn't shown. With only the PR's own (or a `scenarios.json` from before `selected`), there
  are no section headings: titles `###`, screens `####`. `tools/e2e/test_post_scenarios.py` tests the layout
  (`python3 -m unittest tools/e2e/test_post_scenarios.py`; CI doesn't run it). Every post ends with `---` and
  `_Recorded by the e2e workflow · Claude Code_`. The workflow never touches the PR's description, so editing the
  description can't break a post.
- **Earlier versions collapse:** once a new post is up, `post-scenarios.py` edits each earlier post still open into a
  closed `<details>` block holding it unchanged, whose summary reads **Scenarios (V1)** · 12 scenarios · replaced by V2,
  so the newest version is the only one open and the owner doesn't scroll past an old version's GIFs to reach it (PR
  #113). A post already collapsed is left alone: V1 keeps "replaced by V2" when V3 arrives. Only the workflow's own
  posts (`github-actions[bot]`, with its footer) are edited. An edit GitHub refuses (a body past 65,536 characters) is
  a warning in the run's log: the new post is up all the same. The `## Scenarios (V<N>)` heading stays inside the
  block, so the versions count on.
- **Failures:** a failing scenario still gets its GIF and its ❌ (the session fixes it before pushing, so it shouldn't
  happen), and never turns the run red. The run is red only when the workflow itself failed: a push that isn't a PR's
  GIFs, or GitHub refused an upload or a post.
- **One post per PR at a time** (the posting job's concurrency group is per PR, never cancelled midway), so two runs
  can't post the same version. GitHub keeps one waiting job per group: a post that is still waiting when a newer one
  arrives is skipped (its branch is deleted all the same), so push the GIFs once per round.
- **Cleanup:** when the PR is closed or merged, the workflow deletes any `e2e-gifs/pr-<n>-…` branch left behind, then runs
  main's `post-scenarios.py --closed` (with `--merged` for a merged PR), which cleans the releases and shrinks the posts:
  - **`prs-resources`:** it keeps every GIF and MP4 of the PR's newest version and
  deletes its older versions' (`pr-<n>-v1-…` to `pr-<n>-v<N-1>-…`), so the PR's last `## Scenarios (V<N>)` post keeps
  showing its GIFs and playing its MP4s for good.
  - **Each rule release** keeps the latest recording of every rule, so the extension and the player always have one per
    rule. Names keep the form `pr-<n>-v<k>-<run>`, so a PR's posts and versions work as above while it is open. A
    **merged** PR keeps, for each run it recorded there, its newest recording of it (per run, not per version: a rule its
    V2 didn't re-record keeps its V1); its older ones go, and so do other PRs' recordings of those runs, unless that PR
    is still open (its posts need them; whichever merges last wins; one cleanup runs at a time, so two PRs merged
    together can't each delete the other's). An older merged PR's post then shows a broken
    image for a rule a later PR re-recorded, and its player link says the recording was replaced. A PR **closed
    without merging** loses all its recordings there: they never became the rules' behaviour.
  - **The posts:** the PR's older posts would open onto broken images, so each is shrunk to one line: **Scenarios (V1)** · 12 scenarios · replaced by
  V2 · recordings deleted when the PR closed (its footer kept as a hidden comment, its version still counted). Their
  Gherkins go with them: across the PRs before this (#101–#116), versions added or replaced scenarios but only once
  reworded one, and the test files hold the current ones. When a PR closed without merging lost rule recordings, its
  newest post is shrunk too: **Scenarios (V2)** · 12 scenarios · recordings deleted when the PR closed without merging.
  - The newest version is the highest `v<N>` among the PR's assets, read as a number (`v10` is newer than `v2`, and `v1`
  never matches `v10`). Only names of the form `pr-<n>-v<N>-…` are ever deleted, and only in `prs-resources` and the
  `rules-<group>-resources` releases: the owner's own assets (not starting with `pr-`), other PRs' assets in
  `prs-resources` and any other name or release are never touched. A reopened PR carries on from its last version, so
  closing it again keeps only its newest version's GIFs and MP4s and shrinks the post it replaced. The job's log names
  each deleted asset (with its release), how many of the PR's it kept and how many assets each release holds.
  `tools/e2e/test_post_scenarios.py` tests what each release keeps.
- **The releases' limit:** GitHub sets no total size limit on a release, but a release holds at most 1,000 assets (a
  run takes two: its GIF and its MP4). This is why rule recordings have a release per rule file: a rule played on both
  screens takes four assets, so even a file of 100 rules leaves room for open PRs' recordings, and the cleanup keeps one
  recording per rule. The cleanup's log says how many each release holds, with a warning above 900. Near the limit:
  - `prs-resources`: the owner creates a new pre-release (e.g. `prs-resources-2`) and points `RELEASE` in
    `tools/e2e/post-scenarios.py` and the `release` setting's default in `src/Loopsmith.VsCodeExtension`
    and the default `release` in `src/Loopsmith.Web/wwwroot/e2e-player.html` at it. The old release stays, so the posts
    that embed its GIFs keep showing them; their player links look in the new release, so their MP4s no longer play.
    Nothing does this automatically yet.
  - a rule release: split its rule file (`rules/<…>/<area>.yaml` into two files, ADRs D40), so its scenarios move to two
    folders and two releases.
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
