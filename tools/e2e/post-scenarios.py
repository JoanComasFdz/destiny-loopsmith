#!/usr/bin/env python3
"""Posts a pull request's recorded end-to-end scenarios to it.

  post-scenarios.py --pr <n> --commit <sha> [--out <dir>] [--repo <owner/repo>] [--dry-run]

Reads <out>/scenarios.json (tools/e2e/record.sh: one entry per run, a scenario played on a screen), uploads each run's
  GIF and MP4 to the `prs-resources` release as pr-<n>-v<version>-<the run's GIF name>.gif and .mp4 and posts them as a
  new comment on the PR that starts with what changed: <out>/what-changed.md as it is (push-gifs.sh --message), or else
  the body of --commit's message without its trailers, or its subject. Each scenario
  shows once: its title, its Gherkin, then a "🖥️ Desktop" and/or "📱 Mobile" subtitle with that screen's GIF and, below
  it, "▶ Play MP4": both link to the site's player page (e2e-player.html?v=<the MP4's name>), since GitHub shows no
  player for a video it didn't store itself (a tap on a bare GIF would only download it). It never touches the PR's description. The version is one more than the highest
  "## Scenarios (V<N>)" in this workflow's comments. A name longer than 200 characters keeps its start and ends with a
  short hash of the run's GIF name, the same for its GIF and its MP4.
Nothing recorded (an empty scenarios.json) posts nothing.

Uses the gh CLI (GH_TOKEN) on the repository in --repo or GITHUB_REPOSITORY. --dry-run prints instead of writing.
.github/workflows/e2e-videos.yml runs it on the GIFs a session pushed with tools/e2e/push-gifs.sh (--out e2e-gifs,
--commit the GIFs' commit); when the PR closes, the workflow keeps its newest version's assets and deletes the older
ones. docs/hosting.md, "E2E videos on pull requests", explains it.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

RELEASE = "prs-resources"
FOOTER = "_Recorded by the e2e workflow · Claude Code_"
BOT = "github-actions[bot]"
MAX_NAME = 200
HEADING = re.compile(r"^## Scenarios \(V(\d+)\)[ \t]*$", re.MULTILINE)
TRAILER = re.compile(r"^[A-Za-z][A-Za-z0-9-]*: \S")
SCREENS = {"desktop": "🖥️ Desktop", "mobile": "📱 Mobile"}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pr", type=int, required=True)
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY"))
    parser.add_argument("--commit", required=True, help="the commit whose message says what changed")
    parser.add_argument("--out", default="out", help="record.sh's output folder")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if not args.repo:
        sys.exit("No repository: pass --repo or set GITHUB_REPOSITORY.")
    post(args)


def post(args):
    scenarios = read_scenarios(args.out)
    if not scenarios:
        print("No scenario recorded: nothing to post.")
        return
    version = find_last_version(read_workflow_comments(args.repo, args.pr)) + 1
    for scenario in scenarios:
        scenario["asset"] = name_asset(args.pr, version, scenario, ".gif")
        scenario["mp4_asset"] = name_asset(args.pr, version, scenario, ".mp4")
    upload(args, scenarios)
    changed = read_what_changed(args.out) or describe_commit(read_commit_message(args.commit))
    add_comment(args, write_section(args.repo, version, scenarios, changed))


def read_scenarios(out):
    path = os.path.join(out, "scenarios.json")
    if not os.path.exists(path):
        sys.exit(f"{path} is missing: record.sh didn't get as far as recording (see its output above).")
    with open(path, encoding="utf-8") as file:
        entries = json.load(file)
    for entry in entries:
        entry["file"] = os.path.join(out, entry["gif"])
        entry["mp4_file"] = os.path.join(out, entry["mp4"]) if entry.get("mp4") else None
    return entries


def read_workflow_comments(repo, number):
    pages = gh_json(["api", "--paginate", "--slurp", f"repos/{repo}/issues/{number}/comments?per_page=100"])
    return [c["body"] or "" for page in pages for c in page if c["user"]["login"] == BOT and FOOTER in (c["body"] or "")]


def find_last_version(comments):
    versions = [int(v) for text in comments for v in HEADING.findall(text)]
    return max(versions, default=0)


def name_asset(pr, version, scenario, extension):
    """pr-<n>-v<version>-<GIF name><extension>; past 200 characters, its start plus a hash of the GIF name (a method and
    its screen). A run's .gif and .mp4 share their stem."""
    stem = f"pr-{pr}-v{version}-{os.path.splitext(os.path.basename(scenario['gif']))[0]}"
    if len(stem) + len(extension) <= MAX_NAME:
        return stem + extension
    digest = hashlib.sha256(scenario["gif"].encode("utf-8")).hexdigest()[:8]
    keep = MAX_NAME - len(f"-{digest}{extension}")
    return f"{stem[:keep].rstrip('-')}-{digest}{extension}"


def upload(args, scenarios):
    present = []
    for scenario in scenarios:
        if os.path.exists(scenario["file"]):
            present.append((scenario["file"], scenario["asset"]))
        else:
            print(f"No GIF for {scenario['title']} ({scenario['file']}).")
        if has_mp4(scenario):
            present.append((scenario["mp4_file"], scenario["mp4_asset"]))
        else:
            print(f"No MP4 for {scenario['title']} ({scenario['mp4_file'] or 'none recorded'}).")
    if not present:
        return
    with tempfile.TemporaryDirectory() as folder:
        files = []
        for source, asset in present:
            target = os.path.join(folder, asset)
            shutil.copyfile(source, target)
            files.append(target)
            print(f"Uploading {asset}")
        if not args.dry_run:
            gh(["release", "upload", RELEASE, *files, "--clobber", "--repo", args.repo])


def has_mp4(scenario):
    return scenario["mp4_file"] is not None and os.path.exists(scenario["mp4_file"])


def find_player(repo):
    """The site's player page: GitHub Pages serves <owner>/<repo> at https://<owner, lowercase>.github.io/<repo>/."""
    owner, name = repo.split("/", 1)
    return f"https://{owner.lower()}.github.io/{name}/e2e-player.html"


def group_by_scenario(runs):
    """The runs of each scenario (its class and method), in the order the scenarios first appear; desktop before mobile."""
    groups = {}
    for run in runs:
        groups.setdefault((run["class"], run["method"]), []).append(run)
    order = list(SCREENS)
    return [sorted(group, key=lambda run: order.index(run.get("screen")) if run.get("screen") in order else len(order))
            for group in groups.values()]


def write_section(repo, version, scenarios, changed):
    lines = [f"## Scenarios (V{version})", "", "**What changed**", "", changed, ""]
    for runs in group_by_scenario(scenarios):
        first = runs[0]
        mark = "❌ " if any(not run["passed"] for run in runs) else ""
        lines += [f"### {mark}{first['title']}", ""]
        if first.get("gherkin"):
            lines += ["```gherkin", first["gherkin"], "```", ""]
        for run in runs:
            screen = SCREENS.get(run.get("screen"), run.get("screen") or "")
            lines += [f"#### {'❌ ' if not run['passed'] else ''}{screen}".rstrip(), ""]
            if os.path.exists(run["file"]):
                url = f"https://github.com/{repo}/releases/download/{RELEASE}/{run['asset']}"
                image = f"![{first['title']} · {screen}]({url})"
                if has_mp4(run):
                    player = f"{find_player(repo)}?v={run['mp4_asset']}"
                    lines += [f"[{image}]({player})", "", f"[▶ Play MP4]({player})", ""]
                else:
                    lines += [image, ""]
            else:
                lines += ["_No recording._", ""]
    lines += ["---", FOOTER, ""]
    return "\n".join(lines)


def read_what_changed(out):
    """The text a session gave push-gifs.sh --message, as it is; empty when it gave none."""
    path = os.path.join(out, "what-changed.md")
    if not os.path.exists(path):
        return ""
    with open(path, encoding="utf-8") as file:
        return file.read().strip()


def read_commit_message(commit):
    return subprocess.run(["git", "log", "-1", "--format=%B", commit], check=True, capture_output=True, text=True).stdout


def describe_commit(message):
    """The message's body without its trailers (Co-Authored-By: …), or its subject when there is no body."""
    paragraphs = [p.strip("\n") for p in re.split(r"\n[ \t]*\n", message.strip()) if p.strip()]
    if not paragraphs:
        return "_No commit message._"
    subject, body = paragraphs[0], paragraphs[1:]
    if body and all(TRAILER.match(line) for line in body[-1].splitlines()):
        body = body[:-1]
    return "\n\n".join(body) if body else subject


def add_comment(args, section):
    print(section)
    if not args.dry_run:
        gh(["api", "-X", "POST", f"repos/{args.repo}/issues/{args.pr}/comments", "--input", "-"], {"body": section})
    print(f"Commented on #{args.pr}.")


def gh(arguments, payload=None):
    """Runs gh; on failure, stops with what gh said."""
    done = subprocess.run(["gh", *arguments], capture_output=True, text=True,
                          input=None if payload is None else json.dumps(payload))
    if done.returncode != 0:
        sys.exit(f"gh {' '.join(arguments)} failed:\n{done.stderr.strip()}")
    return done.stdout


def gh_json(arguments):
    return json.loads(gh(arguments))


if __name__ == "__main__":
    main()
