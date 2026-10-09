#!/usr/bin/env bash
# Downloads the Destiny Data Compendium (a view-only public Google Sheet) into a dated folder
# next to this script and zips it, ready to hand to a Loopsmith session: compendium-<date>/ and
# compendium-<date>.zip (both gitignored).
#
#   tools/compendium/get-compendium.sh            # tabs only (small)
#   tools/compendium/get-compendium.sh --images   # also the sheet's images
#
# Needs Docker (preferred) or Python 3.10+. Keep the result private: the Compendium is one
# person's donation-supported work — don't publish the raw files.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sheet='https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit'
name="compendium-$(date +%F)"
args=("$sheet" "$name")
[ "${1:-}" = "--images" ] || args+=("--no-images")

cd "$here"
use_docker=false
if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  if docker image inspect python:3.12-slim >/dev/null 2>&1 || docker pull -q python:3.12-slim >/dev/null 2>&1; then
    use_docker=true
  else
    echo "Docker can't download python:3.12-slim (network or proxy settings?) - using local Python instead."
  fi
fi
if [ "$use_docker" = true ]; then
  echo "Running sheet_dump.py in Docker (python:3.12-slim)..."
  docker run --rm -v "$here":/w -w /w -u "$(id -u):$(id -g)" -e HOME=/tmp -e PIP_DISABLE_PIP_VERSION_CHECK=1 python:3.12-slim \
    sh -c "pip install -q --user --no-warn-script-location requests beautifulsoup4 && python sheet_dump.py ${args[*]}"
else
  python3 -m pip install -q --user --disable-pip-version-check requests beautifulsoup4
  python3 sheet_dump.py "${args[@]}"
fi

[ -f "$name/tabs.json" ] || { echo "No tabs.json in $here/$name - the download did not complete." >&2; exit 1; }
if command -v zip >/dev/null 2>&1; then
  zip -qr "$name.zip" "$name"
else
  python3 -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', '.', sys.argv[1])" "$name"
fi
echo
echo "Done: $here/$name.zip"
echo "Send that zip to the Loopsmith session (it stays out of git)."
