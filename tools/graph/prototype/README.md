# Lanes prototype (reference only)

The page the owner designed in session `#evaluate-tests-per-element-e2e`, kept as the reference the lanes tool must
reproduce. It is not part of the build: nothing reads this folder.

- `lanes.html`: the page. It is a copy of the graph tool's former `graph.html` that grew a lanes view; with the
  graph tabs hidden (`#tabs`, `#scroller`… are `display:none`), only the lanes show. It reads two placeholders:
  `__DATA__` (the graph tool's JSON: node names, kinds, slots, icons) and `__LANES__` (the lanes, from `lanes.py`).
- `lanes.py`: the throwaway script that turns `rules/**/*.yaml` into lanes: one lane per rule (one per trigger when a
  rule names "any ability"), one per passive.

## Rebuild the reference page

The page reads the dependency graph's JSON, which the tool wrote until it drew lanes itself. Rebuild it from the commit
that still writes it (`39de9f4`, on the branch `claude/evaluate-tests-per-element-e2e`), in a worktree of its own:

```bash
git worktree add ../loopsmith-prototype 39de9f4 && cd ../loopsmith-prototype
dotnet run --project tools/graph/Loopsmith.Graph             # writes out/graph/arc-hunter.json
python3 tools/graph/prototype/lanes.py out/graph/arc-hunter.json out/graph/lanes.json
python3 - <<'PY'
page = open('tools/graph/prototype/lanes.html').read()
page = page.replace('__DATA__', open('out/graph/arc-hunter.json').read())
page = page.replace('__LANES__', open('out/graph/lanes.json').read())
open('out/graph/lanes-prototype.html', 'w').write('<!doctype html><meta charset="utf-8">' + page)
PY
```

Open `out/graph/lanes-prototype.html` in a browser (the icons load from bungie.net), next to the tool's
`out/graph/arc-hunter-lanes.html`.
