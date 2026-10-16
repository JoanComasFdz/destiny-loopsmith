#!/bin/bash
# SessionStart hook for Claude Code cloud sessions: make the .NET 10 SDK available and restore packages,
# so `dotnet build` / `dotnet test` work, and install what tools/compendium/sheet_dump.py needs to download
# the Compendium snapshot, and ffmpeg when it is missing. Idempotent and quiet; does nothing outside cloud sessions.
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

# tools/compendium/sheet_dump.py (the Compendium download). Not fatal: the build doesn't need it.
if ! python3 -c 'import requests, bs4' >/dev/null 2>&1; then
  python3 -m pip install -q requests beautifulsoup4 >/dev/null 2>&1 || true
fi

# PyYAML (Ubuntu's python3-yaml) for tools/manifest/extract_manifest.py, which runs with python3 -I: that sees only the
# system's packages, not a pip install into the user's home. The image has it today, but nothing guarantees it stays.
# Not fatal: the build doesn't need it.
if ! python3 -I -c 'import yaml' >/dev/null 2>&1; then
  sudo=""
  if [ "$(id -u)" -ne 0 ]; then
    sudo="sudo"
  fi
  {
    $sudo apt-get update -qq >/dev/null
    DEBIAN_FRONTEND=noninteractive $sudo apt-get install -y -qq python3-yaml >/dev/null
  } 2>/dev/null || true
fi

# ffmpeg turns the web app's e2e recordings into GIFs; the image has it today, but nothing guarantees it stays.
# Not fatal: the build doesn't need it.
if ! command -v ffmpeg >/dev/null 2>&1; then
  sudo=""
  if [ "$(id -u)" -ne 0 ]; then
    sudo="sudo"
  fi
  {
    $sudo apt-get update -qq >/dev/null
    DEBIAN_FRONTEND=noninteractive $sudo apt-get install -y -qq ffmpeg >/dev/null
  } 2>/dev/null || true
fi
