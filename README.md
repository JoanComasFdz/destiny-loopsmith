# Loopsmith

**See what your Destiny 2 build actually does.**

DIM shows what you have equipped. Loopsmith takes a build (class, subclass, abilities,
aspects, fragments, exotic, armor set, mods, artifact perks, weapons and perks) and
explains its **gameplay loop**. For each trigger (dodge, grenade kill, pick up an orb,
max Bolt Charge) it shows every outcome that fires, which element caused it, what that
unlocks next, and where the loop closes back on itself.

**The product is the loop you design**: pick triggers one by one, and the result is a
`*.loop.yaml` you can save, share as a link, replay, analyse ("repeats 10+ cycles, 7 kills per
cycle; Tempest Strike's Bolt Charge is wasted 7×: it doesn't stack with Dielectric's") and compare
with another loop.

> **Try it:** https://joancomasfdz.github.io/destiny-loopsmith/ — the web loop designer
> (runs entirely in your browser). Status: working prototype on authored rules for one build
> ([Skip Grenade Hunter](builds/skip-grenade-hunter/)); Compendium / manifest ingestion is
> next (see [Roadmap](#roadmap) and [docs/backlog.md](docs/backlog.md)).

## Quick start

Online: open the link above. Locally (requires the .NET 10 SDK):

```bash
dotnet run --project src/Loopsmith.Web     # the web loop designer, http://localhost:5xxx
```

The CLI does the same from a terminal (and is what the golden tests drive):

```bash
dotnet build Loopsmith.slnx
dotnet run --project src/Loopsmith.Cli -- explain  builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- simulate builds/skip-grenade-hunter/build.yaml \
    --scenario builds/skip-grenade-hunter/scenario.txt --state
dotnet run --project src/Loopsmith.Cli -- play     builds/skip-grenade-hunter/build.yaml --save my.loop.yaml
dotnet run --project src/Loopsmith.Cli -- loop     builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml
dotnet run --project src/Loopsmith.Cli -- compare  builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
    builds/skip-grenade-hunter/loops/melee-first.loop.yaml
dotnet run --project src/Loopsmith.Cli -- loops    builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- graph    builds/skip-grenade-hunter/build.yaml --loops-only > loop.mmd
```

| Command | What you get |
|---|---|
| `explain` | Every trigger in the build → the outcomes it fires `[source]`, in the same shape as a hand-written build note (`--tree` for an aligned tree) |
| `simulate` | A sequence of actions (`--actions grenade:kill:3,class,energy:kill` or `--scenario file`), one block per step: the event → outcomes `[source]`, cascades indented `↳`, then buffs, target debuffs and ground pickups (`--state`). `--why` shows the reason text; `--caveats` shows the unknowns |
| `play` | Interactive: pick the next action by number or token, see what fires and what's available now. Every action becomes a step of the loop you design: `u` undo, `n <note>`, `d <description>`, `a` analyse it so far; `--save <file.loop.yaml>` writes it on quit (`w` saves now), `--name` names it |
| `loop` | A designed loop (`*.loop.yaml`) run back to back from a fresh spawn (`--cycles N`, default 10): does it repeat, where it breaks, kills / pickups / maxed stacks, what fired, what was wasted (rules that don't stack), buff uptime, unknowns and chance bullets. `--trace` adds every step of cycle 1 |
| `compare` | Two designed loops side by side (they may use different builds), one row per metric, the better value marked ✓ |
| `loops` | Discovered loops: cycles in the cause → effect graph, ability-energy loops first |
| `graph` | A Mermaid flowchart of the graph, with loop edges drawn thick. It renders on GitHub and at mermaid.live |
| `validate` | The build checked against the rule catalog (unknown elements, wrong slots, inert elements, rules that don't stack) |

### What it looks like

`explain` on the [Skip Grenade Hunter](builds/skip-grenade-hunter/), compare with the
[original note](builds/skip-grenade-hunter/note.txt):

```text
Class ability -> +?% melee energy [Gambler's Dodge] + +12% grenade energy [Bomber] + Reaper (10s) [Reaper] + +1 Slice (8s) [Slice]
Grenade damage -> Jolt target [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
Kill Jolted target -> +1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike] + Amplified [Flow State] + Ionic Trace [Shock and Clear] + +1 Bolt Charge [Dielectric]
Pick up Ionic Trace -> +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
Gain Bolt Charge -> +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
Max Bolt Charge -> New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover] + consumes Bolt Charge and Bolt Charge strike (kills) [Bolt Charge]
While Amplified -> +1 Bolt Charge per gain [Spark of Frequency] + linear-fusion-rifle/fusion-rifle/heat-weapon: +handling, +reload [Ionic Overclock]
```

`simulate` / `play`: one step, cascades indented, then the state:

```text
#2 Grenade (kill)
  Grenade hit → Jolt target [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
    ↳ Bolt Charge ×1 → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Grenade kill on Jolted target → Amplified [Flow State] + +1 Bolt Charge [Dielectric] + Ionic Trace [Shock and Clear] + doesn't stack with Dielectric [Tempest Strike]
    ↳ Bolt Charge ×3 → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
    ↳ Picked up Ionic Trace → +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
      ↳ Bolt Charge ×5 → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Buffs         Reaper 10s · Slice ×1 8s · Bolt Charge ×5 · Amplified 15s · Armor Charge ×1
  Target        Jolt 10s
```

`?` = unknown (never applied as 0) · `~` = approximate ·
`(chance)` = fires in v1 but isn't guaranteed in game · `doesn't stack with X` = this rule gives nothing
when X's fires on the same event ([ADRs D23](ADRs.md)); `loop` counts it as wasted. Ability energy isn't
simulated — abilities are always available, and energy outcomes are explanations, never added up
([ADRs D21](ADRs.md)). The loop graph renders on GitHub:
[builds/skip-grenade-hunter/loop-graph.md](builds/skip-grenade-hunter/loop-graph.md).

Action tokens: `grenade|melee|super[:hit|kill[:N]]`, `class`, `kinetic|energy|power[:hit|kill[:N]]`,
`pickup:<id>`, `wait[:<seconds>]` — `N` is how many enemies that one action hits or kills (1..20, default 1):
`grenade:kill:3`, `kinetic:hit:5` ([ADRs D22](ADRs.md)).

## How it works

```
rules/*.yaml ─┐                         ┌─ explain   (static: trigger → outcomes)
build.yaml ───┼─ parse → validate ──────┼─ simulate  (pure state machine: (state, action) → (state, fired))
              │   (railway, typestate)  └─ loops / graph (cycles in the cause → effect graph)
```

* Every build element is "an element with rules": `on` trigger → `then` outcomes (+ always-on
  passives). Edges aren't stored; an edge appears when an outcome (a buff, an orb, a debuff,
  energy) matches another rule's trigger. Loops come from that cascade.
* Outcomes of one event apply in phase order (debuff → empower → damage → spawn → refund),
  then derived events cascade depth-first (depth ≤ 5; a rule never re-fires on an identical
  event up its own causal chain).
* Every number is a `GameValue` with provenance. **Unknown stays unknown**: a "?" is shown,
  never applied as 0.

Rule and build file format and the exact engine semantics: [docs/rule-format.md](docs/rule-format.md).
Designed loops — the `.loop.yaml` format, share links, analysis and comparison metrics:
[docs/loop-format.md](docs/loop-format.md). Example loops:
[builds/skip-grenade-hunter/loops/](builds/skip-grenade-hunter/loops/).
Design proposal (requirements, data sources, architecture, roadmap, risks):
[docs/design/loopsmith-design-v0.3.html](docs/design/loopsmith-design-v0.3.html).
Coding conventions (binding): [CONVENTIONS.md](CONVENTIONS.md) · decisions: [ADRs.md](ADRs.md).

## Repository layout

```
src/Loopsmith.Core/      one project, slices = folders (kernel: Domain, Functional, Phrasing, Causality)
src/Loopsmith.Cli/       host: argv → Orchestration shell → effects
src/Loopsmith.Web/       host: Blazor WebAssembly loop designer (Designer, Compare)
tests/Loopsmith.Core.Tests/   unit, golden and architecture tests
rules/                   authored causality (glossary, keywords, class, exotics, mods, artifact, perks)
builds/<slug>/           build.yaml, the original note, note-map, discrepancies, loops/*.loop.yaml
docs/                    rule format, loop format, hosting, backlog, design proposal
tools/compendium/        Destiny Data Compendium download: sheet_dump.py + Docker/Python scripts
tools/web/               prepare-pages.sh — readies a published site for GitHub Pages
.github/workflows/       CI, Pages deploy (main), PR previews
.claude/                 cloud-session hook + project subagents
```

## Data sources

| Source | Gives | Status |
|---|---|---|
| Authored rules (`rules/`) | Causality: what fires on what | ✅ used by the engine |
| [Clarity](https://github.com/Database-Clarity/Live-Clarity-Database) | Hash-keyed descriptions with numbers (mods, fragments, aspects, exotic perks, weapon traits) | Used to author the first build (v2.0625); ingestion slice next |
| Destiny Data Compendium | Abilities, cooldowns, chunk energy scalars, artifact perks, statuses | The 2026-10-09 snapshot's numbers are in the first build's rules (by hand, `compendium/<date>/<tab>#<row>` sources); parser next |
| Bungie manifest | Identity (hashes), names, icons | Next (needs an API key) |

**Getting a Compendium snapshot:** run `tools/compendium/get-compendium.ps1` (Windows) or
`get-compendium.sh` locally and hand the resulting zip to a session — see
[tools/compendium/README.md](tools/compendium/README.md). (`docs.google.com` isn't reachable from
the cloud environment unless it's added to its allowed domains.)

**Licensing.** The Compendium is one person's donation-supported work. Keep snapshots
private, out of any public repo (`snapshots/` is gitignored), never served as raw text,
and credit it. Check Clarity's partnerships page before a public site. Bungie API use
falls under Bungie's API terms. Creator video transcripts are third-party content: keep them
locally as `builds/<slug>/transcript.txt` (gitignored) and link the video instead.

## Development workflow

1. Branch from `main`, commit, open a pull request.
2. CI runs the full test suite, and the **PR preview** workflow publishes that branch's web app
   to `https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<number>/` (the link is
   commented on the PR, updated on every push, removed when the PR closes).
3. Merge → the **Deploy web app** workflow republishes the live site.

`gh-pages` is the built site, owned by those workflows; a ruleset blocks its deletion and
force pushes. Details: [docs/hosting.md](docs/hosting.md).

## Roadmap

1. ✅ Skeleton: kernel, slices, architecture tests, CI.
2. ✅ Rules for the first build; simulation (match, guard, phase order, cascade, energy scalar); CLI.
3. ✅ Golden test: the engine reproduces the build note.
4. ✅ Designed loops as the product: `.loop.yaml`, share links, analysis, comparison.
5. ✅ Web loop designer (Blazor WebAssembly) on GitHub Pages, with a preview per pull request.
6. Ingest: Compendium snapshot parsers (tab registry) + Clarity enrichment + coverage report (FR-7, FR-9).
7. Manifest join (names → hashes, icons), catalog versions (FR-8).
8. Rule drafting from the Compendium's "On X:" phrasing (FR-10).
9. More builds; richer rules (see the expressiveness gaps in [docs/backlog.md](docs/backlog.md)).

Destiny 2 is a trademark of Bungie. Loopsmith is a fan project, not affiliated with Bungie.
