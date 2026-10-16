#!/usr/bin/env bash
# Runs once, when the dev container is created (devcontainer.json, postCreateCommand): restores the solution and
# installs the Chromium the e2e scenarios drive (with its system libraries), the build that the Playwright pinned in
# Directory.Packages.props expects. The cloud sessions' image ships that Chromium in /opt/pw-browsers; here it goes
# to Playwright's default folder (~/.cache/ms-playwright), where the .NET Playwright finds it too.
# Then tmux's configuration and aliases (setup-tmux.sh).
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"

dotnet restore "$repo/Loopsmith.slnx"

playwright="$(sed -n 's/.*"Microsoft.Playwright" Version="\([^"]*\)".*/\1/p' "$repo/Directory.Packages.props")"
npx --yes "playwright@$playwright" install --with-deps chromium ffmpeg

bash "$repo/.devcontainer/setup-tmux.sh"
