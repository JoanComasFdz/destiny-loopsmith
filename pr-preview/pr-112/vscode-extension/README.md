# Loopsmith e2e recordings (VS Code extension)

Shows each e2e scenario's recordings next to its code, in vscode.dev and in desktop VS Code. A scenario is a method with
a `[TestDescription("""…""")]` in a `Scenarios/**/*Tests.cs` file (`tests/Loopsmith.Web.E2E`,
`tests/Loopsmith.VsCodeExtension.E2E`). Its recordings are the GIFs and MP4s the "E2E videos" workflow keeps in the
`prs-resources` release ([docs/hosting.md](../../docs/hosting.md), "E2E videos on pull requests").

- **CodeLens** above the scenario: `Play desktop recording | Play mobile recording | PR #109 · V1`, for each screen of
  its newest recording (the highest PR, then its highest version), or `No recording yet`.
- **Panel** beside the editor, opened by "Play … recording": the Gherkin's title, its class, domain folder, PR and
  version, a **Desktop** / **Mobile** tab per screen, the MP4 with the browser's controls (the GIF when the run has no
  MP4, or when this VS Code can't play it), **Recorded in** with every PR and version of the scenario, newest first, and
  **Open in e2e player ↗** (the site's `e2e-player.html`).
- **Hover** on the method's name: the title, the newest PR and version, each screen's GIF side by side, then
  **▶ Play desktop MP4**, **▶ Play mobile MP4** (they open the panel) and **Open in e2e player ↗**.
- **Loopsmith: Refresh e2e recordings** (Command Palette) reads the release again: a recording posted after the files
  were opened shows up. The read is also renewed when it is ten minutes old.

It finds a scenario's assets by their names: `pr-<n>-v<version>-<GIF name>.gif` / `.mp4`, the GIF name being the method's
name kebab-cased (Humanizer's rules) and its screen, shortened with a hash past 200 characters
(`tools/e2e/post-scenarios.py`, `name_asset`). It reads the release from GitHub's API: 60 requests an hour without a
sign-in, 5,000 with one. It asks VS Code for a GitHub sign-in silently, never with a prompt: VS Code hands an extension a
sign-in only once you have let it use one, so until then it reads the release without one. To let it, run **Loopsmith:
Sign in to GitHub to read e2e recordings** once (or click **Sign in to GitHub** on the CodeLens, which shows when the
hourly limit is used up) and pick your account (on vscode.dev, the one you opened the repository with); from then on it
uses it without asking.

## Install

The site serves the extension with every deploy: the live site at
`https://joancomasfdz.github.io/destiny-loopsmith/vscode-extension/`, a pull request's preview at
`https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<n>/vscode-extension/`.

- **vscode.dev:** open the repository (`https://vscode.dev/github/JoanComasFdz/destiny-loopsmith`), run **Developer:
  Install Extension from Location…** from the Command Palette and paste the folder's link above. It works in Restricted
  Mode too. vscode.dev keeps it until you uninstall it (Extensions view); to update, install from the link again.
- **Desktop VS Code:** download `loopsmith-e2e-recordings.vsix` from that folder, then **Extensions: Install from
  VSIX…**, or `code --install-extension loopsmith-e2e-recordings.vsix`. Its panel plays the recordings' H.264 MP4s
  (checked on VS Code 1.142.0 for Linux, 2026-10-16); a VS Code that can't shows the run's GIF instead and says so.

## Settings

- `loopsmith.e2eRecordings.repository`: the repository whose release holds the recordings (`JoanComasFdz/destiny-loopsmith`).
- `loopsmith.e2eRecordings.release`: the release's tag (`prs-resources`).

## Develop

One file, `extension.js`: no build, no dependencies (VS Code loads it as it is, in a browser and on the desktop). Its
pure functions come first and are unit-tested: `node --test src/Loopsmith.VsCodeExtension/test/*.test.js`. How it
behaves in VS Code is checked by the e2e scenarios of `tests/Loopsmith.VsCodeExtension.E2E` (its README), played in
VS Code for the web. `vsix/` holds the `.vsix`'s manifest; keep its version in step with `package.json`.
