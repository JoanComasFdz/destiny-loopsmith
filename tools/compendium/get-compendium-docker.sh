#!/usr/bin/env bash
# Downloads EVERYTHING from the Destiny Data Compendium (all tabs + images + offline viewer) with Docker,
# into tools/compendium/compendium-<date>/ (gitignored — keep it private, never commit it): the C#
# download (Loopsmith.CompendiumDownload) run from this clone in Docker's .NET 10 SDK image.
# Not zipped: zip the folder yourself to hand it to a session. If Docker can't pull its image
# ("lookup registry-1.docker.io: no such host"), see "Troubleshooting" in README.md.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

docker run --rm -v "$PWD":/w -w /w -u "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/tmp/nuget -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run --project tools/compendium/Loopsmith.CompendiumDownload --artifacts-path /tmp/artifacts -- \
  "https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit" "tools/compendium/compendium-$(date +%F)"
