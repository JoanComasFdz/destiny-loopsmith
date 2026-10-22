# Rule viewer

Draws every rule in `rules/` as one flow per claim, read left to right: what sets it off ➜ what must be in place ➜ what it
gives. One flow is one claim of the rules, so the page is the list of the atomic e2e scenarios to write: one flow, one
scenario ([ADRs D39](../../docs/ADRs.md)).

- **Flows.** One per rule and one per passive ("Always"). A rule whose trigger names several (`via: ability`) is one
  flow per ability that deals damage: Grenade, Melee, Sliding melee, Super, never the class ability (it deals no damage).
  A rule on any melee (`via: melee`) is a Melee flow and a Sliding melee flow; `melee:press` is Melee only. The rules
  file keeps one rule; only the flows split it.
- **The rule's name.** Above each flow, the rule's full name: its file's path and its id
  (`hunter-arc-melees-combination-blow-2`, [ADRs D40](../../docs/ADRs.md)), the same on every flow of a split rule, and
  what its e2e scenario and the scenario's recordings are named by. The JSON gives it as each flow's `rule`.
- **What must be in place.** The element, "equipped" (not for a keyword: Jolt, Bolt Charge, Amplified, Ionic Trace, Orb
  of Power, Super and Guardian are in every build), then each state the rule needs, with its caption ("on the target",
  "on you", "at max stacks", "3+ stacks"). A flow needing nothing beyond its trigger says "always produces".
- **What it gives.** Strikes, other effects (energy, health, resistance…), buffs, debuffs, pickups, summons; then what
  it ends (dashed), collects or adds stacks to. Amounts as the rules author them ("?", "100%", "8.5s", "×2").
- **The class ability.** Its trigger is "Class ability" ("Air class ability" in the air) for every class, with the
  class's own word as its caption (Hunter: Dodge, Titan: Barricade, Warlock: Rift) and the icon of the class's first
  class ability in the rules, by id (the Hunter's: Gambler's Dodge); the Class stat's icon when the rules have none.
- **As written.** Each rule is drawn as its own element: its class (none for a rule of every class, whose class ability
  reads "Class ability", with no class move) and its affinity's subclass (Prismatic for a neutral or kinetic one). What
  a rule needs from the build stays as written: Harmonic Siphon's trigger is "Weapon of your Super's element 2+ kills",
  never "Arc weapon …". A weapon perk's enhanced trait is an element of its own, right after its perk, with its perk's
  rules and their names.
- **The tree.** Beside the flows, `rules/` as a folder explorer: folders, then files, each by name; in a file, its
  elements in the file's order; in an element, its rules then its passives (a rule shows its id and what sets off each of
  its flows). Every item has a checkbox (ticked at first) that shows or hides its flows and ticks or unticks everything
  under it (a dash when some are ticked), and every item but a rule an arrow that folds it (folders and files start
  open, elements folded). Clicking a name selects the item and brings its flows into view (a folder's: its first file's).
  The three are separate: folding hides nothing on the right, and selecting ticks nothing. The flows are laid out file by
  file in the tree's order, each element under its icon, name and kind. On a narrow screen the tree sits above the flows.
- **The dependency graph.** `RuleViewing.DrawGraph` still draws a class's subclass as a graph (which element needs what,
  and what it gives) in three scopes: the subclass, + what it needs from outside, + what builds on it; the page doesn't
  show it.

The tool is three projects ([ADRs D39](../../docs/ADRs.md)):

- `Loopsmith.RuleViewer.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md) like a Core
  slice. It draws every rule as a tree, each with its flows (`RuleViewing.DrawTree`), one rule's flows (`RuleViewing.PickRuleFlows`) and the dependency graph, and uses of `Loopsmith.Core`
  only its kernel and `Orchestration` (`CatalogLoading.ParseCatalog` parses the rules).
- `Loopsmith.RuleViewer`: the host. It reads `rules/`, calls `RuleViewing` and writes two files.
- `Loopsmith.RuleViewer.Tests`: the unit and golden tests, and the architecture test that keeps the pure project pure
  (no files, network or console; no part of Core but its kernel and `Orchestration`). Core's host boundary test checks
  the host's and the pure project's calls into Core.
- [`tests/Loopsmith.RuleViewer.E2E`](../../tests/Loopsmith.RuleViewer.E2E/README.md): the page's e2e scenarios, played in
  Chromium on the page drawn from that project's fixture rules: the tree's arrows, checkboxes (and their dash) and names,
  and where the tree sits on a desktop and a phone (`tools/e2e/record.sh` draws, serves and records them).

The files:

- `out/rule-viewer/rules.json`: the tree (folders, files, elements with their boxes, rules, flows, boxes with their icons);
- `out/rule-viewer/rules.html`: the page, built by the components in `Loopsmith.RuleViewer/Page/` (`RulePage`,
  `RuleTreeView`, `RuleFileView`, `RuleFlowView`, `RuleBoxView`, `RuleIconView`) with the style and script in `Page/rules.css` and
  `Page/rules.js`. The script draws each flow's wires once its boxes have landed, and again on resize and theme change.

- `out/rule-viewer/<rule name>.png`, with `--rule <rule name>` instead of the two above: one rule's flows, by the rule's full
  name (`hunter-arc-aspects-lethal-current-2`, the name its e2e scenarios quote), stacked when the rule is split by its
  trigger (`keywords-arc-jolt-2` is four flows), each under the name; no title, lede or tree, in the light
  theme (`--theme dark` for the dark one, as the PRs' e2e posts (`tools/e2e/record.sh`) and the `loopsmith-task` session show it), at twice the pixels. The host renders a page of just those flows (`Page/RuleFlowPage`, with the same components,
  style and script) and takes its picture with Playwright's headless Chromium (`Hosting/RuleFlowShot`): the
  `Microsoft.Playwright` the e2e tests pin (`Directory.Packages.props`), which finds the Chromium in `/opt/pw-browsers`
  in a cloud session and in the dev container. Any rule or passive in `rules/` has its PNG, wherever its file sits (a
  folder added later too): the tool finds the element whose rule has that name and draws it as itself, with its own
  class and subclass (its affinity's; Prismatic for a neutral or kinetic one). A rule of every class names no class move
  (its class ability reads "Class ability", not "Dodge"), and "a weapon of your Super's element" stays that.

```bash
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer               # rules/ → out/rule-viewer/
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rules <dir> --out <dir>
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rule hunter-arc-aspects-lethal-current-2 [--theme light|dark] [--out <dir>]   # → <dir>/<rule name>.png
dotnet test tools/rule-viewer/Loopsmith.RuleViewer.Tests                  # also part of `dotnet test`
```

It exits with 1 and the error text when the rules don't parse, when no rule has the `--rule` name ("No rule is named
'…'.") or when the PNG can't be taken, and with 2 on an unknown option or a `--theme` other than `light` or `dark`. `out/` is gitignored.
The icons load from bungie.net. `RuleTreeGoldenTests` keeps every rule's flows as text, file by file,, so a rules change shows its flows
in the PR diff.

The page the owner designed, which this tool reproduces, was `tools/rule-viewer/prototype/` until commit `3237e89`.
