# E2E tools

The scripts that record the e2e scenarios and post them on a PR ([docs/hosting.md](../../docs/hosting.md), "E2E videos
on pull requests").

- `record.sh`: plays and records the scenarios a PR added or changed (`--no-video` plays them unrecorded) and writes
  `out/<domain>/<scenario>-<screen>.gif` and `.mp4` and `out/scenarios.json`.
- `push-gifs.sh`: pushes those recordings to a throwaway `e2e-gifs/pr-<n>-<sha>` branch, which
  `.github/workflows/e2e-videos.yml` posts on the PR with the e2e tool.
- **The e2e tool** (.NET): posts a PR's recorded scenarios, and cleans the releases when the PR closes. It also does the
  steps the scripts above (and the dev container's) hand it, so no script runs Python:

  ```bash
  dotnet run --project tools/e2e/Loopsmith.E2ETools -- post --pr <n> --commit <sha> [--out <dir>] [--repo <owner/repo>] [--dry-run]
  dotnet run --project tools/e2e/Loopsmith.E2ETools -- post --pr <n> --closed [--merged] [--repo <owner/repo>] [--dry-run]
  ```

  `post` uses the `gh` CLI (`GH_TOKEN`) on `--repo` or `GITHUB_REPOSITORY`; `--dry-run` prints what it would upload, post,
  edit, hide or delete instead of doing it (it still reads from GitHub). The steps (`record.sh` builds the tool once and
  runs it with `dotnet <its dll>`; the others with `dotnet run`):

  | Command | Called by | What it does |
  |---|---|---|
  | `free-port` | `record.sh` | prints a port free on 127.0.0.1, for VS Code for the web |
  | `vscode-commit <package.json>` | `record.sh` | prints the VS Code build the runner's `package.json` pins (`config.vscodeCommit`) |
  | `report-runs <.trx>...` | `record.sh --no-video` | prints each unrecorded run's result and time from the test results; fails when a file is missing or no run passed or failed ("No scenario ran.") |
  | `find-home <record.json>` | `record.sh` | prints a recorded run's domain folder, GIF name, screen and the second its video shows Home (the first frame that ffmpeg finds matches the screenshot the test took of Home, `<name>.home.png`; the time the test noted when there is none), where its GIF and MP4 start |
  | `list-flows <record.json>...` | `record.sh` | prints each rule the runs' Gherkins name (`# Rule <name>:`), `<domain folder> <rule>`, once per folder |
  | `write-scenarios <out> <selection.tsv> <record.json>...` | `record.sh` | writes `<out>/scenarios.json` (what `post` reads) and prints its summary |
  | `list-files <scenarios.json>` | `push-gifs.sh` | prints the GIFs, MP4s and flow PNGs it lists, each once |
  | `pack-vsix <extension folder> <LICENSE> <.vsix>` | `.devcontainer/7-install-e2e-recordings-extension.sh` | packs the VS Code extension as `Loopsmith.Web.csproj` packs the site's |

- **The static server** (.NET, Kestrel): `record.sh` serves the published web app with it, as GitHub Pages would (a
  folder's `index.html`, a folder asked without its slash redirected to it, any other file as it is, 404 for what isn't
  there). It prints the address it listens on, at a port the system picks unless `--port` is given:

  ```bash
  dotnet run --project tools/e2e/Loopsmith.StaticServer -- <folder> [--port <n>]
  ```

- **The projects**, like the rule viewer's:
  - `Loopsmith.E2ETools`: the e2e tool's host. It reads `scenarios.json`, the recordings and the files the scripts name,
    and calls `gh` and `ffmpeg` (`Hosting/`): posting (`ScenarioPosting`), closing (`PrClosing`) and the scripts' steps
    (`RecordingSteps`).
  - `Loopsmith.StaticServer`: the static server, a host only (`Hosting/StaticSiteServing`).
  - `Loopsmith.E2ETools.Core`: their logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md): the post
    (`PostWriting`), its versions and what becomes of the earlier ones (`PostVersioning`: collapsed, hidden, shrunk), the
    releases and the names of their assets (`ReleaseNaming`, mirrored by the VS Code extension), what each release keeps
    when a PR closes (`ReleaseCleaning`), what to upload (`UploadPlanning`), "What changed" (`CommitDescribing`), the
    unrecorded runs' table (`RunReporting`), Home in a video (`HomeFinding`), `scenarios.json` and the rules to draw
    (`ScenarioListing`), the .vsix's layout (`VsixPacking`), the JSON it reads and sends (`JsonReading`), and the command
    lines (`CommandReading`, `OptionsReading`, `ServerOptionsReading`).
  - `Loopsmith.E2ETools.Tests`: their tests, which `dotnet test` runs with the rest, in CI too.

  None of them references `Loopsmith.Core`, only `Loopsmith.Functional` (an architecture test holds it): the
  workflow builds the tool on every post, and Core would add about half a minute to each.
