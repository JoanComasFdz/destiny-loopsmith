#!/usr/bin/env bash
# .NET 10: dotnet-sdk-10.0 from Ubuntu's own archive, as the cloud sessions install it (.claude/hooks/session-start.sh;
# global.json's rollForward takes its feature band), then the solution's packages.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends dotnet-sdk-10.0
fi
dotnet restore "$REPO/Loopsmith.slnx"
