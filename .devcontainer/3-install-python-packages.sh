#!/usr/bin/env bash
# The repository's Python packages, from Ubuntu's archive (its Python refuses pip installs outside a virtual environment):
# requests and beautifulsoup4 for the Compendium download (tools/compendium/sheet_dump.py), and PyYAML for the manifest
# extractor (tools/manifest/extract_manifest.py, run with python3 -I, which sees only the system's packages).
set -euo pipefail

if ! python3 -I -c 'import requests, bs4, yaml' >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends python3 python3-requests python3-bs4 python3-yaml
fi
