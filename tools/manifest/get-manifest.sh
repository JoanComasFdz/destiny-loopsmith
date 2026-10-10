#!/usr/bin/env bash
# Downloads the English manifest components extract_manifest.py reads, into <out-dir>:
#   tools/manifest/get-manifest.sh <out-dir>
# The component paths come from Bungie's manifest index (https://www.bungie.net/Platform/Destiny2/Manifest/;
# sent with $BUNGIE_API_KEY as X-API-Key when it is set). Where the index can't be read (a cloud session got
# bungie.net's 500 error page for it), give its content id and version instead:
#   MANIFEST_ID=a8ba855a-93c7-4014-8f2e-d92357fdfb42 MANIFEST_VERSION=244213.26.06.29.2000-1-bnet.65864 \
#     tools/manifest/get-manifest.sh <out-dir>
# The component files themselves (…/common/destiny2_content/json/en/…) need no key. Then:
#   python3 -I tools/manifest/extract_manifest.py <out-dir> "$(cat <out-dir>/version.txt)"
set -euo pipefail

out="$1"
mkdir -p "$out"
components=(DestinyInventoryItemLiteDefinition DestinyInventoryBucketDefinition DestinyDamageTypeDefinition)

if [ -z "${MANIFEST_ID:-}" ]; then
  header=()
  [ -n "${BUNGIE_API_KEY:-}" ] && header=(-H "X-API-Key: ${BUNGIE_API_KEY}")
  curl -fsS "${header[@]}" -o "$out/index.json" https://www.bungie.net/Platform/Destiny2/Manifest/
  MANIFEST_VERSION=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["Response"]["version"])' "$out/index.json")
  MANIFEST_ID=$(python3 -I -c 'import json,re,sys; p=json.load(open(sys.argv[1]))["Response"]["jsonWorldComponentContentPaths"]["en"]["DestinyInventoryItemLiteDefinition"]; print(re.search(r"Definition-(.+)\.json$", p).group(1))' "$out/index.json")
fi

for component in "${components[@]}"; do
  curl -fsS --compressed -o "$out/$component.json" \
    "https://www.bungie.net/common/destiny2_content/json/en/$component-$MANIFEST_ID.json"
done
echo "${MANIFEST_VERSION:-unknown}" > "$out/version.txt"
echo "manifest ${MANIFEST_VERSION:-unknown} (${MANIFEST_ID}) in $out"
