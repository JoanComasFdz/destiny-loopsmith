#!/usr/bin/env bash
# The Python packages of the Compendium download (tools/compendium/sheet_dump.py): requests and beautifulsoup4, from
# Ubuntu's archive (its Python refuses pip installs outside a virtual environment).
set -euo pipefail

if ! python3 -c 'import requests, bs4' >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends python3 python3-requests python3-bs4
fi
