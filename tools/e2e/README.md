# E2E tools

The scripts that record the e2e scenarios and post them on a PR ([docs/hosting.md](../../docs/hosting.md), "E2E videos
on pull requests").

- `record.sh`: plays and records the scenarios a PR added or changed (`--no-video` plays them unrecorded) and writes
  `out/<domain>/<scenario>-<screen>.gif` and `.mp4` and `out/scenarios.json`.
- `push-gifs.sh`: pushes those recordings to a throwaway `e2e-gifs/pr-<n>-<sha>` branch, which
  `.github/workflows/e2e-videos.yml` posts on the PR with the e2e tool.
- **The e2e tool** (.NET): posts a PR's recorded scenarios, and cleans the releases when the PR closes.

  ```bash
  dotnet run --project tools/e2e/Loopsmith.E2ETools -- post --pr <n> --commit <sha> [--out <dir>] [--repo <owner/repo>] [--dry-run]
  dotnet run --project tools/e2e/Loopsmith.E2ETools -- post --pr <n> --closed [--merged] [--repo <owner/repo>] [--dry-run]
  ```

  It uses the `gh` CLI (`GH_TOKEN`) on `--repo` or `GITHUB_REPOSITORY`; `--dry-run` prints what it would upload, post,
  edit, hide or delete instead of doing it (it still reads from GitHub). Three projects, like the rule viewer's:
  - `Loopsmith.E2ETools`: the host. It reads `scenarios.json` and the recordings, and calls `gh`
    (`Hosting/`): posting (`ScenarioPosting`) and closing (`PrClosing`).
  - `Loopsmith.E2ETools.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md): the post
    (`PostWriting`), its versions and what becomes of the earlier ones (`PostVersioning`: collapsed, hidden, shrunk), the
    releases and the names of their assets (`ReleaseNaming`, mirrored by the VS Code extension), what each release keeps
    when a PR closes (`ReleaseCleaning`), what to upload (`UploadPlanning`), "What changed" (`CommitDescribing`) and the
    JSON it reads and sends (`JsonReading`), and the command line (`OptionsReading`).
  - `Loopsmith.E2ETools.Tests`: their tests, which `dotnet test` runs with the rest, in CI too.

  Neither project references `Loopsmith.Core`, only `Loopsmith.Functional` (an architecture test holds it): the
  workflow builds the tool on every post, and Core would add about half a minute to each.
