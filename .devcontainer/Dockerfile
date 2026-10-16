# The dev container's image (devcontainer.json): Ubuntu 24.04 with what the repository builds and records with.
# - dotnet-sdk-10.0 from Ubuntu's own archive, as the cloud sessions install it (.claude/hooks/session-start.sh);
#   global.json's rollForward takes its feature band.
# - ffmpeg turns the e2e recordings into GIFs and MP4s (tools/e2e/record.sh).
# - python3-requests and python3-bs4: the Compendium download (tools/compendium/sheet_dump.py).
# Node comes from devcontainer.json's node feature; Playwright's Chromium from post-create.sh (it needs Node).
FROM mcr.microsoft.com/devcontainers/base:noble
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      dotnet-sdk-10.0 ffmpeg python3 python3-requests python3-bs4 \
 && rm -rf /var/lib/apt/lists/*
