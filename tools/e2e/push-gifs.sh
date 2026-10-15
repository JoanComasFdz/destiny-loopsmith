#!/usr/bin/env bash
# Pushes the GIFs and MP4s record.sh made to GitHub, for .github/workflows/e2e-videos.yml to post them on the pull request.
#   tools/e2e/push-gifs.sh [<PR number>] [--out <dir>] [--message <what changed>] [--dry-run]
# A session can't upload a file to GitHub, but it can push: this pushes e2e-gifs/pr-<n>-<short sha>, the branch's head
# (already pushed) plus one commit that adds <out>/scenarios.json and its GIFs and MP4s under e2e-gifs/. The workflow
# uploads them to the prs-resources release, posts them on the PR as "## Scenarios (V<N>)" (each GIF with a link to its
# MP4 in the site's player page) and deletes that branch.
# The PR is the open one of the current branch unless given. "What changed" in the post is --message, or else the body of
# the head commit's message (its subject when it has none). An empty scenarios.json pushes nothing. Never touches the
# working tree or the index. --dry-run builds the commit and says what it would push.
# docs/hosting.md, "E2E videos on pull requests".
set -euo pipefail

repo_dir="$(cd "$(dirname "$0")/../.." && pwd)"
out="$repo_dir/out"
pr=""
message=""
dry_run=0

while [ $# -gt 0 ]; do
  case "$1" in
    --out) out="$2"; shift 2 ;;
    --message) message="$2"; shift 2 ;;
    --dry-run) dry_run=1; shift ;;
    -h|--help) sed -n '2,11p' "$0"; exit 0 ;;
    *) pr="$1"; shift ;;
  esac
done
cd "$repo_dir"

scenarios="$out/scenarios.json"
if [ ! -f "$scenarios" ]; then
  echo "$scenarios is missing: run tools/e2e/record.sh first." >&2
  exit 2
fi
# Each run's GIF, then its MP4 (a scenarios.json from before the MP4s has none).
files="$(python3 -c 'import json,sys; [print(e[k]) for e in json.load(open(sys.argv[1])) for k in ("gif", "mp4") if e.get(k)]' "$scenarios")"
if [ -z "$files" ]; then
  echo "No scenario recorded ($scenarios is empty): nothing to push."
  exit 0
fi

# owner/repo from origin's URL (github.com/<owner>/<repo>, or a proxy's …/<owner>/<repo>).
slug="$(git remote get-url origin | sed -E 's#\.git$##; s#/+$##' | awk -F'[/:]' '{print $(NF-1) "/" $NF}')"
branch="$(git rev-parse --abbrev-ref HEAD)"
head="$(git rev-parse HEAD)"

# The GIFs' commit must sit on a commit of the PR, so the branch's head has to be on GitHub already.
if ! git fetch -q --no-tags origin "$branch" 2> /dev/null; then
  echo "$branch isn't on GitHub: push it and open its pull request first." >&2
  exit 1
fi
if [ "$(git rev-parse FETCH_HEAD)" != "$head" ]; then
  echo "origin/$branch is at $(git rev-parse --short FETCH_HEAD), this branch at $(git rev-parse --short HEAD):" \
       "push the branch first." >&2
  exit 1
fi

if [ -z "$pr" ]; then
  pr="$(gh api "repos/$slug/pulls?head=${slug%%/*}:$branch&state=open" --jq '.[0].number // empty')"
  if [ -z "$pr" ]; then
    echo "No open pull request for $branch: open it first, or pass its number." >&2
    exit 1
  fi
else
  found="$(gh api "repos/$slug/pulls/$pr" --jq '"\(.state) \(.head.ref)"')"
  read -r state head_ref <<< "$found"
  if [ "$state" != open ] || [ "$head_ref" != "$branch" ]; then
    echo "PR #$pr is $state, on $head_ref: not an open pull request of $branch." >&2
    exit 1
  fi
fi

target="e2e-gifs/pr-$pr-$(git rev-parse --short=7 HEAD)"
if git ls-remote --exit-code -q origin "refs/heads/$target" > /dev/null; then
  echo "$target is already on GitHub: its workflow run deletes it when it's done (Actions, \"E2E videos\")." >&2
  exit 1
fi

# The commit: the head's tree plus e2e-gifs/, built in a separate index so the working tree and the index stay as they are.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export GIT_INDEX_FILE="$work/index"
git read-tree HEAD
add_file() {
  local blob
  blob="$(git hash-object -w "$1")"
  git update-index --add --cacheinfo "100644,$blob,e2e-gifs/$2"
}
add_file "$scenarios" scenarios.json
gifs=0
mp4s=0
while IFS= read -r file; do
  if [ -f "$out/$file" ]; then
    add_file "$out/$file" "$file"
    case "$file" in *.mp4) mp4s=$((mp4s + 1)) ;; *) gifs=$((gifs + 1)) ;; esac
  elif [[ $file == *.mp4 ]]; then
    echo "No MP4 at $out/$file: its GIF has no player link."
  else
    echo "No GIF at $out/$file: its post says \"No recording\"."
  fi
done <<< "$files"
tree="$(git write-tree)"
unset GIT_INDEX_FILE

if [ -n "$message" ]; then
  printf 'E2E GIFs of #%s at %s\n\n%s\n' "$pr" "$(git rev-parse --short=7 HEAD)" "$message" > "$work/message"
else
  git log -1 --format=%B HEAD > "$work/message"
fi
commit="$(git commit-tree "$tree" -p HEAD -F "$work/message")"

echo "$gifs GIF(s) and $mp4s MP4(s) of PR #$pr in $(git rev-parse --short "$commit") on $head."
if [ $dry_run -eq 1 ]; then
  git diff --stat "$head" "$commit"
  echo "Dry run: would push it to $target."
  exit 0
fi
git push -q origin "$commit:refs/heads/$target"
echo "Pushed $target: the \"E2E videos\" workflow posts them on https://github.com/$slug/pull/$pr and deletes the branch."
echo "Its runs: https://github.com/$slug/actions/workflows/e2e-videos.yml"
