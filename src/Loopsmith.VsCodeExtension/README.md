# Loopsmith e2e recordings (VS Code extension)

Shows each e2e scenario's recordings next to its code, in vscode.dev and in desktop VS Code. A scenario is a method with
a `[TestDescription("""…""")]` in a `Scenarios/**/*Tests.cs` file (`tests/Loopsmith.Web.E2E`,
`tests/Loopsmith.VsCodeExtension.E2E`). Its recordings are the GIFs and MP4s the "E2E videos" workflow keeps in the
`prs-resources` release ([docs/hosting.md](../../docs/hosting.md), "E2E videos on pull requests").

- **CodeLens** above the scenario's method, between its attributes and its declaration (next to other extensions' CodeLens,
  such as C# Dev Kit's "0 references"): `Play desktop recording | Play mobile recording | PR #109 · V1`, for each screen of
  its newest recording (the highest PR, then its highest version), or `No recording yet`.
- **Panel** beside the editor, opened by "Play … recording": the Gherkin's title, its class, domain folder, PR and
  version, a **Desktop** / **Mobile** tab per screen, the MP4 with the browser's controls (the GIF when the run has no
  MP4, or when this VS Code can't play it), **Recorded in** with every PR and version of the scenario, newest first, and
  **Open in e2e player ↗** (the site's `e2e-player.html`).
- **Hover** on the method's name: the title, the newest PR and version, **▶ Play desktop MP4**, **▶ Play mobile MP4**
  (they open the panel) and **Open in e2e player ↗**, then each screen's GIF side by side (the links come first: VS
  Code's hover is at most 250 px tall, and on a phone the GIFs would push them out of sight).
- **Play e2e recording of this scenario**: the ▷ button in the editor's title bar of a scenario file, and **Loopsmith: Play
  e2e recording of this scenario** in the Command Palette, open the panel on the scenario the cursor is in (the file's
  first one when the cursor is above them all), on its first screen. It's the way to play on a phone, where a CodeLens
  or a hover's link is hard to tap.
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

### Codespaces and Dev Containers: already installed

The repository's dev container (`.devcontainer/`) installs the extension from this folder each time VS Code attaches to
it (`7-install-e2e-recordings-extension.sh`: it packs the `.vsix` as the site's is packed and installs it with VS Code's own CLI), so
there is nothing to install: no Marketplace, no container of your own, no download. The extension is always the
checkout's own, so a branch's Codespace runs that branch's extension. The same container is a full dev environment for
the repository, so `dotnet build Loopsmith.slnx`, `dotnet test` and `tools/e2e/record.sh` run in it: .NET 10, Node 22,
ffmpeg, the Compendium download's Python packages, the Chromium the e2e scenarios drive, zsh with Oh My Zsh and the
Spaceship prompt, and tmux (one script per feature: [.devcontainer/README.md](../../.devcontainer/README.md)).

- **GitHub Codespaces:** on the repository's page, **Code** → **Codespaces** → **⋯** → **New with options…**, pick the
  branch, then **Create codespace** (a 4-core machine at least). Creating it takes a few minutes the first time.
- **Desktop VS Code** (with Docker on your computer and the Dev Containers extension): open a clone, then **Dev
  Containers: Reopen in Container**.

Once the container's setup has finished (its terminal says so), the Extensions view lists **Loopsmith e2e recordings**.
If a scenario file shows no CodeLens yet, run **Developer: Reload Window** once. In a Codespace, VS Code may already
have your GitHub sign-in, so the extension can read the release with it from the start. On a phone, put the cursor in a
scenario and tap the ▷ button in the editor's title bar.

### vscode.dev: from a container on your computer

vscode.dev fetches only from `localhost` and a few hosts of its own (its content security policy), so it can't install the
extension from the site, and plain vscode.dev has no "Install from VSIX". It installs it from `http://localhost:5000`,
served by a container built from this folder (`Dockerfile`, `serve.js`): Docker builds it straight from GitHub, with
nothing else to install or clone.

1. Build it and start it (once; `--restart unless-stopped` starts it again with Docker):
   ```
   docker build -t loopsmith-vscode-extension "https://github.com/JoanComasFdz/destiny-loopsmith.git#main:src/Loopsmith.VsCodeExtension"
   docker run -d --name loopsmith-vscode-extension --restart unless-stopped -p 127.0.0.1:5000:5000 loopsmith-vscode-extension
   ```
   To try a branch's version, put the branch's name in place of `main`.
2. In vscode.dev, run **Developer: Install Extension from Location…** from the Command Palette and paste
   `http://localhost:5000`. It works in Restricted Mode too.

What to know:
- **Keep the container running** whenever you use vscode.dev: vscode.dev keeps only the extension's `package.json` and
  loads `extension.js` from `http://localhost:5000` each time it starts it; with the container stopped the extension
  stays "Activating…" (checked on vscode.dev, 2026-10-16).
- **One browser at a time:** vscode.dev keeps it in that browser's storage, on that computer. Settings Sync doesn't
  carry it (on another computer it installs each synced extension from the Marketplace, and this one isn't there), and a phone can't reach a container on your computer, so each
  computer needs its container and its install, and a phone none.
- **To update:** build and start it again (`docker rm -f loopsmith-vscode-extension`, then step 1), and reload
  vscode.dev. **To remove:** uninstall it in the Extensions view, then `docker rm -f loopsmith-vscode-extension`.

### Desktop VS Code: the .vsix

The site has it with every deploy: `https://joancomasfdz.github.io/destiny-loopsmith/vscode-extension/loopsmith-e2e-recordings.vsix`
(a pull request's preview at `https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<n>/vscode-extension/loopsmith-e2e-recordings.vsix`).
Download it, then **Extensions: Install from VSIX…**, or `code --install-extension loopsmith-e2e-recordings.vsix`. Its
panel plays the recordings' H.264 MP4s (checked on VS Code 1.142.0 for Linux, 2026-10-16); a VS Code that can't shows
the run's GIF instead and says so.

## Settings

- `loopsmith.e2eRecordings.repository`: the repository whose release holds the recordings (`JoanComasFdz/destiny-loopsmith`).
- `loopsmith.e2eRecordings.release`: the release's tag (`prs-resources`).

## Develop

One file, `extension.js`: no build, no dependencies (VS Code loads it as it is, in a browser and on the desktop). Its
pure functions come first and are unit-tested: `node --test src/Loopsmith.VsCodeExtension/test/*.test.js`. How it
behaves in VS Code is checked by the e2e scenarios of `tests/Loopsmith.VsCodeExtension.E2E` (its README), played in
VS Code for the web. `vsix/` holds the `.vsix`'s manifest; keep its version in step with `package.json`.
