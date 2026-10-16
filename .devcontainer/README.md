# Dev container

A GitHub Codespace, or desktop VS Code's **Dev Containers: Reopen in Container**, on this repository: a box with
everything it builds, tests and records with, set up the same way every time. How to open one and use the e2e
recordings extension in it: [src/Loopsmith.VsCodeExtension/README.md](../src/Loopsmith.VsCodeExtension/README.md),
"Codespaces and Dev Containers".

## What's inside

- **.NET 10**, restored: `dotnet build Loopsmith.slnx`, `dotnet test`.
- **Node 22** (the dev container's `node` feature).
- **ffmpeg** and **Playwright's Chromium**: `tools/e2e/record.sh` records the e2e scenarios.
- **The Compendium download's Python packages**: `tools/compendium/sheet_dump.py`.
- **Zsh**, as in [my-claude-code-setup](https://github.com/JoanComasFdz/my-claude-code-setup/tree/main/.devcontainer):
  Oh My Zsh + the Spaceship prompt, with `git`, `zsh-autosuggestions`, `zsh-syntax-highlighting`, `sudo`, `zoxide` (`z`
  to jump around), `extract`, `history`, `docker` and `npm`; `eza` (the `l` alias). It is the terminal's shell.
- **tmux**: prefix Ctrl+A, the mouse on, `|` and `-` split; `t`, `tl` (list), `tk <name>` (kill), `tn <name>` (new),
  `ta <name>` (attach).
- **The Loopsmith e2e recordings extension**, installed from `src/Loopsmith.VsCodeExtension` each time VS Code attaches.
- **C# Dev Kit** and **Container Tools**, from the Marketplace.
- **No chat pop-up**: VS Code's secondary side bar, where it opens its chat, starts hidden.

## One-time setup on your computer: a terminal font

The Spaceship prompt and `eza`'s icons use **Nerd Font** glyphs. VS Code draws the terminal on your computer (or in your
browser, for a Codespace), not in the container, so the font must be installed there; the container can't do it. Install
any Nerd Font and make sure its name is in `terminal.integrated.fontFamily` in `devcontainer.json`
(<https://www.nerdfonts.com/font-downloads>; on Windows, `scoop bucket add nerd-fonts && scoop install FiraCode-NF`).
Without one, the prompt still works, with boxes in place of its icons. A phone's browser shows the boxes.

## Files

- `devcontainer.json`: the image (`mcr.microsoft.com/devcontainers/base:noble`), Node, the machine size, the VS Code
  extensions and settings, and the two commands below.
- `postCreateCommand.sh`: runs once, when the container is created; runs each feature's script, in order, and stops at
  the first that fails.
- `1-install-dotnet.sh`: `dotnet-sdk-10.0` from Ubuntu's archive, then `dotnet restore`.
- `2-install-ffmpeg.sh`: ffmpeg.
- `3-install-compendium-python.sh`: `requests` and `beautifulsoup4` (Ubuntu's packages).
- `4-install-playwright-chromium.sh`: the Chromium the pinned Playwright (`Directory.Packages.props`) expects, with its
  system libraries.
- `5-install-zsh-config.sh`: zsh, Oh My Zsh, Spaceship, the plugins, zoxide, eza and `~/.zshrc`.
- `6-install-tmux.sh`: tmux, `~/.tmux.conf` and the aliases in `~/.zshrc` (after step 5, which writes it).
- `7-install-e2e-recordings-extension.sh`: runs each time VS Code attaches (`postAttachCommand`): packs the extension's
  `.vsix` as `Loopsmith.Web.csproj` packs the site's and installs it in the VS Code attached.
