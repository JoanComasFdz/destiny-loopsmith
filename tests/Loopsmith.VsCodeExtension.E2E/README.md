# Loopsmith.VsCodeExtension.E2E — end-to-end tests of the VS Code extension

The e2e scenarios of the VS Code extension (`src/Loopsmith.VsCodeExtension`): each Gherkin about what it shows in VS
Code (its CodeLens, play button and recording panel) is one scenario here, played in VS Code for the web in Chromium, on the
desktop. Their code rules: [E2E-TEST-CODE-RULES.md](E2E-TEST-CODE-RULES.md). A PR that adds or changes them shows them
on the PR like the web app's: their Gherkins and their GIFs and MP4s, posted by the "E2E videos" workflow
([docs/hosting.md](../../docs/hosting.md), "E2E videos on pull requests").

## Run and record

`tools/e2e/record.sh` runs them with the web app's scenarios (its header says how to choose):

```bash
tools/e2e/record.sh --no-video Recordings             # this project's scenarios, unrecorded: the fast run while developing
tools/e2e/record.sh --no-video CodeLensTests          # one class
tools/e2e/record.sh --changed-since origin/main       # recorded: out/Recordings/<scenario>-desktop.gif + .mp4, right before a push
```

For this project it:
1. installs VS Code for the web's runner once (`npm ci` in `vscode-web/`: `@vscode/test-web`, pinned in
   `vscode-web/package-lock.json`; its `node_modules/` is never committed);
2. serves VS Code for the web, the build `vscode-web/package.json`'s `config.vscodeCommit` names (1.142.0; downloaded
   once into `${E2E_VSCODE_CACHE:-~/.cache/loopsmith/vscode-web}`), with the extension under development (the folder
   `src/Loopsmith.VsCodeExtension`, as it is: the extension has no build) and opened on `Workspace/`;
3. runs the scenarios with `E2E_VSCODE_URL` set to it (and `E2E_VIDEO_DIR` when recording), and makes each run's GIF and
   MP4 like the web app's, trimmed from when VS Code's workbench showed.

Without `E2E_VSCODE_URL` the scenarios are skipped, so `dotnet test` runs only their checks that need no browser
(`ScenarioShapeTests`, `AssetNamingTests`). The extension's own logic has unit tests too:
`node --test src/Loopsmith.VsCodeExtension/test/*.test.js`.

To update VS Code: set `config.vscodeCommit` and `config.vscodeVersion` in `vscode-web/package.json` to a stable build's
(`https://update.code.visualstudio.com/api/update/web-standalone/stable/latest` names the latest); to update the runner,
`npm install --prefix tests/Loopsmith.VsCodeExtension.E2E/vscode-web @vscode/test-web@<version>`.

## Where tests go

`Scenarios/<domain folder>/<Topic>Tests.cs`, by what the developer does with the extension: `Recordings/` (seeing a
scenario's recordings: `CodeLensTests`, `PlayHereTests`, `RecordingPanelTests`, `RefreshTests`). The domain folder is the
namespace after `.Scenarios.`, and the folder their GIFs go in under `out/`: a new one must not take a web domain
folder's name (`Starting`, `Loop`, …).

## The fake GitHub and the workspace

No scenario reaches GitHub. `Infrastructure/Fakes/FakeGitHub.cs` answers the extension's requests for a release
(`prs-resources`, which always exists, or a rule group's `rules-<group>-resources`, which exists once the Given names it)
with the recordings the scenario's Given put there, named as the workflow names them, and their
downloads with the cards in `Infrastructure/Fakes/Media/` ("Desktop" / "Mobile", "fake e2e recording": small GIFs, and
MP4s in VP9, which the image's Chromium plays; the real recordings are H.264, which only Chrome and VS Code's own
builds play). `Workspace/` holds the scenario files the scenarios open: copies of web scenarios cut short, one
whose asset names pass 200 characters, and a rule scenario (`Workspace/Rules/Hunter/Arc/Melees/`).

## Isolation and parallelism

Each scenario gets its own browser context (VS Code keeps its state in the context's IndexedDB) and its own fake GitHub,
so scenarios share nothing and run in any order. Two run at a time (`xunit.runner.json`): VS Code for the web is heavy.
