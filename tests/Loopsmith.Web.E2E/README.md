# Loopsmith.Web.E2E — end-to-end tests of the web app

Scenarios that drive the Blazor loop designer in a real browser (Playwright, on the Chromium the cloud image ships),
each one the Gherkin of an acceptance criterion ([docs/given-when-then-test-rules.md](../../docs/given-when-then-test-rules.md)),
played on a desktop window and/or a phone, one GIF per screen, so a PR can show its scenarios working. **How to write one:
[E2E-TEST-CODE-RULES.md](E2E-TEST-CODE-RULES.md)** (binding for this project; the coding rules, docs/CODING_RULES.md, don't apply
here).

## Run and record

While developing, run them **unrecorded** (fast; as often as you like):

```bash
tools/e2e/record.sh --no-video StartFromScratchTests            # any selection below, no video, no pacing, no GIF
tools/e2e/record.sh --no-video --changed-since origin/main      # the scenarios this branch adds or changes
tools/e2e/record.sh --no-video --no-publish ReloadTests         # reuse the last publish: only scenario code changed
```

`--no-video` runs the same selection against the same locally served publish, without `E2E_VIDEO_DIR`: no step
waits for the next, no pointer glides, nothing is held on screen, and nothing is encoded. It prints each run's result
and time and the requests that left for bungie.net, exits non-zero when a run failed, and leaves the GIFs and
`scenarios.json` of the last recording in `out/` as they are. `--no-publish` skips the publish (about a minute): use
it when only scenario code changed; a change to the web app needs a run without it. Measured on `StartFromScratchTests`
(two runs, desktop and mobile, publish skipped, cache warm): about 14 s a run and 35 s in all unrecorded, 24–27 s a run
and about a minute in all recorded.

Record them only **right before a push** that adds or changes scenarios (CLAUDE.md, "Agent workflow"), so the GIFs
posted on the PR match the pushed code:

```bash
dotnet test                                         # without E2E_URL every scenario is skipped
tools/e2e/record.sh --changed-since origin/main     # right before the push: the scenarios this branch adds or changes
tools/e2e/record.sh Starting                        # a domain folder (or several, or `all`)
tools/e2e/record.sh StartFromScratchTests           # a class
tools/e2e/record.sh Starting --action-gap 2         # at least 2 s between two steps (default 1)
tools/e2e/push-gifs.sh                              # their GIFs, posted on the branch's PR (after the push)
```

`record.sh` publishes the web app (Release; `--no-publish` reuses the last one), prepares it for `/destiny-loopsmith/`
with `tools/web/prepare-pages.sh`, serves it with `python3 -m http.server`, runs the chosen scenarios with `E2E_URL` set
and video on, and writes to `out/` (`--out <dir>`; gitignored):
- `out/<domain folder>/<gif name>.gif`: each run of a scenario from Home on (the manifest's startup screen trimmed
  off), its last frame held 3 s, 20 fps; a desktop run 800 px wide, a phone run at its 390 CSS pixels; the GIF name ends in
  `-desktop` or `-mobile`; a failing run gets its GIF too;
- `out/<domain folder>/<gif name>.mp4`: the same run as an H.264 MP4 at the recording's own size and frame rate
  (1280×800; a phone's 780×1688, its pixels), its last frame held 3 s too: the PR comment links it to the site's player
  page;
- `out/scenarios.json`: one entry per run: domain folder, class, method, title, Gherkin, screen (`desktop` /
  `mobile`), GIF path, MP4 path, startup time (page open → Home shown) and whether it passed.

It prints each scenario's startup time and every request that left for bungie.net, and exits non-zero when a scenario
failed. `--changed-since <ref>` picks the scenario classes added or changed since the branch left `<ref>` (working tree
included), plus those that name a changed fixture file.

What the GIF and the MP4 show is the browser window, nothing more and nothing less: the video is the window at its pixels
(1280×800 on desktop; a 390×844 phone drawn at twice that, 780×1688; `BaseE2E`, `BrowserRun`, E2E-TEST-CODE-RULES.md rule b), and the window shows its scrollbar (Playwright hides it in headless Chromium; `BrowserRun` turns
that off), so a page taller than the window says so. Before each action and each read, the page object scrolls the
whole box it works on into view (`Infrastructure/Frontend/OnScreen.cs`; E2E-TEST-CODE-RULES.md, rule c). While
recording, it scrolls smoothly, holds a box that moved a moment on screen, glides the mouse pointer (headless Chromium
draws none, so `BaseE2E` draws a big one, and a ring at each click) to the element an action uses (on a phone, which
has no pointer, it shows a pink dot where the finger lands for 0.6 s instead, then the ring at the tap: the app
re-renders before the ring is drawn, so without the dot the video would show the new screen before the tap), and starts a step no sooner than
`--action-gap` seconds (`E2E_ACTION_GAP`, default 1) after the previous one ended, so the GIF doesn't scroll and click
faster than the eye can follow. Without a recording it doesn't wait.

CI doesn't run them. On a pull request, the session records them once, right before each push that adds or changes
scenarios (`--changed-since origin/main`; again if a scenario or the web app changes before the push, so the GIFs match
the pushed head; never between pushes) and, right after the push, `tools/e2e/push-gifs.sh` pushes the GIFs to a throwaway
`e2e-gifs/pr-<n>-<short sha>` branch, which `.github/workflows/e2e-videos.yml` posts on the PR
([docs/hosting.md](../../docs/hosting.md#e2e-videos-on-pull-requests)): each scenario once, its title and Gherkin, then
its 🖥️ Desktop and/or 📱 Mobile GIF with a ▶ Play MP4 link under it; a failing run is marked there, never red.

Against a site you serve yourself: `E2E_URL=http://127.0.0.1:8000/destiny-loopsmith/ dotnet test tests/Loopsmith.Web.E2E`
(add `E2E_VIDEO_DIR=<dir>` to keep the videos, `E2E_BUNGIE_LOG=<file>` to log the requests that left for bungie.net).

## Where tests go

`Infrastructure/` holds everything that isn't a scenario: `BaseE2E`, the `Frontend` page objects, the `Vocabulary`
unions, the Bungie routing, the assertions. `Scenarios/` is organized by domain, never by PR or task: folder = domain
area, class = a topic in it (`<Topic>Tests.cs`), method = one scenario (one Gherkin), played once per screen attribute.

```
Scenarios/Starting/          start from scratch, from a DIM link
Scenarios/Build/Subclass/    picking aspects, fragments, abilities; what the build check says
Scenarios/Build/Artifact/    the same for the artifact
Scenarios/Build/Weapons/     the same for weapons and their perks
Scenarios/Build/Armor/       the same for armor, sets, mods
Scenarios/ImportExport/      build.yaml and loop import, loop export, share links
Scenarios/Loop/Arc/          each Arc interaction with a minimal build (then Solar/, Void/, Strand/, Stasis/, Prismatic/)
Scenarios/Loop/Weapons/      a weapon's steps: reload, the "×" enemy count, its row in the trigger panel
```

A folder exists once it has a scenario. A scenario's fixture files (a minimal `.build.yaml` / `.loop.yaml`) sit next to
its class and are copied to the test output; never under `builds/` (the web app would list them on Home).

## Isolation and parallelism

The site is static and shared by all scenarios. Isolation comes from a fresh browser context per scenario: nothing one
saves (IndexedDB, local storage) reaches another, and BaseE2E checks each context starts empty. xunit runs the classes
in parallel; `xunit.runner.json` sets `maxParallelThreads` to `default`, one per CPU, because each scenario's tab
downloads, holds and indexes the whole manifest (about 210 MB) behind the startup screen: more tabs than CPUs only
slows them all. There is no warm-up page: every scenario's video starts on the startup screen, and `record.sh` trims it
off from when Home shows.

## The manifest cache

No scenario reaches bungie.net except to fill a local cache once. BaseE2E routes every bungie.net request of a context:
- the manifest's index gets an error, so the app uses its pinned `ManifestContentId` (`src/Loopsmith.Web/wwwroot/appsettings.json`)
  and every run reads the same manifest version;
- Bungie's static files (`/common/destiny2_content/…`: the manifest's components and the icons the app shows) come from
  `${E2E_MANIFEST_CACHE:-~/.cache/loopsmith/bungie-manifest}`, keyed by their URL (which holds the version), served by
  a small local HTTP server in the test process (handing a 200 MB file through Playwright's channel stalls the page).
  A file missing from the cache is fetched from bungie.net once, saved and served; parallel scenarios wait for that one
  download;
- any other bungie.net request is refused.

What left for bungie.net, or was refused, is logged (`record.sh` prints it). Bumping `ManifestContentId` makes the next
run download the new version once. The cache is Bungie's data: never copy it into the repository.

## The browser

Playwright is pinned in `Directory.Packages.props` to the release that matches the cloud image's Chromium
(`/opt/pw-browsers/chromium-1194` → Microsoft.Playwright 1.56.0); sessions never run `playwright install`. If the
image's Chromium changes, bump the pin to match. Videos need `ffmpeg` (the session-start hook installs it).
