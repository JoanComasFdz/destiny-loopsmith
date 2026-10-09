#!/usr/bin/env bash
# Downloads EVERYTHING from the Destiny Data Compendium (all tabs + images + offline viewer) with Docker,
# into tools/compendium/compendium-<date>/ (gitignored — keep it private, never commit it).
# If Docker can't download images (e.g. "no such host" behind a corporate proxy), set the proxy in
# Docker Desktop → Settings → Resources → Proxies, or use get-compendium.sh, which falls back to Python.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

docker run --rm -v "$PWD":/w -w /w -u "$(id -u):$(id -g)" -e HOME=/tmp -e PIP_DISABLE_PIP_VERSION_CHECK=1 python:3.12-slim \
  sh -c 'pip install -q --user --no-warn-script-location requests beautifulsoup4 && python sheet_dump.py "https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit" "compendium-$(date +%F)"'
