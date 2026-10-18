# Rules prototype (reference only)

The page the owner designed in session `#evaluate-tests-per-element-e2e`, kept as the reference the rule viewer must
reproduce. It is not part of the build: nothing reads this folder.

- `rules.html`: the page. It is a copy of the graph tool's former `graph.html` that grew a view of the rules as flows
  (it calls them lanes, the word before the rename); with the graph tabs hidden (`#tabs`, `#scroller`… are
  `display:none`), only the flows show. It reads two placeholders: `__DATA__` (the graph tool's JSON: node names, kinds,
  slots, icons) and `__LANES__` (the flows, from `rules.py`).
- `rules.py`: the throwaway script that turns `rules/**/*.yaml` into flows: one flow per rule (one per trigger when a
  rule names "any ability"), one per passive.

Both were `lanes.html` and `lanes.py` until the rename, and still are at the commit the rebuild below starts from.

## Rebuild the reference page

The page reads the dependency graph's JSON, which the tool wrote until it drew the flows itself. Rebuild it from the commit
that still writes it (`39de9f4`, on the branch `claude/evaluate-tests-per-element-e2e`), in a worktree of its own:

```bash
git worktree add ../loopsmith-prototype 39de9f4 && cd ../loopsmith-prototype   # that commit still has tools/graph/
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
`out/rule-viewer/arc-hunter-rules.html` (in your own checkout).
