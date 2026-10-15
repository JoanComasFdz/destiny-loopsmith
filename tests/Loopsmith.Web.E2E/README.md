# Loopsmith.Web.E2E — end-to-end tests of the web app

Scenarios that drive the Blazor loop designer in a real browser (Playwright, on the Chromium the cloud image ships), one
GIF per scenario, so a PR can show its scenarios working. **How to write one:
[E2E-TEST-CODE-RULES.md](E2E-TEST-CODE-RULES.md)** (binding for this project; CONVENTIONS.md's coding rules don't apply
here).

## Run and record

```bash
dotnet test                                         # without E2E_URL every scenario is skipped
tools/e2e/record.sh Starting                        # a domain folder (or several, or `all`)
tools/e2e/record.sh StartFromScratchTests           # a class
tools/e2e/record.sh --changed-since origin/main     # the scenarios this branch adds or changes
tools/e2e/push-gifs.sh                              # their GIFs, posted on the branch's PR (after the push)
```

`record.sh` publishes the web app (Release; `--no-publish` reuses the last one), prepares it for `/destiny-loopsmith/`
with `tools/web/prepare-pages.sh`, serves it with `python3 -m http.server`, runs the chosen scenarios with `E2E_URL` set
and video on, and writes to `out/` (`--out <dir>`; gitignored):
- `out/<domain folder>/<gif name>.gif`: each scenario from Home on (the manifest's startup screen trimmed off), 8 fps,
  800 px wide; a failing scenario gets its GIF too;
- `out/scenarios.json`: one entry per scenario: domain folder, class, method, title, GIF path, startup time (page open
  → Home shown) and whether it passed.

It prints each scenario's startup time and every request that left for bungie.net, and exits non-zero when a scenario
failed. `--changed-since <ref>` picks the scenario classes added or changed since the branch left `<ref>` (working tree
included), plus those that name a changed fixture file.

CI doesn't run them. On a pull request, the session records them before each push that adds or changes scenarios
(`--changed-since origin/main`) and, right after the push, `tools/e2e/push-gifs.sh` pushes the GIFs to a throwaway
`e2e-gifs/pr-<n>-<short sha>` branch, which `.github/workflows/e2e-videos.yml` posts on the PR
([docs/hosting.md](../../docs/hosting.md#e2e-videos-on-pull-requests)): a failing scenario is marked there, never red.

Against a site you serve yourself: `E2E_URL=http://127.0.0.1:8000/destiny-loopsmith/ dotnet test tests/Loopsmith.Web.E2E`
(add `E2E_VIDEO_DIR=<dir>` to keep the videos).

## Where tests go

`Infrastructure/` holds everything that isn't a scenario: `BaseE2E`, the `Frontend` page objects, the `Vocabulary`
unions, the Bungie routing, the assertions. `Scenarios/` is organized by domain, never by PR or task: folder = domain
area, class = a topic in it (`<Topic>Tests.cs`), method = one scenario.

```
Scenarios/Starting/          start from scratch, from a DIM link
Scenarios/Build/Subclass/    picking aspects, fragments, abilities; what the build check says
Scenarios/Build/Artifact/    the same for the artifact
Scenarios/Build/Weapons/     the same for weapons and their perks
Scenarios/Build/Armor/       the same for armor, sets, mods
Scenarios/ImportExport/      build.yaml and loop import, loop export, share links
Scenarios/Loop/Arc/          each Arc interaction with a minimal build (then Solar/, Void/, Strand/, Stasis/, Prismatic/)
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
