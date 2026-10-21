#!/usr/bin/env bash
# Downloads the Destiny Data Compendium (a view-only public Google Sheet) into a dated folder
# next to this script and zips it, ready to hand to a Loopsmith session: compendium-<date>/ and
# compendium-<date>.zip (both gitignored).
#
#   tools/compendium/get-compendium.sh            # tabs only (small)
#   tools/compendium/get-compendium.sh --images   # also the sheet's images
#
# Runs the C# download (Loopsmith.CompendiumDownload) from this clone: in Docker's .NET 10 SDK image
# (preferred), or with a local .NET 10 SDK. Keep the result private: the Compendium is one person's
# donation-supported work — don't publish the raw files.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
sheet='https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit'
image='mcr.microsoft.com/dotnet/sdk:10.0'
name="compendium-$(date +%F)"
args=("$sheet" "tools/compendium/$name" --zip)
[ "${1:-}" = "--images" ] || args+=("--no-images")

cd "$repo"
use_docker=false
if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  if docker image inspect "$image" >/dev/null 2>&1 || docker pull -q "$image" >/dev/null 2>&1; then
    use_docker=true
  else
    echo "Docker can't download $image (network or proxy settings?) - using the local .NET SDK instead."
  fi
fi
if [ "$use_docker" = true ]; then
  echo "Running the Compendium download in Docker ($image)..."
  # Builds into the container's /tmp, so the clone's bin/ and obj/ stay the host's.
  docker run --rm -v "$repo":/w -w /w -u "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp \
    -e NUGET_PACKAGES=/tmp/nuget -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 "$image" \
    dotnet run --project tools/compendium/Loopsmith.CompendiumDownload --artifacts-path /tmp/artifacts -- "${args[@]}"
elif command -v dotnet >/dev/null 2>&1; then
  dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- "${args[@]}"
else
  echo "Install Docker or the .NET 10 SDK first, then run this script again." >&2
  exit 1
fi

[ -f "$here/$name/tabs.json" ] || { echo "No tabs.json in $here/$name - the download did not complete." >&2; exit 1; }
echo
echo "Done: $here/$name.zip"
echo "Send that zip to the Loopsmith session (it stays out of git)."
