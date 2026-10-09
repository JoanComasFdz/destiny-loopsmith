#!/bin/bash
# SessionStart hook for Claude Code cloud sessions: make the .NET 10 SDK available and restore packages,
# so `dotnet build` / `dotnet test` work. Idempotent and quiet; does nothing outside cloud sessions.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

if ! command -v dotnet >/dev/null 2>&1; then
  # Ubuntu 24.04 (noble) ships dotnet-sdk-10.0 in its own archive; Microsoft's download hosts are blocked.
  sudo=""
  if [ "$(id -u)" -ne 0 ]; then
    sudo="sudo"
  fi
  $sudo apt-get update -qq >/dev/null
  DEBIAN_FRONTEND=noninteractive $sudo apt-get install -y -qq dotnet-sdk-10.0 >/dev/null
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ] && ! grep -qs 'DOTNET_NOLOGO' "$CLAUDE_ENV_FILE"; then
  {
    echo 'export DOTNET_NOLOGO=1'
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1'
  } >> "$CLAUDE_ENV_FILE"
fi

dotnet restore "$CLAUDE_PROJECT_DIR/Loopsmith.slnx" --verbosity quiet
