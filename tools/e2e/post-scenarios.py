#!/usr/bin/env python3
"""Posts a pull request's recorded end-to-end scenarios to it.

  post-scenarios.py --pr <n> --commit <sha> [--out <dir>] [--repo <owner/repo>] [--dry-run]
  post-scenarios.py --pr <n> --closed [--merged] [--repo <owner/repo>] [--dry-run]

Reads <out>/scenarios.json (tools/e2e/record.sh: one entry per run, a scenario played on a screen), uploads each run's
  GIF and MP4 to its release as pr-<n>-v<version>-<the run's GIF name>.gif and .mp4 and posts them as a
  new comment on the PR that starts with what changed. A run's release: a rule scenario's (its GIF under Rules/<…>/) is its
  rule group's, rules-<the folders under Rules/, kebab-cased, joined by dashes>-resources (Rules/Hunter/Arc/Melees →
  rules-hunter-arc-melees-resources, one per rule file: rules/hunter/arc/melees.yaml), created as a pre-release when it
  doesn't exist yet; any other run's is `prs-resources`. The post then shows <out>/what-changed.md as it is (push-gifs.sh --message), or else
  the body of --commit's message without its trailers, or its subject. Each scenario
  shows once: its title, the flow PNG of the rule its Gherkin names (record.sh's "flow", uploaded once per rule as
  pr-<n>-v<version>-<rule name>.png next to its GIFs, or a line saying why none was drawn), its Gherkin, then a "🖥️ Desktop" and/or "📱 Mobile" subtitle with that screen's GIF and, below
  it, "▶ Play MP4": both link to the site's player page (e2e-player.html?v=<the MP4's name>, and &r=<its release> for a
  rule release), since GitHub shows no
  player for a video it didn't store itself (a tap on a bare GIF would only download it). It never touches the PR's description. The version is one more than the highest
  "## Scenarios (V<N>)" in this workflow's comments. A name longer than 200 characters keeps its start and ends with a
  short hash of the run's GIF path (<domain>/<GIF name>.gif), the same for its GIF and its MP4.
The scenarios the PR added or changed come first; those only re-recorded because a fixture file they use changed
  (record.sh's "selected": "fixtures") follow under "### Re-recorded: their fixtures changed", each naming its changed
  fixtures. With only the first kind the post has no section headings.
Nothing recorded (an empty scenarios.json) posts nothing.
Once the new post is up, every earlier post still open is collapsed into a closed <details> block, unchanged inside, whose
  summary reads "Scenarios (V1) · 12 scenarios · replaced by V2"; a post already collapsed or shrunk is left as it is.
  Every earlier post not shrunk is then hidden as Outdated (GraphQL's minimizeComment, classifier OUTDATED: "This comment
  was marked as outdated", with "Show comment"), so the timeline doesn't show it open while GitHub hasn't redrawn its
  collapsed body (PR #155). A post already hidden is left as it is; one collapsed earlier but never hidden (before this,
  or its hide failed) is hidden now. A hide GitHub refuses, or a failed read of which posts are hidden, is a warning,
  never a failure: the post is collapsed all the same.
--closed (the workflow, when the PR closes) cleans the releases, then shrinks every post but the newest to that line plus
  "· recordings deleted when the PR closed". In `prs-resources` it keeps the PR's newest version and deletes its older
  ones. In each rule release it keeps the latest recording of every rule: with --merged (the PR was merged), for each run
  of the PR the PR's newest recording of it, deleting its older ones and those of other PRs no longer open; without it
  (closed without merging), it deletes all the PR's recordings there, which never became the rules' behaviour, and then
  shrinks the newest post too. Only names pr-<n>-v<version>-… are ever deleted. Each release's asset count is logged, with
  a warning above 900 of its 1,000. A post GitHub refuses to edit (a body past 65,536 characters) is a warning, never a
  failure.

Uses the gh CLI (GH_TOKEN) on the repository in --repo or GITHUB_REPOSITORY. --dry-run prints instead of writing.
.github/workflows/e2e-videos.yml runs it on the GIFs a session pushed with tools/e2e/push-gifs.sh (--out e2e-gifs,
--commit the GIFs' commit), and --closed when the PR closes. docs/hosting.md, "E2E videos on pull requests", explains it.
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
RULES_FOLDER = "Rules"
RULE_RELEASE = re.compile(r"^rules-[a-z0-9]+(?:-[a-z0-9]+)*-resources$")
ASSET = re.compile(r"^pr-(\d+)-v(\d+)-(.+)$")
HASHED = re.compile(r"-([0-9a-f]{8})(\.[a-z0-9]+)$")
MAX_ASSETS = 1000
WARN_ASSETS = 900
FOOTER = "_Recorded by the e2e workflow · Claude Code_"
BOT = "github-actions[bot]"
MAX_NAME = 200
HEADING = re.compile(r"^## Scenarios \(V(\d+)\)[ \t]*$", re.MULTILINE)
# A shrunk post has no heading left: its version is in its one line.
SHRUNK = re.compile(r"^\*\*Scenarios \(V(\d+)\)\*\*")
COLLAPSED = "<details>"
SECTIONS = re.compile(r"^### (Added or changed by this PR|Re-recorded: their fixtures changed)[ \t]*$", re.MULTILINE)
TRAILER = re.compile(r"^[A-Za-z][A-Za-z0-9-]*: \S")
SCREENS = {"desktop": "🖥️ Desktop", "mobile": "📱 Mobile"}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pr", type=int, required=True)
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY"))
    parser.add_argument("--commit", help="the commit whose message says what changed")
    parser.add_argument("--out", default="out", help="record.sh's output folder")
    parser.add_argument("--closed", action="store_true",
                        help="the PR closed: clean the releases, shrink every post but the newest to one line")
    parser.add_argument("--merged", action="store_true", help="with --closed: the PR was merged")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if not args.repo:
        sys.exit("No repository: pass --repo or set GITHUB_REPOSITORY.")
    if args.closed:
        unmerged_rules = clean(args)
        shrink(args, unmerged_rules)
        return
    if not args.commit:
        sys.exit("No commit: pass --commit (or --closed).")
    post(args)


def post(args):
    scenarios = read_scenarios(args.out)
    if not scenarios:
        print("No scenario recorded: nothing to post.")
        return
    comments = read_workflow_comments(args.repo, args.pr)
    version = find_last_version(comments) + 1
    for scenario in scenarios:
        scenario["release"] = find_release(scenario["gif"])
        scenario["asset"] = name_asset(args.pr, version, scenario, ".gif")
        scenario["mp4_asset"] = name_asset(args.pr, version, scenario, ".mp4")
        scenario["flow_asset"] = name_flow(args.pr, version, scenario)
    upload(args, scenarios)
    changed = read_what_changed(args.out) or describe_commit(read_commit_message(args.commit))
    add_comment(args, write_section(args.repo, version, scenarios, changed))
    for comment, body in plan_collapses(comments, version):
        edit_comment(args, comment, body)
    hidden = read_hidden(args, comments)
    if hidden is None:
        return
    for comment in plan_hides(comments, version, hidden):
        hide_comment(args, comment)


def shrink(args, newest_too=False):
    """The PR closed: every post but the newest becomes its one line, its recordings deleted with its version; the newest
    too when its rule recordings were deleted (closed without merging)."""
    comments = read_workflow_comments(args.repo, args.pr)
    edits = plan_shrinks(comments, newest_too)
    if not edits:
        print(f"No earlier post on #{args.pr} to shrink.")
    for comment, body in edits:
        edit_comment(args, comment, body)


def read_scenarios(out):
    path = os.path.join(out, "scenarios.json")
    if not os.path.exists(path):
        sys.exit(f"{path} is missing: record.sh didn't get as far as recording (see its output above).")
    with open(path, encoding="utf-8") as file:
        entries = json.load(file)
    for entry in entries:
        entry["file"] = os.path.join(out, entry["gif"])
        entry["mp4_file"] = os.path.join(out, entry["mp4"]) if entry.get("mp4") else None
        entry["flow_file"] = os.path.join(out, entry["flow"]) if entry.get("flow") else None
    return entries


def read_workflow_comments(repo, number):
    pages = gh_json(["api", "--paginate", "--slurp", f"repos/{repo}/issues/{number}/comments?per_page=100"])
    return [{"id": c["id"], "node_id": c["node_id"], "body": c["body"] or ""} for page in pages for c in page
            if c["user"]["login"] == BOT and FOOTER in (c["body"] or "")]


def find_last_version(comments):
    return max((v for v in map(read_version, comments) if v is not None), default=0)


def read_version(comment):
    """The post's version: its "## Scenarios (V<N>)" heading, open or collapsed, or its shrunk line; None for another
    comment of the workflow."""
    found = HEADING.search(comment["body"]) or SHRUNK.match(comment["body"])
    return int(found.group(1)) if found else None


def is_shrunk(comment):
    return SHRUNK.match(comment["body"]) is not None


def is_collapsed(comment):
    return comment["body"].startswith(COLLAPSED)


def count_scenarios(body):
    """A scenario's title is ### in a post without sections, #### under "Added or changed…" / "Re-recorded…" (its
    screens one level down, so never counted)."""
    level = "####" if SECTIONS.search(body) else "###"
    return len(re.findall(rf"^{level} ", body, re.MULTILINE))


def describe_version(version, count, replaced_by):
    return f"Scenarios (V{version}) · {count} scenario{'' if count == 1 else 's'} · replaced by V{replaced_by}"


def find_next_version(version, versions):
    return min(v for v in versions if v > version)


def plan_collapses(comments, newest):
    """(comment, new body) for each earlier post still open: a closed <details> holding it as it was."""
    versions = [v for v in map(read_version, comments) if v is not None] + [newest]
    edits = []
    for comment in comments:
        version = read_version(comment)
        if version is None or version >= newest or is_collapsed(comment) or is_shrunk(comment):
            continue
        summary = describe_version(version, count_scenarios(comment["body"]), find_next_version(version, versions))
        summary = summary.replace(f"Scenarios (V{version})", f"<b>Scenarios (V{version})</b>", 1)
        edits.append((comment, f"{COLLAPSED}<summary>{summary}</summary>\n\n{comment['body'].rstrip()}\n\n</details>\n"))
    return edits


def plan_hides(comments, newest, hidden):
    """Each earlier post, open or collapsed, not hidden yet (its node id not in hidden) and not shrunk: to hide as
    Outdated."""
    return [comment for comment in comments
            if (version := read_version(comment)) is not None and version < newest
            and not is_shrunk(comment) and comment["node_id"] not in hidden]


def plan_shrinks(comments, newest_too=False):
    """(comment, new body) for each post but the newest not shrunk yet, open or collapsed: its one line, with the footer
    kept out of sight so the post still reads as the workflow's (read_workflow_comments). With newest_too (the PR closed
    without merging, its rule recordings deleted) the newest post too."""
    versions = [v for v in map(read_version, comments) if v is not None]
    newest = max(versions, default=0)
    edits = []
    for comment in comments:
        version = read_version(comment)
        if version is None or (version >= newest and not newest_too) or is_shrunk(comment):
            continue
        count = count_scenarios(comment["body"])
        if version >= newest:
            line = (f"**Scenarios (V{version})** · {count} scenario{'' if count == 1 else 's'}"
                    " · recordings deleted when the PR closed without merging")
        else:
            line = describe_version(version, count, find_next_version(version, versions))
            line = line.replace(f"Scenarios (V{version})", f"**Scenarios (V{version})**", 1)
            line += " · recordings deleted when the PR closed"
        edits.append((comment, f"{line}\n\n<!-- {FOOTER} -->\n"))
    return edits


def to_kebab_case(name):
    """"AllClasses" → "all-classes": Humanizer's Kebaberize, as ScenarioNaming.ToGifName and the extension's toKebabCase."""
    name = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", name)
    name = re.sub(r"([a-z\d])([A-Z])", r"\1_\2", name)
    return re.sub(r"[-\s]", "_", name).lower().replace("_", "-")


def find_release(gif):
    """A run's release, from its GIF's path (<domain folder>/<GIF name>.gif): a rule scenario's (Rules/Hunter/Arc/Melees/…)
    is rules-hunter-arc-melees-resources, any other prs-resources."""
    folders = gif.split("/")[:-1]
    if len(folders) < 2 or folders[0] != RULES_FOLDER:
        return RELEASE
    return f"rules-{'-'.join(to_kebab_case(folder) for folder in folders[1:])}-resources"


def is_rule_release(tag):
    return RULE_RELEASE.match(tag) is not None


def read_asset(name):
    """(pr, version, run) of a name pr-<n>-v<version>-<run>, the run with its extension; None for any other name. A name
    shortened to 200 characters (name_asset) keeps less of its run the longer its PR number and version are, so its run
    is its hash and extension, the same for every PR and version."""
    found = ASSET.match(name)
    if not found:
        return None
    hashed = HASHED.search(name) if len(name) == MAX_NAME else None
    run = f"#{hashed.group(1)}{hashed.group(2)}" if hashed else found.group(3)
    return int(found.group(1)), int(found.group(2)), run


def plan_pr_deletions(assets, pr):
    """The PR's assets of prs-resources to delete when it closes: every version older than its newest. assets are
    {"id", "name"}; a name not of the form pr-<n>-v<version>-… is never deleted."""
    own = [(asset, read_asset(asset["name"])) for asset in assets]
    own = [(asset, parsed) for asset, parsed in own if parsed is not None and parsed[0] == pr]
    newest = max((parsed[1] for _, parsed in own), default=0)
    return [asset for asset, parsed in own if parsed[1] < newest]


def plan_rule_deletions(assets, pr, merged, open_prs):
    """The assets of a rule release to delete when the PR closes, so each rule keeps its latest recording. Merged: for
    each run the PR recorded, its newest recording stays; its older ones go, and so do other PRs' recordings of that run,
    unless that PR is still open (its posts need them; the one merged last wins). Closed without merging: all of the
    PR's go. A name not of the form pr-<n>-v<version>-… is never deleted."""
    parsed = [(asset, read_asset(asset["name"])) for asset in assets]
    parsed = [(asset, found) for asset, found in parsed if found is not None]
    own = [(asset, found) for asset, found in parsed if found[0] == pr]
    if not merged:
        return [asset for asset, _ in own]
    newest = {}
    for _, (_, version, run) in own:
        newest[run] = max(newest.get(run, 0), version)
    return [asset for asset, (owner, version, run) in parsed
            if run in newest and ((owner == pr and version < newest[run]) or (owner != pr and owner not in open_prs))]


def clean(args):
    """The PR closed: deletes what plan_pr_deletions and plan_rule_deletions say in each release, logging each name and
    each release's count. True when it deleted rule recordings of a PR closed without merging (its newest post shrinks)."""
    releases = gh_json(["api", "--paginate", "--slurp", f"repos/{args.repo}/releases?per_page=100"])
    releases = [release for page in releases for release in page
                if release["tag_name"] == RELEASE or is_rule_release(release["tag_name"])]
    open_prs = set() if not args.merged else {
        pull["number"] for page in gh_json(["api", "--paginate", "--slurp", f"repos/{args.repo}/pulls?state=open&per_page=100"])
        for pull in page}
    unmerged_rules = False
    for release in sorted(releases, key=lambda release: release["tag_name"] != RELEASE):
        tag = release["tag_name"]
        pages = gh_json(["api", "--paginate", "--slurp", f"repos/{args.repo}/releases/{release['id']}/assets?per_page=100"])
        assets = [{"id": asset["id"], "name": asset["name"]} for page in pages for asset in page]
        if tag == RELEASE:
            deleted = plan_pr_deletions(assets, args.pr)
        else:
            deleted = plan_rule_deletions(assets, args.pr, args.merged, open_prs)
            unmerged_rules = unmerged_rules or (not args.merged and bool(deleted))
        for asset in deleted:
            print(f"{tag}: deleting {asset['name']}")
            if not args.dry_run:
                gh(["api", "-X", "DELETE", f"repos/{args.repo}/releases/assets/{asset['id']}"])
        left = len(assets) - len(deleted)
        kept = sum(1 for asset in assets if asset not in deleted and (read_asset(asset["name"]) or (0,))[0] == args.pr)
        print(f"{tag}: kept {kept} asset(s) of PR #{args.pr}; it holds {left} of at most {MAX_ASSETS:,} assets.")
        if left > WARN_ASSETS:
            print(f"::warning::{tag} holds {left} of at most {MAX_ASSETS:,} assets (docs/hosting.md, \"The releases' limit\").")
    if not any(release["tag_name"] == RELEASE for release in releases):
        print(f"There is no {RELEASE} release: nothing to clean there.")
    return unmerged_rules


def name_asset(pr, version, scenario, extension):
    """pr-<n>-v<version>-<GIF name><extension>; past 200 characters, its start plus a hash of the GIF's path (<domain>/<GIF name>.gif: a method and
    its screen). A run's .gif and .mp4 share their stem."""
    stem = f"pr-{pr}-v{version}-{os.path.splitext(os.path.basename(scenario['gif']))[0]}"
    if len(stem) + len(extension) <= MAX_NAME:
        return stem + extension
    digest = hashlib.sha256(scenario["gif"].encode("utf-8")).hexdigest()[:8]
    keep = MAX_NAME - len(f"-{digest}{extension}")
    return f"{stem[:keep].rstrip('-')}-{digest}{extension}"


def name_flow(pr, version, scenario):
    """pr-<n>-v<version>-<rule name>.png, the flow of the rule a scenario names; None without one. A rule's scenarios and
    screens share it, and the cleanup keeps each rule's newest like a run's."""
    return f"pr-{pr}-v{version}-{os.path.basename(scenario['flow'])}" if scenario.get("flow") else None


def upload(args, scenarios):
    """Each run's GIF and MP4, and each rule's flow once, to its release, one upload per release, a rule release created
    first when it is missing."""
    present = {}
    for scenario in scenarios:
        files = present.setdefault(scenario["release"], [])
        if has_flow(scenario) and (scenario["flow_file"], scenario["flow_asset"]) not in files:
            files.append((scenario["flow_file"], scenario["flow_asset"]))
        if os.path.exists(scenario["file"]):
            files.append((scenario["file"], scenario["asset"]))
        else:
            print(f"No GIF for {scenario['title']} ({scenario['file']}).")
        if has_mp4(scenario):
            files.append((scenario["mp4_file"], scenario["mp4_asset"]))
        else:
            print(f"No MP4 for {scenario['title']} ({scenario['mp4_file'] or 'none recorded'}).")
    for release, files in present.items():
        if not files:
            continue
        if is_rule_release(release):
            ensure_release(args, release, next(s["gif"] for s in scenarios if s["release"] == release))
        with tempfile.TemporaryDirectory() as folder:
            targets = []
            for source, asset in files:
                target = os.path.join(folder, asset)
                shutil.copyfile(source, target)
                targets.append(target)
                print(f"Uploading {asset} to {release}")
            if not args.dry_run:
                gh(["release", "upload", release, *targets, "--clobber", "--repo", args.repo])


def ensure_release(args, release, gif):
    """Creates a rule release that doesn't exist yet, as a pre-release; gif is one of its runs' GIF path, which names its
    rule file. Another PR's run may create it at the same time: a creation GitHub refuses is fine when the release is
    there after it."""
    if run_gh(["release", "view", release, "--repo", args.repo, "--json", "tagName"]).returncode == 0:
        return
    print(f"Creating the pre-release {release}")
    if args.dry_run:
        return
    rule_file = "/".join(to_kebab_case(folder) for folder in gif.split("/")[1:-1])
    notes = (f"The latest e2e recordings (GIFs and MP4s) and flows (PNGs) of the rules of rules/{rule_file}.yaml, "
             "kept by the E2E videos workflow (docs/hosting.md, \"E2E videos on pull requests\"). Created by it: don't edit.")
    made = run_gh(["release", "create", release, "--prerelease", "--title", release, "--notes", notes, "--repo", args.repo])
    if made.returncode != 0 and run_gh(["release", "view", release, "--repo", args.repo, "--json", "tagName"]).returncode != 0:
        sys.exit(f"Couldn't create the release {release}:\n{made.stderr.strip()}")


def has_mp4(scenario):
    return scenario["mp4_file"] is not None and os.path.exists(scenario["mp4_file"])


def has_flow(scenario):
    return scenario.get("flow_file") is not None and os.path.exists(scenario["flow_file"])


def find_player(repo):
    """The site's player page: GitHub Pages serves <owner>/<repo> at https://<owner, lowercase>.github.io/<repo>/."""
    owner, name = repo.split("/", 1)
    return f"https://{owner.lower()}.github.io/{name}/e2e-player.html"


def link_player(repo, mp4_asset, release):
    """The player page on an MP4: ?v=<its name>, and &r=<its release> when that isn't prs-resources (the page's default)."""
    return f"{find_player(repo)}?v={mp4_asset}" + ("" if release == RELEASE else f"&r={release}")


def group_by_scenario(runs):
    """The runs of each scenario (its class and method), in the order the scenarios first appear; desktop before mobile."""
    groups = {}
    for run in runs:
        groups.setdefault((run["class"], run["method"]), []).append(run)
    order = list(SCREENS)
    return [sorted(group, key=lambda run: order.index(run.get("screen")) if run.get("screen") in order else len(order))
            for group in groups.values()]


def write_section(repo, version, scenarios, changed):
    """The post. The scenarios the PR added or changed come first, then those only re-recorded because a fixture file they
    use changed ("selected": "fixtures"), each naming its changed fixtures. With only the first kind (a scenarios.json
    without "selected" included) there are no section headings, and a scenario's title is ### and its screens ####."""
    lines = [f"## Scenarios (V{version})", "", "**What changed**", "", changed, ""]
    groups = group_by_scenario(scenarios)
    own = [runs for runs in groups if not is_rerecorded(runs[0])]
    rerecorded = [runs for runs in groups if is_rerecorded(runs[0])]
    if not rerecorded:
        for runs in own:
            lines += write_scenario(repo, runs, "###")
    else:
        if own:
            lines += ["### Added or changed by this PR", ""]
            for runs in own:
                lines += write_scenario(repo, runs, "####")
        lines += ["### Re-recorded: their fixtures changed", "",
                  "Their code didn't change, a fixture file they use did: they should behave as before.", ""]
        for runs in rerecorded:
            fixtures = ", ".join(f"`{path}`" for path in runs[0].get("fixtures") or [])
            lines += write_scenario(repo, runs, "####", f"_{fixtures} changed._" if fixtures else "")
    lines += ["---", FOOTER, ""]
    return "\n".join(lines)


def is_rerecorded(scenario):
    return scenario.get("selected") == "fixtures"


def write_scenario(repo, runs, level, why=""):
    """A scenario's title at <level>, why it is there, the flow of the rule it names (or why there is none), its Gherkin,
    then each screen one level down with its GIF."""
    first = runs[0]
    mark = "❌ " if any(not run["passed"] for run in runs) else ""
    lines = [f"{level} {mark}{first['title']}", ""]
    if why:
        lines += [why, ""]
    lines += write_flow(repo, first)
    if first.get("gherkin"):
        lines += ["```gherkin", first["gherkin"], "```", ""]
    for run in runs:
        screen = SCREENS.get(run.get("screen"), run.get("screen") or "")
        lines += [f"{level}# {'❌ ' if not run['passed'] else ''}{screen}".rstrip(), ""]
        if os.path.exists(run["file"]):
            release = run.get("release", RELEASE)
            url = f"https://github.com/{repo}/releases/download/{release}/{run['asset']}"
            image = f"![{first['title']} · {screen}]({url})"
            if has_mp4(run):
                player = link_player(repo, run["mp4_asset"], release)
                lines += [f"[{image}]({player})", "", f"[▶ Play MP4]({player})", ""]
            else:
                lines += [image, ""]
        else:
            lines += ["_No recording._", ""]
    return lines


def write_flow(repo, scenario):
    """The flow PNG of the rule a scenario's Gherkin names, from its release; a line saying why when the rule viewer
    couldn't draw it; nothing for a scenario that names no rule."""
    if not scenario.get("rule"):
        return []
    if has_flow(scenario):
        url = f"https://github.com/{repo}/releases/download/{scenario.get('release', RELEASE)}/{scenario['flow_asset']}"
        return [f"![Flow of {scenario['rule']}]({url})", ""]
    return [f"_No flow drawn for `{scenario['rule']}`: {scenario.get('flowError') or 'not drawn'}_", ""]


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


def edit_comment(args, comment, body):
    """Replaces a post's body; a refusal (a body past GitHub's 65,536 characters) is a warning: the new post is up."""
    print(f"Editing comment {comment['id']}: {body.splitlines()[0]}")
    if args.dry_run:
        return
    done = run_gh(["api", "-X", "PATCH", f"repos/{args.repo}/issues/comments/{comment['id']}", "--input", "-"],
                  {"body": body})
    if done.returncode != 0:
        print(f"::warning::Couldn't edit comment {comment['id']} on #{args.pr}: {done.stderr.strip()}")


def read_hidden(args, comments):
    """The node ids of the workflow's posts already hidden (GraphQL's isMinimized: REST doesn't say); None, with a
    warning, when GitHub refuses the read, so nothing is hidden this time."""
    ids = [comment["node_id"] for comment in comments]
    if not ids:
        return set()
    query = "query($ids: [ID!]!) { nodes(ids: $ids) { ... on IssueComment { id isMinimized } } }"
    done = run_gh(["api", "graphql", "--input", "-"], {"query": query, "variables": {"ids": ids}})
    if done.returncode != 0:
        print(f"::warning::Couldn't read which posts on #{args.pr} are hidden, so none is hidden: {done.stderr.strip()}")
        return None
    nodes = json.loads(done.stdout)["data"]["nodes"]
    return {node["id"] for node in nodes if node and node.get("isMinimized")}


def hide_comment(args, comment):
    """Hides a post as Outdated; a refusal is a warning: the post is collapsed all the same."""
    print(f"Hiding comment {comment['id']} as outdated: {comment['body'].splitlines()[0]}")
    if args.dry_run:
        return
    mutation = ("mutation($id: ID!) { minimizeComment(input: {subjectId: $id, classifier: OUTDATED}) "
                "{ minimizedComment { isMinimized } } }")
    done = run_gh(["api", "graphql", "--input", "-"], {"query": mutation, "variables": {"id": comment["node_id"]}})
    if done.returncode != 0:
        print(f"::warning::Couldn't hide comment {comment['id']} on #{args.pr} as outdated: {done.stderr.strip()}")


def run_gh(arguments, payload=None):
    return subprocess.run(["gh", *arguments], capture_output=True, text=True,
                          input=None if payload is None else json.dumps(payload))


def gh(arguments, payload=None):
    """Runs gh; on failure, stops with what gh said."""
    done = run_gh(arguments, payload)
    if done.returncode != 0:
        sys.exit(f"gh {' '.join(arguments)} failed:\n{done.stderr.strip()}")
    return done.stdout


def gh_json(arguments):
    return json.loads(gh(arguments))


if __name__ == "__main__":
    main()
