#!/usr/bin/env bash
# Records the web app's end-to-end scenarios (tests/Loopsmith.Web.E2E) as GIFs.
#   tools/e2e/record.sh [all | <domain folder> | <TestClass> ...] [--changed-since <git ref>] [--out <dir>] [--no-publish]
# A domain folder is a folder under Scenarios/ (Starting, Build/Subclass); a class is a scenario class (StartFromScratchTests).
# --changed-since <ref>: the scenario classes added or changed since <ref> (from where the branch left it, working tree
# included), plus those that use a changed fixture file: "this PR's scenarios" with origin/main.
# It publishes the web app (Release), prepares it for /destiny-loopsmith/ (tools/web/prepare-pages.sh), serves it
# locally, runs the chosen scenarios with E2E_URL set and video on, trims each video from when Home was shown (the
# manifest's startup screen left out) and turns it into a GIF under <out>/<domain folder>/<gif name>.gif. It prints each
# scenario's startup time (page open → Home shown) and the requests that left for bungie.net, and writes
# <out>/scenarios.json. Exits non-zero when a scenario failed (its GIF is still written).
# The manifest's files come from ${E2E_MANIFEST_CACHE:-~/.cache/loopsmith/bungie-manifest} (filled once from bungie.net).
set -euo pipefail

repo="$(cd "$(dirname "$0")/../.." && pwd)"
project="$repo/tests/Loopsmith.Web.E2E"
scenarios_dir="$project/Scenarios"
out="$repo/out"
changed_since=""
publish=1
chosen=()

while [ $# -gt 0 ]; do
  case "$1" in
    --changed-since) changed_since="$2"; shift 2 ;;
    --out) out="$2"; shift 2 ;;
    --no-publish) publish=0; shift ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) chosen+=("$1"); shift ;;
  esac
done
mkdir -p "$out"
out="$(cd "$out" && pwd)"

# The scenario classes changed since a ref, and those whose fixture files changed.
list_changed_classes() {
  local base
  base="$(git -C "$repo" merge-base "$1" HEAD)"
  local files
  files="$( { git -C "$repo" diff --name-only --diff-filter=AMR "$base" -- tests/Loopsmith.Web.E2E/Scenarios;
              git -C "$repo" ls-files --others --exclude-standard -- tests/Loopsmith.Web.E2E/Scenarios; } | sort -u)"
  local file
  for file in $files; do
    case "$file" in
      *Tests.cs) basename "$file" .cs ;;
      *.cs) ;;
      *) grep -rlF --include='*Tests.cs' "$(basename "$file")" "$scenarios_dir" | xargs -r -n1 basename | sed 's/\.cs$//' ;;
    esac
  done | sort -u
}

filters=()
if [ -n "$changed_since" ]; then
  while IFS= read -r class; do
    [ -n "$class" ] && filters+=("FullyQualifiedName~.$class.")
  done < <(list_changed_classes "$changed_since")
  if [ ${#filters[@]} -eq 0 ]; then
    echo "No scenario changed since $changed_since."
    echo "[]" > "$out/scenarios.json"
    exit 0
  fi
fi
for choice in "${chosen[@]}"; do
  if [ "$choice" = "all" ]; then
    filters=()
    break
  elif [ -d "$scenarios_dir/$choice" ]; then
    filters+=("FullyQualifiedName~Loopsmith.Web.E2E.Scenarios.${choice//\//.}.")
  elif [ -n "$(find "$scenarios_dir" -name "$choice.cs" -print -quit)" ]; then
    filters+=("FullyQualifiedName~.$choice.")
  else
    echo "No domain folder or scenario class named '$choice' under $scenarios_dir" >&2
    exit 2
  fi
done
# The filters are joined into one: dotnet test reads '|' as "or".
filter="FullyQualifiedName~Loopsmith.Web.E2E.Scenarios."
if [ ${#filters[@]} -gt 0 ]; then
  filter="$(IFS='|'; echo "${filters[*]}")"
fi

site="$out/.site"
videos="$out/.videos"
rm -rf "$videos"
mkdir -p "$videos"

if [ $publish -eq 1 ] || [ ! -d "$site/destiny-loopsmith" ]; then
  echo "Publishing the web app (Release)…"
  rm -rf "$site"
  dotnet publish "$repo/src/Loopsmith.Web" -c Release -o "$site/publish" --nologo -v q
  mkdir -p "$site/root"
  cp -r "$site/publish/wwwroot" "$site/root/destiny-loopsmith"
  "$repo/tools/web/prepare-pages.sh" "$site/root/destiny-loopsmith" /destiny-loopsmith/ --root
fi

port="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"
python3 -m http.server "$port" --bind 127.0.0.1 --directory "$site/root" > "$out/.server.log" 2>&1 &
server=$!
trap 'kill $server 2>/dev/null || true' EXIT
for _ in $(seq 50); do
  curl -sf "http://127.0.0.1:$port/destiny-loopsmith/" > /dev/null && break
  sleep 0.2
done

echo "Running the scenarios ($filter)…"
status=0
E2E_URL="http://127.0.0.1:$port/destiny-loopsmith/" E2E_VIDEO_DIR="$videos" \
  dotnet test "$project" -c Release --nologo --filter "$filter" --logger "console;verbosity=normal" || status=$?

# When Home shows in a video: the video's clock runs up to a second apart from the test's, so the time the test noted
# only says where to look. The first big change of picture from a second before it to three after (the startup screen
# giving way to Home; the click comes at least two seconds after Home) is where Home starts; without one, the noted time.
find_home_in_video() {
  local video="$1" noted="$2"
  python3 - "$video" "$noted" <<'PY'
import re, subprocess, sys
video, noted = sys.argv[1], float(sys.argv[2])
start = max(0.0, noted - 1.0)
probe = subprocess.run(
    ["ffmpeg", "-nostdin", "-hide_banner", "-ss", f"{start:.3f}", "-t", "4", "-i", video,
     "-vf", "select='gte(scene,0)',metadata=print:key=lavfi.scene_score", "-f", "null", "-"],
    capture_output=True, text=True).stderr
times = [float(t) for t in re.findall(r"pts_time:([0-9.]+)", probe)]
scores = [float(s) for s in re.findall(r"scene_score=([0-9.]+)", probe)]
cut = next((start + t for t, score in zip(times, scores) if score >= 0.08), None)
print(f"{(cut + 0.1) if cut is not None else noted + 0.2:.3f}")
PY
}

# Each recorded scenario: trimmed from when Home was shown, as a GIF next to its domain folder's others.
palette="fps=8,scale=800:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
# The last run's GIFs go (only the folders named like Scenarios/' domain folders).
for folder in "$scenarios_dir"/*/; do rm -rf "${out:?}/$(basename "$folder")"; done
records=()
while IFS= read -r record; do
  records+=("$record")
  video="${record%.json}.webm"
  read -r domain gif noted < <(python3 -c 'import json,sys; r=json.load(open(sys.argv[1])); s=r["homeShownSeconds"]; print(r["domain"], r["gif"], 0 if s is None else s)' "$record")
  offset="$(find_home_in_video "$video" "$noted")"
  mkdir -p "$out/$domain"
  ffmpeg -nostdin -loglevel error -y -ss "$offset" -i "$video" -vf "$palette" "$out/$domain/$gif.gif"
done < <(find "$videos" -path "$videos/.raw" -prune -o -name '*.json' -print | sort)

python3 - "$out" "${records[@]}" <<'PY'
import json, os, sys
out, records = sys.argv[1], sys.argv[2:]
entries = []
for path in records:
    r = json.load(open(path))
    entries.append({
        "domain": r["domain"],
        "class": r["class"],
        "title": r["title"],
        "gif": f'{r["domain"]}/{r["gif"]}.gif',
        "startupSeconds": None if r["homeShownSeconds"] is None else round(r["homeShownSeconds"], 1),
        "passed": r["passed"],
    })
json.dump(entries, open(os.path.join(out, "scenarios.json"), "w"), indent=2)
print()
print("Scenarios:")
for e in entries:
    startup = "Home never shown" if e["startupSeconds"] is None else f'startup {e["startupSeconds"]} s'
    print(f'  {"passed" if e["passed"] else "FAILED"}  {startup:>16}  {e["gif"]}')
PY

log="$videos/bungie-requests.log"
if [ -s "$log" ]; then
  echo "Requests that left for bungie.net, or were refused ($(wc -l < "$log")):"
  sed 's/^/  /' "$log"
else
  echo "Requests that left for bungie.net: none."
fi
echo "GIFs and scenarios.json in $out"

if [ $status -eq 0 ] && grep -q '"passed": false' "$out/scenarios.json"; then status=1; fi
if [ $status -eq 0 ] && [ ${#records[@]} -eq 0 ]; then echo "No scenario ran." >&2; status=1; fi
exit $status
