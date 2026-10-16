#!/usr/bin/env bash
# The dev container's setup: runs once, inside the container, right after it is created (devcontainer.json,
# "postCreateCommand"). Each feature is one numbered script next to this one, run in order; a failing one stops the setup,
# so a broken container says so instead of half working.
# The e2e recordings extension (7-install-e2e-recordings-extension.sh) isn't run here: it needs VS Code attached, so
# devcontainer.json runs it each time VS Code attaches ("postAttachCommand").
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for step in \
  1-install-dotnet.sh \
  2-install-ffmpeg.sh \
  3-install-compendium-python.sh \
  4-install-playwright-chromium.sh \
  5-install-zsh-config.sh \
  6-install-tmux.sh; do
  echo "▶ postCreate: $step"
  bash "$SCRIPT_DIR/$step"
done

echo "▶ postCreate: complete."
