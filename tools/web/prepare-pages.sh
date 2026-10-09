#!/usr/bin/env bash
# Prepares a published Blazor WebAssembly site (dotnet publish …/wwwroot) for GitHub Pages.
#   tools/web/prepare-pages.sh <site-dir> <base-path> [--root]
# <base-path> is where the site is served, e.g. /destiny-loopsmith/ or /destiny-loopsmith/pr-preview/pr-7/.
# --root marks the main site: its 404.html is GitHub Pages' fallback for the whole domain path, so it also
# sends deep links inside a PR preview (…/pr-preview/pr-7/library) back to that preview's start page.
set -euo pipefail

site="$1"
base="$2"
is_root="${3:-}"

case "$base" in
  /*/) ;;
  *) echo "base path must start and end with '/': $base" >&2; exit 1 ;;
esac

index="$site/index.html"
sed -i -E "s|<base href=\"[^\"]*\" ?/?>|<base href=\"${base}\" />|" "$index"
grep -q "<base href=\"${base}\"" "$index" || { echo "could not set <base href> in $index" >&2; exit 1; }

# Pre-compressed copies would still carry the old <base href>; Pages compresses on its own.
rm -f "$site/index.html.br" "$site/index.html.gz"

# Jekyll would hide _framework/. Pages only reads .nojekyll at the site root, and a preview's own copy
# would outlive the preview (the preview action's removal skips dotfiles), so only the root gets one.
if [ "$is_root" = "--root" ]; then
  touch "$site/.nojekyll"
else
  rm -f "$site/.nojekyll"
fi

if [ "$is_root" = "--root" ]; then
  preview_redirect="<script>(function(){var m=location.pathname.match(new RegExp('^(.*/pr-preview/pr-[0-9]+/).'));if(m){location.replace(m[1]+location.hash);}})();</script>"
  sed "s|<head>|<head>${preview_redirect}|" "$index" > "$site/404.html"
  grep -q "pr-preview" "$site/404.html" || { echo "could not add the preview redirect to 404.html" >&2; exit 1; }
else
  rm -f "$site/404.html"
fi
