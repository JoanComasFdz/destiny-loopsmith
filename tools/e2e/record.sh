#!/usr/bin/env bash
# Records the web app's end-to-end scenarios (tests/Loopsmith.Web.E2E) as GIFs and MP4s: one of each per screen a
# scenario is played on ([PlayInDesktop] / [PlayInMobile]).
#   tools/e2e/record.sh [all | <domain folder> | <TestClass> ...] [--changed-since <git ref>] [--action-gap <seconds>]
#                       [--out <dir>] [--no-publish] [--no-video]
# A domain folder is a folder under Scenarios/ (Starting, Build/Subclass); a class is a scenario class (StartFromScratchTests).
# --changed-since <ref>: the scenario classes added or changed since <ref> (from where the branch left it, working tree
# included), plus those that use a changed fixture file: "this PR's scenarios" with origin/main.
# --action-gap <seconds>: the least time between two steps (an action or a read) of a recorded scenario (default 1).
# It publishes the web app (Release), prepares it for /destiny-loopsmith/ (tools/web/prepare-pages.sh), serves it
# locally, runs the chosen scenarios with E2E_URL set and video on (screenshots at the device's pixels), turns each
# run's screenshots into a 25 fps video, trims it from when Home was shown (the manifest's startup screen left out) and
# turns it into a GIF under <out>/<domain folder>/<gif name>.gif (20 fps; a desktop run 800 px wide, a phone run at its
# 390 CSS pixels; the GIF name ends in -desktop or -mobile) and an MP4 next to it, <gif name>.mp4 (H.264 at 25 fps and
# the video's own size: 1280×800, a phone's 780×1688). Both hold their last frame 3 s
# before they end, and it prints how many frames a second each run took. It prints each
# scenario's startup time (page open → Home shown) and the requests that left for bungie.net, and writes
# <out>/scenarios.json. Exits non-zero when a scenario failed (its GIF is still written).
# --no-video: the same scenarios against the same locally served publish, unrecorded and unpaced (no E2E_VIDEO_DIR):
# the fast run while developing. It prints each run's result and time and the requests that left for bungie.net, exits
# non-zero when a run failed, and leaves the last recording's GIFs, MP4s and scenarios.json in <out> as they are. With
# --no-publish it also skips the publish (about a minute): fine when only scenario code changed, not the web app.
# Right before a push that adds or changes scenarios (never between pushes): tools/e2e/record.sh --changed-since
# origin/main, then tools/e2e/push-gifs.sh right after the push, so the GIFs posted match the pushed code.
# The manifest's files come from ${E2E_MANIFEST_CACHE:-~/.cache/loopsmith/bungie-manifest} (filled once from bungie.net).
set -euo pipefail

repo="$(cd "$(dirname "$0")/../.." && pwd)"
project="$repo/tests/Loopsmith.Web.E2E"
scenarios_dir="$project/Scenarios"
out="$repo/out"
changed_since=""
action_gap=1
publish=1
video=1
chosen=()

while [ $# -gt 0 ]; do
  case "$1" in
    --changed-since) changed_since="$2"; shift 2 ;;
    --action-gap) action_gap="$2"; shift 2 ;;
    --out) out="$2"; shift 2 ;;
    --no-publish) publish=0; shift ;;
    --no-video) video=0; shift ;;
    -h|--help) sed -n '2,24p' "$0"; exit 0 ;;
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
    if [ $video -eq 1 ]; then echo "[]" > "$out/scenarios.json"; fi
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
if [ $video -eq 1 ]; then
  rm -rf "$videos"
  mkdir -p "$videos"
fi

if [ $publish -eq 1 ] || [ ! -d "$site/root/destiny-loopsmith" ]; then
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

status=0
if [ $video -eq 0 ]; then
  echo "Running the scenarios, unrecorded ($filter)…"
  results="$out/.results"
  bungie_log="$out/.bungie-requests.log"
  rm -rf "$results" "$bungie_log"
  # A recording's variables left in the shell would record and pace this run too: unset.
  env -u E2E_VIDEO_DIR -u E2E_ACTION_GAP E2E_URL="http://127.0.0.1:$port/destiny-loopsmith/" E2E_BUNGIE_LOG="$bungie_log" \
    dotnet test "$project" -c Release --nologo --filter "$filter" --results-directory "$results" \
      --logger "trx;LogFileName=runs.trx" --logger "console;verbosity=normal" || status=$?
  # Each run's result and time, from the test results: without a recording no scenario writes its record.
  # It fails when no run passed or failed (none matched, or all skipped), like a recording with no record.
  PYTHONIOENCODING=utf-8 python3 - "$results/runs.trx" <<'PY' || status=1
import json, os, re, sys, xml.etree.ElementTree as ET
path = sys.argv[1]
if not os.path.exists(path):
    print("No test results were written.", file=sys.stderr)
    sys.exit(1)
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
runs = ET.parse(path).getroot().findall(".//t:UnitTestResult", ns)
def seconds(duration):
    h, m, s = duration.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)
# The screen in a run's name ("[📱 Mobile]") comes escaped ("📱"): decoded back.
def name_of(run):
    return re.sub(r"(?:\\u[0-9a-fA-F]{4})+", lambda m: json.loads(f'"{m.group(0)}"'), run.get("testName", ""))
print()
print("Runs (unrecorded):")
for r in sorted(runs, key=name_of):
    outcome = {"Passed": "passed", "Failed": "FAILED", "NotExecuted": "skipped"}.get(r.get("outcome"), r.get("outcome"))
    print(f'  {outcome:>7}  {seconds(r.get("duration", "0:0:0")):5.1f} s  {name_of(r)}')
if not any(r.get("outcome") in ("Passed", "Failed") for r in runs):
    print("No scenario ran.", file=sys.stderr)
    sys.exit(1)
PY
  if [ -s "$bungie_log" ]; then
    echo "Requests that left for bungie.net, or were refused ($(wc -l < "$bungie_log")):"
    sed 's/^/  /' "$bungie_log"
  else
    echo "Requests that left for bungie.net: none."
  fi
  echo "Nothing recorded: the GIFs, MP4s and scenarios.json in $out are still the last recording's."
  exit $status
fi

echo "Running the scenarios ($filter)…"
E2E_URL="http://127.0.0.1:$port/destiny-loopsmith/" E2E_VIDEO_DIR="$videos" E2E_ACTION_GAP="$action_gap" \
  dotnet test "$project" -c Release --nologo --filter "$filter" --logger "console;verbosity=normal" || status=$?

# When Home shows in a video: the test notes it on the frames' own clock, but a frame can come up to one frame gap late,
# so the noted time only says where to look. The first big change of picture from a second before it to three after (the
# startup screen giving way to Home; the click comes at least two seconds after Home) is where Home starts; without one,
# the noted time.
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

# Each recorded run: its screenshots (BaseE2E, ScreenRecorder: each frame timed on the test's clock, at the device's
# pixels) as a 25 fps video, losslessly, an odd width or height padded to even (H.264's yuv420p needs it);
# then trimmed from when Home was shown, its last frame held 3 s, as a GIF next to its domain folder's others (20 fps; a
# desktop window scaled to 800 px wide, a phone to its 390 CSS pixels) and as an MP4 (H.264 at 25 fps and the video's
# own size, 1280×800 or 780×1688, for the player page: docs/hosting.md, "E2E videos on pull requests").
hold_end="tpad=stop_mode=clone:stop_duration=3"
palette_at() {
  echo "$hold_end,fps=20,scale=$1:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
}
# The last run's GIFs go (only the folders named like Scenarios/' domain folders).
for folder in "$scenarios_dir"/*/; do rm -rf "${out:?}/$(basename "$folder")"; done
records=()
while IFS= read -r record; do
  records+=("$record")
  video="${record%.json}.mkv"
  ffmpeg -nostdin -loglevel error -y -f concat -safe 0 -i "${record%.json}.frames/frames.ffconcat" \
    -vf "fps=25,pad=ceil(iw/2)*2:ceil(ih/2)*2" -c:v libx264 -preset ultrafast -qp 0 "$video"
  read -r domain gif screen noted < <(python3 -c 'import json,sys; r=json.load(open(sys.argv[1])); s=r["homeShownSeconds"]; print(r["domain"], r["gif"], r["screen"], 0 if s is None else s)' "$record")
  width=800
  if [ "$screen" = mobile ]; then width=390; fi
  offset="$(find_home_in_video "$video" "$noted")"
  mkdir -p "$out/$domain"
  ffmpeg -nostdin -loglevel error -y -ss "$offset" -i "$video" -vf "$(palette_at $width)" "$out/$domain/$gif.gif"
  ffmpeg -nostdin -loglevel error -y -ss "$offset" -i "$video" -vf "$hold_end" -an \
    -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart "$out/$domain/$gif.mp4"
done < <(find "$videos" -path "$videos/.raw" -prune -o -name '*.json' -print | sort)

python3 - "$out" "${records[@]}" <<'PY'
import json, os, sys
out, records = sys.argv[1], sys.argv[2:]
entries = []
fps = []
for path in records:
    r = json.load(open(path))
    entries.append({
        "domain": r["domain"],
        "class": r["class"],
        "method": r["method"],
        "title": r["title"],
        "gherkin": r["gherkin"],
        "screen": r["screen"],
        "gif": f'{r["domain"]}/{r["gif"]}.gif',
        "mp4": f'{r["domain"]}/{r["gif"]}.mp4',
        "startupSeconds": None if r["homeShownSeconds"] is None else round(r["homeShownSeconds"], 1),
        "passed": r["passed"],
    })
    fps.append(r["framesPerSecond"])
json.dump(entries, open(os.path.join(out, "scenarios.json"), "w"), indent=2)
print()
print("Scenarios:")
for e, taken in zip(entries, fps):
    startup = "Home never shown" if e["startupSeconds"] is None else f'startup {e["startupSeconds"]} s'
    print(f'  {"passed" if e["passed"] else "FAILED"}  {startup:>16}  {taken:>4} frames/s taken  {e["gif"]}')
PY

log="$videos/bungie-requests.log"
if [ -s "$log" ]; then
  echo "Requests that left for bungie.net, or were refused ($(wc -l < "$log")):"
  sed 's/^/  /' "$log"
else
  echo "Requests that left for bungie.net: none."
fi
echo "GIFs, MP4s and scenarios.json in $out"

if [ $status -eq 0 ] && grep -q '"passed": false' "$out/scenarios.json"; then status=1; fi
if [ $status -eq 0 ] && [ ${#records[@]} -eq 0 ]; then echo "No scenario ran." >&2; status=1; fi
exit $status
