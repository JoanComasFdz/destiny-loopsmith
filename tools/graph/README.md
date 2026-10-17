# Dependency graph tool

Draws the rules' dependency graph of the Arc Hunter: which element needs what before it can fire (a trigger, a
weapon, a buff, a debuff, a pickup, a strike) and what it gives. Its columns put the elements in the order to test
them, left to right. Three scopes: the subclass alone, plus what it needs from outside (weapons), plus what builds on
it (mods, perks, exotics, artifact perks that need one of its nodes).

The tool is a host with no logic of its own: it reads `rules/`, calls `Orchestration.DependencyGraphs` in
`Loopsmith.Core` (the `DependencyGraphing` slice does the work), and writes two files:

- `out/graph/arc-hunter.json`: the graph (title, nodes, scopes with their edges and columns);
- `out/graph/arc-hunter.html`: the page template `Loopsmith.Graph/graph.html` with the JSON where it says `__DATA__`.

```bash
dotnet run --project tools/graph/Loopsmith.Graph                     # rules/ → out/graph/
dotnet run --project tools/graph/Loopsmith.Graph -- --rules <dir> --out <dir>
```

It exits with 1 and the error text when the rules don't parse, and with 2 on an unknown option. `out/` is gitignored.
