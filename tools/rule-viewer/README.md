# Rule viewer

Draws every rule of the Arc Hunter as one lane, read left to right: what sets it off ➜ what must be in place ➜ what it
gives. One lane is one claim of the rules, so the page is the list of the atomic e2e scenarios to write: one lane, one
scenario ([ADRs D39](../../docs/ADRs.md)).

- **Lanes.** One per rule and one per passive ("Always"). A rule whose trigger names several (`via: ability`) is one
  lane per ability that deals damage: Grenade, Melee, Sliding melee, Super, never a dodge (a dodge deals no damage). A
  rule on any melee (`via: melee`) is a Melee lane and a Sliding melee lane; `melee:press` is Melee only. The rules file
  keeps one rule; only the lanes split it.
- **What must be in place.** The element, "equipped" (not for a keyword: Jolt, Bolt Charge, Amplified, Ionic Trace, Orb
  of Power, Super and Guardian are in every build), then each state the rule needs, with its caption ("on the target",
  "on you", "at max stacks", "3+ stacks"). A lane needing nothing beyond its trigger says "always produces".
- **What it gives.** Strikes, other effects (energy, health, resistance…), buffs, debuffs, pickups, summons; then what
  it ends (dashed), collects or adds stacks to. Amounts as the rules author them ("?", "100%", "8.5s", "×2").
- **Groups and scopes.** Lanes are grouped by what sets them off: Dodge, Air dodge, Melee, Sliding melee, Grenade,
  Finisher, Super, the weapons, Critical health, every other trigger by name, "Always on" last. Three chips pick the
  scope: the Arc subclass, + what it needs from outside (weapons), + what builds on it (mods, perks, exotics, artifact
  perks that react to something in it).

The tool is three projects ([ADRs D39](../../docs/ADRs.md)):

- `Loopsmith.RuleViewer.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md) like a Core
  slice. It draws the dependency graph, its scopes and its lanes (`RuleViewing.DrawLanes`), and uses of `Loopsmith.Core`
  only its kernel and `Orchestration` (`CatalogLoading.ParseCatalog` parses the rules).
- `Loopsmith.RuleViewer`: the host. It reads `rules/`, calls `RuleViewing` and writes two files.
- `Loopsmith.RuleViewer.Tests`: the unit and golden tests, and the architecture test that keeps the pure project pure
  (no files, network or console; no part of Core but its kernel and `Orchestration`). Core's host boundary test checks
  the host's and the pure project's calls into Core.

The files:

- `out/rule-viewer/arc-hunter-lanes.json`: the lanes (scopes, groups, lanes, boxes with their icons);
- `out/rule-viewer/arc-hunter-lanes.html`: the page, built by the components in `Loopsmith.RuleViewer/Page/` (`LanePage`,
  `LaneGroupView`, `LaneRowView`, `LaneBoxView`, `LaneIconView`) with the style and script in `Page/lanes.css` and
  `Page/lanes.js`. The script draws each lane's wires once its boxes have landed, and again on resize and theme change.

```bash
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer               # rules/ → out/rule-viewer/
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rules <dir> --out <dir>
dotnet test tools/rule-viewer/Loopsmith.RuleViewer.Tests                  # also part of `dotnet test`
```

It exits with 1 and the error text when the rules don't parse, and with 2 on an unknown option. `out/` is gitignored.
The icons load from bungie.net. `ArcHunterLanesGoldenTests` keeps the lanes as text, so a rules change shows its lanes
in the PR diff.

`prototype/` is the page the owner designed, kept as the reference this tool reproduces (its README rebuilds it).
