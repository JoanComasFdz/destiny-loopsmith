#!/usr/bin/env bash
# Records the end-to-end scenarios as GIFs and MP4s: one of each per screen a scenario is played on ([PlayInDesktop] /
# [PlayInMobile]). Two projects hold scenarios: tests/Loopsmith.Web.E2E (the web app: its Scenarios/, and its Rules/, one
# scenario per rule claim) and tests/Loopsmith.VsCodeExtension.E2E (the VS Code extension, played in VS Code for the web).
#   tools/e2e/record.sh [all | <domain folder> | <TestClass> ...] [--changed-since <git ref>] [--action-gap <seconds>]
#                       [--out <dir>] [--no-publish] [--no-video]
# A domain folder is a folder under a project's Scenarios/ (Starting, Build/Subclass, Recordings), or the web project's
# Rules/ or a folder under it (Rules, Rules/Hunter/Arc); a class is a scenario class (StartFromScratchTests). A rule
# scenario's GIF lands under <out>/Rules/<…>/ and the "E2E videos" workflow keeps it in its rule group's release
# (docs/hosting.md, "E2E videos on pull requests").
# --changed-since <ref>: the scenario classes added or changed since <ref> (from where the branch left it, working tree
# included), plus those that use a changed fixture file (for the extension's scenarios, any file of its Workspace/):
# "this PR's scenarios" with origin/main. scenarios.json says why each was picked ("selected": "changed", or "fixtures"
# with the changed "fixtures", each a path in its project: Scenarios/Loop/Weapons/…, Workspace/…); a class that changed
# and uses a changed fixture, and a class or domain folder named here, is "changed".
# --action-gap <seconds>: the least time between two steps (an action or a read) of a recorded scenario (default 1).
# For web scenarios it publishes the web app (Release), prepares it for /destiny-loopsmith/ (tools/web/prepare-pages.sh)
# and serves it locally. For the extension's scenarios it serves VS Code for the web (@vscode/test-web, pinned with the
# VS Code build in tests/Loopsmith.VsCodeExtension.E2E/vscode-web/package.json; the build is cached in
# ${E2E_VSCODE_CACHE:-~/.cache/loopsmith/vscode-web}) with src/Loopsmith.VsCodeExtension, opened on that project's
# Workspace/. It runs the chosen scenarios with video on, trims each video from when Home (or VS Code's workbench) was
# shown (the startup screen left out) and turns it into a GIF under <out>/<domain folder>/<gif name>.gif (20 fps; a
# desktop run 800 px wide, a phone run at its 390 CSS pixels; the GIF name ends in -desktop or -mobile) and an MP4 next to
# it, <gif name>.mp4 (H.264 at the video's own size: 1280×800, a phone's 780×1688). Both hold their last frame 3 s
# before they end. The videos are converted one per core at a time. It prints each
# scenario's startup time (page open → Home shown) and the requests that left for bungie.net, and writes
# <out>/scenarios.json. Each rule a recorded scenario's Gherkin names ("# Rule <name>: …") is drawn by the rule viewer
# (tools/rule-viewer, --rule, in its dark theme) as <out>/<domain folder>/<rule name>.png, its "flow" in scenarios.json, which the post shows
# under the scenario's title; a rule it can't draw is its "flowError" and stops nothing. Exits non-zero when a scenario
# failed (its GIF is still written).
# --no-video: the same scenarios against the same locally served publish, unrecorded and unpaced (no E2E_VIDEO_DIR):
# the fast run while developing. It prints each run's result and time and the requests that left for bungie.net, exits
# non-zero when a run failed, and leaves the last recording's GIFs, MP4s and scenarios.json in <out> as they are. With
# --no-publish it also skips the publish (about a minute): fine when only scenario code changed, not the web app.
# Right before a push that adds or changes scenarios (never between pushes): tools/e2e/record.sh --changed-since
# origin/main, then tools/e2e/push-gifs.sh right after the push, so the GIFs posted match the pushed code.
# The manifest's files come from ${E2E_MANIFEST_CACHE:-~/.cache/loopsmith/bungie-manifest} (filled once from bungie.net).
set -euo pipefail

repo="$(cd "$(dirname "$0")/../.." && pwd)"
web_project="$repo/tests/Loopsmith.Web.E2E"
vscode_project="$repo/tests/Loopsmith.VsCodeExtension.E2E"
projects=("$web_project" "$vscode_project")
# The folders a project's scenario classes live in, relative to it: the web project's rule scenarios sit in Rules/.
scenario_roots() {
  if [ "$1" = "$web_project" ]; then echo Scenarios Rules; else echo Scenarios; fi
}
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
    -h|--help) sed -n '2,38p' "$0"; exit 0 ;;
    *) chosen+=("$1"); shift ;;
  esac
done
mkdir -p "$out"
out="$(cd "$out" && pwd)"

# The project (web, vscode) whose Scenarios/ (or Rules/, the web project's) holds a scenario class's file, or nothing.
find_class_project() {
  if [ -n "$(find "$web_project/Scenarios" "$web_project/Rules" -name "$1.cs" -print -quit 2> /dev/null)" ]; then echo web; fi
  if [ -n "$(find "$vscode_project/Scenarios" -name "$1.cs" -print -quit)" ]; then echo vscode; fi
}

# The scenario classes changed since a ref, and those whose fixture files changed, in both projects, each on a line with
# why it was picked: "<class>\tchanged", or "<class>\tfixtures\t<the fixture's path in its project>" (one line per
# changed fixture). A class uses a fixture when it names its whole file name in double quotes
# (ImportBuildAsync("arc-staff.build.yaml")), so a fixture whose name ends another's (arc-staff.build.yaml,
# thunderous-retort-disorienting-blow-arc-staff.build.yaml) selects only its own classes; FixtureNamingTests keeps every
# fixture named that way. The extension's scenarios all open the files of its project's Workspace/: a change there selects
# every one of them.
list_changed_classes() {
  local base
  base="$(git -C "$repo" merge-base "$1" HEAD)"
  local project root file class
  for project in "${projects[@]}"; do
    for root in $(scenario_roots "$project"); do
      local scenarios="${project#"$repo"/}/$root"
      [ -d "$repo/$scenarios" ] || continue
      local files
      files="$( { git -C "$repo" diff --name-only --diff-filter=AMR "$base" -- "$scenarios";
                  git -C "$repo" ls-files --others --exclude-standard -- "$scenarios"; } | sort -u)"
      for file in $files; do
        case "$file" in
          *Tests.cs) printf '%s\tchanged\n' "$(basename "$file" .cs)" ;;
          *.cs) ;;
          *)
            for class in $(cd "$project" && grep -rlF --include='*Tests.cs' "\"$(basename "$file")\"" $(scenario_roots "$project") 2> /dev/null | xargs -r -n1 basename | sed 's/\.cs$//'); do
              printf '%s\tfixtures\t%s\n' "$class" "${file#"${project#"$repo"/}"/}"
            done ;;
        esac
      done
    done
  done
  local workspace="${vscode_project#"$repo"/}/Workspace"
  local changed_workspace
  changed_workspace="$( { git -C "$repo" diff --name-only "$base" -- "$workspace";
                         git -C "$repo" ls-files --others --exclude-standard -- "$workspace"; } | sort -u)"
  for file in $changed_workspace; do
    for class in $(find "$vscode_project/Scenarios" -name '*Tests.cs' -exec basename {} .cs \;); do
      printf '%s\tfixtures\t%s\n' "$class" "${file#"${vscode_project#"$repo"/}"/}"
    done
  done
}

# Each project's filters, joined into one ('|' is "or" to dotnet test); a project with none doesn't run.
web_filters=()
vscode_filters=()
add_class() {
  local where
  for where in $(find_class_project "$1"); do
    if [ "$where" = web ]; then web_filters+=("FullyQualifiedName~.$1."); else vscode_filters+=("FullyQualifiedName~.$1."); fi
  done
}
everything=0
# Why each class was picked (list_changed_classes' lines), for scenarios.json: a class not in it was named on the command
# line, and counts as changed.
selection="$out/.selection.tsv"
: > "$selection"
if [ -n "$changed_since" ]; then
  list_changed_classes "$changed_since" | sort -u > "$selection"
  while IFS= read -r class; do
    if [ -n "$class" ]; then add_class "$class"; fi
  done < <(cut -f1 "$selection" | sort -u)
  if [ ${#web_filters[@]} -eq 0 ] && [ ${#vscode_filters[@]} -eq 0 ] && [ ${#chosen[@]} -eq 0 ]; then
    echo "No scenario changed since $changed_since."
    if [ $video -eq 1 ]; then echo "[]" > "$out/scenarios.json"; fi
    exit 0
  fi
fi
for choice in "${chosen[@]}"; do
  choice="${choice%/}"
  if [ "$choice" = "all" ]; then
    everything=1
  elif [[ $choice == Rules || $choice == Rules/* ]] && [ -d "$web_project/$choice" ]; then
    web_filters+=("FullyQualifiedName~Loopsmith.Web.E2E.${choice//\//.}.")
  elif [ -d "$web_project/Scenarios/$choice" ] || [ -d "$vscode_project/Scenarios/$choice" ]; then
    if [ -d "$web_project/Scenarios/$choice" ]; then
      web_filters+=("FullyQualifiedName~Loopsmith.Web.E2E.Scenarios.${choice//\//.}.")
    fi
    if [ -d "$vscode_project/Scenarios/$choice" ]; then
      vscode_filters+=("FullyQualifiedName~Loopsmith.VsCodeExtension.E2E.Scenarios.${choice//\//.}.")
    fi
  elif [ -n "$(find_class_project "$choice")" ]; then
    add_class "$choice"
  else
    echo "No domain folder or scenario class named '$choice' under $web_project/Scenarios, $web_project/Rules or $vscode_project/Scenarios" >&2
    exit 2
  fi
done
if [ -z "$changed_since" ] && [ ${#chosen[@]} -eq 0 ]; then
  everything=1
fi
web_filter=""
vscode_filter=""
if [ $everything -eq 1 ]; then
  web_filter="FullyQualifiedName~Loopsmith.Web.E2E.Scenarios.|FullyQualifiedName~Loopsmith.Web.E2E.Rules."
  vscode_filter="FullyQualifiedName~Loopsmith.VsCodeExtension.E2E.Scenarios."
else
  if [ ${#web_filters[@]} -gt 0 ]; then web_filter="$(IFS='|'; echo "${web_filters[*]}")"; fi
  if [ ${#vscode_filters[@]} -gt 0 ]; then vscode_filter="$(IFS='|'; echo "${vscode_filters[*]}")"; fi
fi

site="$out/.site"
videos="$out/.videos"
if [ $video -eq 1 ]; then
  rm -rf "$videos"
  mkdir -p "$videos"
fi

free_port() {
  python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}
servers=()
trap 'for pid in "${servers[@]}"; do kill "$pid" 2>/dev/null || true; done' EXIT

web_url=""
if [ -n "$web_filter" ]; then
  if [ $publish -eq 1 ] || [ ! -d "$site/root/destiny-loopsmith" ]; then
    echo "Publishing the web app (Release)…"
    rm -rf "$site"
    dotnet publish "$repo/src/Loopsmith.Web" -c Release -o "$site/publish" --nologo -v q
    mkdir -p "$site/root"
    cp -r "$site/publish/wwwroot" "$site/root/destiny-loopsmith"
    "$repo/tools/web/prepare-pages.sh" "$site/root/destiny-loopsmith" /destiny-loopsmith/ --root
  fi
  port="$(free_port)"
  python3 -m http.server "$port" --bind 127.0.0.1 --directory "$site/root" > "$out/.server.log" 2>&1 &
  servers+=($!)
  for _ in $(seq 50); do
    curl -sf "http://127.0.0.1:$port/destiny-loopsmith/" > /dev/null && break
    sleep 0.2
  done
  web_url="http://127.0.0.1:$port/destiny-loopsmith/"
fi

vscode_url=""
if [ -n "$vscode_filter" ]; then
  runner="$vscode_project/vscode-web"
  if [ ! -d "$runner/node_modules/@vscode/test-web" ] || [ "$runner/package-lock.json" -nt "$runner/node_modules" ]; then
    echo "Installing VS Code for the web's runner (npm ci)…"
    npm ci --prefix "$runner" --silent --no-audit --no-fund
    touch "$runner/node_modules"
  fi
  commit="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1]))["config"]["vscodeCommit"])' "$runner/package.json")"
  cache="${E2E_VSCODE_CACHE:-$HOME/.cache/loopsmith/vscode-web}"
  mkdir -p "$cache"
  vport="$(free_port)"
  echo "Serving VS Code for the web (build ${commit:0:7}) with the extension…"
  "$runner/node_modules/.bin/vscode-test-web" --browser=none --quality stable --commit "$commit" --port "$vport" \
    --testRunnerDataDir "$cache" --extensionDevelopmentPath="$repo/src/Loopsmith.VsCodeExtension" \
    "$vscode_project/Workspace" > "$out/.vscode-web.log" 2>&1 &
  servers+=($!)
  for _ in $(seq 600); do
    curl -sf "http://localhost:$vport/" > /dev/null && break
    sleep 0.5
  done
  curl -sf "http://localhost:$vport/" > /dev/null || { echo "VS Code for the web didn't start: see $out/.vscode-web.log" >&2; exit 1; }
  vscode_url="http://localhost:$vport/"
fi

status=0
if [ $video -eq 0 ]; then
  results="$out/.results"
  bungie_log="$out/.bungie-requests.log"
  rm -rf "$results" "$bungie_log"
  trx=()
  # A recording's variables left in the shell would record and pace this run too: unset.
  if [ -n "$web_filter" ]; then
    echo "Running the web scenarios, unrecorded ($web_filter)…"
    env -u E2E_VIDEO_DIR -u E2E_ACTION_GAP E2E_URL="$web_url" E2E_BUNGIE_LOG="$bungie_log" \
      dotnet test "$web_project" -c Release --nologo --filter "$web_filter" --results-directory "$results" \
        --logger "trx;LogFileName=web.trx" --logger "console;verbosity=normal" || status=$?
    trx+=("$results/web.trx")
  fi
  if [ -n "$vscode_filter" ]; then
    echo "Running the extension's scenarios, unrecorded ($vscode_filter)…"
    env -u E2E_VIDEO_DIR -u E2E_ACTION_GAP E2E_VSCODE_URL="$vscode_url" \
      dotnet test "$vscode_project" -c Release --nologo --filter "$vscode_filter" --results-directory "$results" \
        --logger "trx;LogFileName=vscode.trx" --logger "console;verbosity=normal" || status=$?
    trx+=("$results/vscode.trx")
  fi
  # Each run's result and time, from the test results: without a recording no scenario writes its record.
  # It fails when no run passed or failed (none matched, or all skipped), like a recording with no record.
  PYTHONIOENCODING=utf-8 python3 - "${trx[@]}" <<'PY' || status=1
import json, os, re, sys, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
runs = []
for path in sys.argv[1:]:
    if not os.path.exists(path):
        print(f"No test results were written ({os.path.basename(path)}).", file=sys.stderr)
        sys.exit(1)
    runs += ET.parse(path).getroot().findall(".//t:UnitTestResult", ns)
def seconds(duration):
    h, m, s = duration.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)
# The screen in a run's name ("[📱 Mobile]") comes escaped: decoded back.
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
  if [ -n "$web_filter" ]; then
    if [ -s "$bungie_log" ]; then
      echo "Requests that left for bungie.net, or were refused ($(wc -l < "$bungie_log")):"
      sed 's/^/  /' "$bungie_log"
    else
      echo "Requests that left for bungie.net: none."
    fi
  fi
  echo "Nothing recorded: the GIFs, MP4s and scenarios.json in $out are still the last recording's."
  exit $status
fi

if [ -n "$web_filter" ]; then
  echo "Running the web scenarios ($web_filter)…"
  E2E_URL="$web_url" E2E_VIDEO_DIR="$videos" E2E_ACTION_GAP="$action_gap" \
    dotnet test "$web_project" -c Release --nologo --filter "$web_filter" --logger "console;verbosity=normal" || status=$?
fi
if [ -n "$vscode_filter" ]; then
  echo "Running the extension's scenarios ($vscode_filter)…"
  E2E_VSCODE_URL="$vscode_url" E2E_VIDEO_DIR="$videos" E2E_ACTION_GAP="$action_gap" \
    dotnet test "$vscode_project" -c Release --nologo --filter "$vscode_filter" --logger "console;verbosity=normal" || status=$?
fi

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

# Each recorded run: trimmed from when Home was shown, its last frame held 3 s, as a GIF next to its domain folder's
# others (20 fps; a desktop window scaled to 800 px wide, a phone to its 390 CSS pixels) and as an MP4 (H.264 at the
# video's own size and frame rate, for the player page: docs/hosting.md, "E2E videos on pull requests").
hold_end="tpad=stop_mode=clone:stop_duration=3"
palette_at() {
  echo "$hold_end,fps=20,scale=$1:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle"
}
# The last run's GIFs go (only the folders named like the projects' Scenarios/ domain folders, and Rules/).
for folder in "$web_project/Scenarios"/*/ "$vscode_project/Scenarios"/*/; do rm -rf "${out:?}/$(basename "$folder")"; done
rm -rf "${out:?}/Rules"
convert_run() {
  local record="$1" video="${1%.json}.webm" domain gif screen noted width=800 offset
  read -r domain gif screen noted < <(python3 -c 'import json,sys; r=json.load(open(sys.argv[1])); s=r["homeShownSeconds"]; print(r["domain"], r["gif"], r["screen"], 0 if s is None else s)' "$record") || return 1
  if [ "$screen" = mobile ]; then width=390; fi
  offset="$(find_home_in_video "$video" "$noted")" || return 1
  mkdir -p "$out/$domain" || return 1
  ffmpeg -nostdin -loglevel error -y -ss "$offset" -i "$video" -vf "$(palette_at $width)" "$out/$domain/$gif.gif" || return 1
  ffmpeg -nostdin -loglevel error -y -ss "$offset" -i "$video" -vf "$hold_end" -an \
    -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart "$out/$domain/$gif.mp4"
}
# One conversion per core at a time, the MP4 with x264's veryfast preset: 28 runs on a 4-core session in about 2 minutes
# (9 one after another with the slow preset, 5 converting in parallel with it; veryfast's MP4s look the same on the
# player page and are smaller: tests/Loopsmith.Web.E2E/README.md, "How long a recording takes"). A conversion that
# fails names its video and fails the recording.
conversions="$(nproc)"
failed_conversions="$out/.failed-conversions"
: > "$failed_conversions"
records=()
# The conversions running (the servers run in the background too: never waited for).
converting=()
while IFS= read -r record; do
  records+=("$record")
  while [ ${#converting[@]} -ge "$conversions" ]; do
    wait -n "${converting[@]}" || true
    running=()
    for pid in "${converting[@]}"; do if kill -0 "$pid" 2> /dev/null; then running+=("$pid"); fi; done
    converting=("${running[@]}")
  done
  { convert_run "$record" || echo "${record%.json}.webm" >> "$failed_conversions"; } < /dev/null &
  converting+=($!)
done < <(find "$videos" -path "$videos/.raw" -prune -o -name '*.json' -print | sort)
if [ ${#converting[@]} -gt 0 ]; then wait "${converting[@]}" || true; fi

# Each rule a recorded scenario names (its Gherkin's "# Rule <name>:" line) drawn as its flow, by the rule viewer, next to
# the scenario's GIFs: <out>/<domain folder>/<rule name>.png, which the post shows under the scenario's title. A rule the
# viewer can't draw keeps its error for scenarios.json ("flowError") and the recording carries on (docs/hosting.md, "E2E
# videos on pull requests").
flow_errors="$out/.flow-errors"
rm -rf "$flow_errors"
mkdir -p "$flow_errors"
flows="$(python3 -c '
import json, re, sys
seen = set()
for path in sys.argv[1:]:
    r = json.load(open(path))
    for rule in re.findall(r"^\s*# Rule ([a-z0-9-]+):", r.get("gherkin") or "", re.M):
        if (r["domain"], rule) not in seen:
            seen.add((r["domain"], rule))
            print(r["domain"], rule)
' "${records[@]}")"
if [ -n "$flows" ]; then
  echo "Drawing the rules' flows ($(wc -l <<< "$flows"))…"
  viewer="$repo/tools/rule-viewer/Loopsmith.RuleViewer"
  if dotnet build "$viewer" -c Release --nologo -v q > "$flow_errors/.build.log" 2>&1; then
    while read -r domain rule; do
      mkdir -p "$out/$domain"
      if drawn="$(cd "$repo" && dotnet run --no-build -c Release --project "$viewer" -- --rule "$rule" --theme dark --out "$out/$domain" 2>&1)"; then
        echo "  $domain/$rule.png"
      else
        { printf '%s\n' "$drawn" | grep -v '^\s*$' || echo "the rule viewer failed with no message"; } | tail -1 > "$flow_errors/$rule.txt"
        echo "  no flow for $rule: $(cat "$flow_errors/$rule.txt")"
      fi
    done <<< "$flows"
  else
    echo "  the rule viewer didn't build ($flow_errors/.build.log): no flow drawn."
    while read -r domain rule; do echo "the rule viewer didn't build" > "$flow_errors/$rule.txt"; done <<< "$flows"
  fi
fi

python3 - "$out" "$selection" "${records[@]}" <<'PY'
import json, os, re, sys
out, selection, records = sys.argv[1], sys.argv[2], sys.argv[3:]
# A class changed itself, or only a fixture it uses changed (and which ones); changed wins over fixtures.
fixtures, changed = {}, set()
for line in open(selection, encoding="utf-8").read().splitlines():
    parts = line.split("\t")
    if len(parts) >= 2 and parts[1] == "changed":
        changed.add(parts[0])
    elif len(parts) == 3 and parts[1] == "fixtures":
        fixtures.setdefault(parts[0], []).append(parts[2])
def select(entry, cls):
    if cls in fixtures and cls not in changed:
        entry["selected"] = "fixtures"
        entry["fixtures"] = sorted(set(fixtures[cls]))
    else:
        entry["selected"] = "changed"
    return entry
# The flow of the rule its Gherkin names, drawn above: its PNG, or why there is none.
def add_flow(entry):
    found = re.search(r"^\s*# Rule ([a-z0-9-]+):", entry["gherkin"] or "", re.M)
    if not found:
        return entry
    rule = entry["rule"] = found.group(1)
    png = f'{entry["domain"]}/{rule}.png'
    error = os.path.join(out, ".flow-errors", f"{rule}.txt")
    if os.path.exists(os.path.join(out, png)):
        entry["flow"] = png
    else:
        entry["flowError"] = open(error, encoding="utf-8").read().strip() if os.path.exists(error) else "not drawn"
    return entry
entries = []
for path in records:
    r = json.load(open(path))
    entries.append(add_flow(select({
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
    }, r["class"])))
json.dump(entries, open(os.path.join(out, "scenarios.json"), "w"), indent=2)
print()
print("Scenarios:")
for e in entries:
    startup = "Home never shown" if e["startupSeconds"] is None else f'startup {e["startupSeconds"]} s'
    why = "" if e["selected"] == "changed" else f'  (re-recorded: {", ".join(e["fixtures"])} changed)'
    print(f'  {"passed" if e["passed"] else "FAILED"}  {startup:>16}  {e["gif"]}{why}')
PY

log="$videos/bungie-requests.log"
if [ -s "$log" ]; then
  echo "Requests that left for bungie.net, or were refused ($(wc -l < "$log")):"
  sed 's/^/  /' "$log"
else
  echo "Requests that left for bungie.net: none."
fi
echo "GIFs, MP4s and scenarios.json in $out"

if [ -s "$failed_conversions" ]; then
  echo "Videos that couldn't be turned into a GIF and an MP4 ($(wc -l < "$failed_conversions")):" >&2
  sed 's/^/  /' "$failed_conversions" >&2
  status=1
fi
if [ $status -eq 0 ] && grep -q '"passed": false' "$out/scenarios.json"; then status=1; fi
if [ $status -eq 0 ] && [ ${#records[@]} -eq 0 ]; then echo "No scenario ran." >&2; status=1; fi
exit $status
