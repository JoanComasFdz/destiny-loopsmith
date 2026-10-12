#!/usr/bin/env bash
# Prepares a published Blazor WebAssembly site (dotnet publish …/wwwroot) for GitHub Pages.
#   tools/web/prepare-pages.sh <site-dir> <base-path> [--root]
# <base-path> is where the site is served, e.g. /destiny-loopsmith/ or /destiny-loopsmith/pr-preview/pr-7/.
# --root marks the main site: it gets a 404.html (a copy of index.html) as GitHub Pages' fallback for the whole
# domain path, so a deep link of the main site (…/destiny-loopsmith/compare) loads the app, and a deep link inside
# a PR preview (…/pr-preview/pr-7/compare) is sent back to that preview's start page. A preview gets no 404.html.
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

# The page's own stylesheet and scripts get their content's hash as a query (css/app.css?v=…), so a browser that
# cached the last deploy's copy (Pages lets it keep one for 10 minutes) fetches this one with the new markup.
for page in "$index" "$site/login.html"; do
  [ -f "$page" ] || continue
  for asset in $(grep -oE '(href|src)="(css|js)/[^"?]+\.(css|js)"' "$page" | sed -E 's/^(href|src)="//; s/"$//'); do
    [ -f "$site/$asset" ] || { echo "$page names $asset, which the site doesn't have" >&2; exit 1; }
    version=$(sha256sum "$site/$asset" | cut -c1-12)
    sed -i -E "s#(href|src)=\"${asset}\"#\1=\"${asset}?v=${version}\"#" "$page"
  done
done
grep -qE 'href="css/app\.css\?v=[0-9a-f]{12}"' "$index" || { echo "could not version css/app.css in $index" >&2; exit 1; }
rm -f "$site/login.html.br" "$site/login.html.gz"

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
