#!/usr/bin/env bash
# The Loopsmith e2e recordings extension. Runs each time VS Code attaches to the dev container (devcontainer.json,
# "postAttachCommand"; not from postCreateCommand.sh: it needs VS Code attached): packs it from
# src/Loopsmith.VsCodeExtension into a .vsix, laid out as Loopsmith.Web.csproj's
# PublishVsCodeExtension target lays out the site's (extension/ with package.json, extension.js, README.md and the
# repository's LICENSE as LICENSE.txt; extension.vsixmanifest; [Content_Types].xml), and installs it in the VS Code
# attached, replacing the one there: no Marketplace, no download.
#   .devcontainer/7-install-e2e-recordings-extension.sh [--pack-only <vsix>]
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
source="$repo/src/Loopsmith.VsCodeExtension"
vsix="${TMPDIR:-/tmp}/loopsmith-e2e-recordings.vsix"
pack_only=0
if [ "${1:-}" = "--pack-only" ]; then
  pack_only=1
  vsix="$2"
fi

# Packed by the e2e tool's pack-vsix (tools/e2e/README.md); the first attach builds it.
dotnet run --project "$repo/tools/e2e/Loopsmith.E2ETools" -c Release -v q -- pack-vsix "$source" "$repo/LICENSE" "$vsix"
if [ $pack_only -eq 1 ]; then
  exit 0
fi

# The VS Code attached: its `code` command when this runs where VS Code's terminals do (it tells the running VS Code,
# so the extension starts without a reload), else its server's own CLI, the newest one (where Codespaces and Dev
# Containers put it).
if [ -n "${VSCODE_IPC_HOOK_CLI:-}" ] && command -v code >/dev/null 2>&1 && code --install-extension "$vsix" --force; then
  exit 0
fi
server="$(ls -t "$HOME"/.vscode-remote/bin/*/bin/code-server "$HOME"/.vscode-server/bin/*/bin/code-server \
                "$HOME"/.vscode-server/cli/servers/*/server/bin/code-server 2>/dev/null | head -n1 || true)"
if [ -z "$server" ]; then
  echo "No VS Code attached: the extension is packed in $vsix but not installed." >&2
  exit 0
fi
"$server" --install-extension "$vsix" --force
