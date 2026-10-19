# Rule viewer

Draws every rule of the Arc Hunter as one flow, read left to right: what sets it off ➜ what must be in place ➜ what it
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
- **Groups and scopes.** Flows are grouped by what sets them off: Class ability, Air class ability, Melee, Sliding melee, Grenade,
  Finisher, Super, the weapons, Critical health, every other trigger by name, "Always on" last. Three chips pick the
  scope: the Arc subclass, + what it needs from outside (weapons), + what builds on it (mods, perks, exotics, artifact
  perks that react to something in it).

The tool is three projects ([ADRs D39](../../docs/ADRs.md)):

- `Loopsmith.RuleViewer.Core`: its logic, pure, following [CODING_RULES](../../docs/CODING_RULES.md) like a Core
  slice. It draws the dependency graph, its scopes and its rules, each as a flow (`RuleViewing.DrawRules`), and uses of `Loopsmith.Core`
  only its kernel and `Orchestration` (`CatalogLoading.ParseCatalog` parses the rules).
- `Loopsmith.RuleViewer`: the host. It reads `rules/`, calls `RuleViewing` and writes two files.
- `Loopsmith.RuleViewer.Tests`: the unit and golden tests, and the architecture test that keeps the pure project pure
  (no files, network or console; no part of Core but its kernel and `Orchestration`). Core's host boundary test checks
  the host's and the pure project's calls into Core.

The files:

- `out/rule-viewer/arc-hunter-rules.json`: the rules (scopes, groups, flows, boxes with their icons);
- `out/rule-viewer/arc-hunter-rules.html`: the page, built by the components in `Loopsmith.RuleViewer/Page/` (`RulePage`,
  `RuleGroupView`, `RuleFlowView`, `RuleBoxView`, `RuleIconView`) with the style and script in `Page/rules.css` and
  `Page/rules.js`. The script draws each flow's wires once its boxes have landed, and again on resize and theme change.

- `out/rule-viewer/<rule name>.png`, with `--rule <rule name>` instead of the two above: one rule's flows, by the rule's full
  name (`hunter-arc-aspects-lethal-current-2`, the name its e2e scenarios quote), stacked when the rule is split by its
  trigger (`keywords-arc-jolt-2` is four flows), each under the name; no title, lede or scope chips, in the light
  theme, at twice the pixels. The host renders a page of just those flows (`Page/RuleFlowPage`, with the same components,
  style and script) and takes its picture with Playwright's headless Chromium (`Hosting/RuleFlowShot`): the
  `Microsoft.Playwright` the e2e tests pin (`Directory.Packages.props`), which finds the Chromium in `/opt/pw-browsers`
  in a cloud session and in the dev container. Any rule or passive in `rules/` has its PNG, wherever its file sits (a
  folder added later too): the tool finds the element whose rule has that name and draws it as itself, with its own
  class and subclass (its affinity's; Prismatic for a neutral or kinetic one). A rule of every class names no class move
  (its class ability reads "Class ability", not "Dodge"), and "a weapon of your Super's element" stays that.

```bash
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer               # rules/ → out/rule-viewer/
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rules <dir> --out <dir>
dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rule hunter-arc-aspects-lethal-current-2 [--out <dir>]   # → <dir>/<rule name>.png
dotnet test tools/rule-viewer/Loopsmith.RuleViewer.Tests                  # also part of `dotnet test`
```

It exits with 1 and the error text when the rules don't parse, when no rule has the `--rule` name ("No rule is named
'…'.") or when the PNG can't be taken, and with 2 on an unknown option. `out/` is gitignored.
The icons load from bungie.net. `ArcHunterRulesGoldenTests` keeps the flows as text, so a rules change shows its flows
in the PR diff.

`prototype/` is the page the owner designed, kept as the reference this tool reproduces (its README rebuilds it).
