# Loopsmith

**See what your Destiny 2 build actually does.**

Loopsmith describes the **cause and effect** in a Destiny 2 build; it doesn't simulate the game
([requirements](docs/requirements.md), [ADRs D1](ADRs.md)). For each trigger it shows every
outcome that fires, which element caused it, what that unlocks next and what doesn't work together.
Numbers are facts shown with their outcome ("+12% grenade energy", "Amplified (15s)"); what only
play decides — that Bolt Charge is at max, that a buff has ended, how many enemies a grenade kills —
you declare, and Loopsmith shows what it sets off.

DIM shows what you have equipped. Loopsmith takes that build (class, subclass, abilities, aspects,
fragments, exotic, armor set, mods, artifact perks, weapons and perks) and explains its
**gameplay loop**.

**The product is the loop you design**: pick triggers one by one (dodge, grenade kill, pick up an
orb, Bolt Charge at max), and the result is a `*.loop.yaml` you can save, share as a link, replay and
compare with another loop. A loop is judged by its **order of triggers**: does each step get what it
needs from the steps before it, does the order repeat, and what is wasted where ("Tempest Strike's
Bolt Charge doesn't stack with Dielectric's").

> **Try it:** https://joancomasfdz.github.io/destiny-loopsmith/ — the web loop designer
> (runs entirely in your browser). Status: working prototype on authored rules for one build
> ([Skip Grenade Hunter](builds/skip-grenade-hunter/), plus the creator's own
> [Ascension variant](builds/skip-grenade-hunter-ascension/)); what's next is in the
> [Roadmap](#roadmap) and [docs/backlog.md](docs/backlog.md).

## Quick start

Online: open the link above. Locally (requires the .NET 10 SDK):

```bash
dotnet run --project src/Loopsmith.Web     # the web loop designer, http://localhost:5000
```

The CLI does the same from a terminal (the golden tests snapshot the same text it prints):

```bash
dotnet build Loopsmith.slnx
dotnet run --project src/Loopsmith.Cli -- explain  builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- trace    builds/skip-grenade-hunter/build.yaml \
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
| `trace` | A sequence of steps (`--actions class,grenade:kill:3,max:bolt-charge,energy:kill` or `--scenario file`), one block per step: the event → outcomes `[source]`, cascades indented `↳`; `--state` adds what is present after each step (buffs on you, debuffs on the pack, pickups on the ground). `--why` shows the reason text; `--caveats` the caveats (amount unknown, already active, …). `simulate` is an alias |
| `play` | Interactive: pick the next step by number or token — an action, a pickup on the ground or a state you declare — and see what fires and what's available now. Every step becomes part of the loop you design: `u` undo, `n <note>`, `d <description>`, `a` analyse it so far; `--save <file.loop.yaml>` writes it on quit (`w` saves now), `--name` names it |
| `loop` | A designed loop (`*.loop.yaml`): its build, its order of steps and the verdict — it repeats, or it breaks at the step that can't happen — then each step with what it needs (and which step provided it), what it sets off and what is wasted there, and where the first pass from a fresh spawn differs. `--trace` adds the full trace of the first pass, and of the repeating pass when it differs |
| `compare` | Two designed loops' orders side by side (they may use different builds): each order on one line with its verdict, then trigger by trigger its step in each loop, where its needs come from there, and the elements that fire in only one of them and why (what an earlier step provided, or didn't) |
| `loops` | Discovered loops: cycles in the cause → effect graph, ability loops first ("ability loop — gives grenade energy back"). Reaching a stacking buff's max is a link you declare; where a rule gives way to one it doesn't stack with, the loop passes a "Doesn't stack" node (not counted as a step) |
| `graph` | A Mermaid flowchart of the graph, with loop edges drawn thick; `Gain Bolt Charge → Max Bolt Charge` is a dotted edge labelled "you declare"; the arrows of rules that don't stack meet in a "Doesn't stack" rhombus, and only the rule that applies leaves it. It renders on GitHub and at mermaid.live |
| `validate` | The build checked against the rule catalog (unknown elements, wrong slots, inert elements, rules that don't stack) |

Every command takes `--rules <dir>` (default: the nearest `rules/` with a `glossary.yaml` above the build or loop
file, then above the current directory) and `--no-color` (also when `NO_COLOR` is set or the output is redirected); `loops` and
`graph` show `--limit <n>` loops (default 10); `--verbose` is `--why --caveats`. `--help` lists every
option.

### What it looks like

`explain` on the [Skip Grenade Hunter](builds/skip-grenade-hunter/) (an excerpt), compare with the
[original note](builds/skip-grenade-hunter/note.txt):

```text
Class ability -> +?% melee energy [Gambler's Dodge] + +12% grenade energy [Bomber] + Reaper (10s) [Reaper] + +1 Slice (8s) [Slice]
Grenade damage -> Jolt target (10s) [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
Ability damage -> consumes Bolt Charge and Bolt Charge strike (kills) (while Bolt Charge at max) [Bolt Charge]
Kill Jolted target -> +1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike] + Amplified (15s) [Flow State] + Ionic Trace [Shock and Clear] + +1 Bolt Charge [Dielectric]
Pick up Ionic Trace -> +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
Gain Bolt Charge -> +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
Max Bolt Charge -> New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover]
While Amplified -> +1 Bolt Charge per gain [Spark of Frequency] + linear-fusion-rifle/fusion-rifle/heat-weapon: +handling, +reload [Ionic Overclock]
```

`trace` / `play`: a step, cascades indented, then the state (`--state`); then a state you declare
(`max:bolt-charge`):

```text
#2 Grenade (kill)
  Grenade hit → Jolt target (10s) [Spark of Shock] + +1 Bolt Charge and +4.2% grenade energy [Shinobu's Vow]
    ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Grenade kill on Jolted target → Amplified (15s) [Flow State] + +1 Bolt Charge [Dielectric] + Ionic Trace [Shock and Clear] + doesn't stack with Dielectric [Tempest Strike]
    ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
    ↳ Picked up Ionic Trace → +1 Bolt Charge [Spark of Discharge] + +1 Armor Charge [Elemental Charge] (chance) + +11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]
      ↳ Bolt Charge gained → +?% grenade energy [Shinobu's Vow] + +2.5% melee energy [Bolt Charge]
  Buffs         Reaper · Slice · Bolt Charge · Amplified · Armor Charge
  Target        Jolt
  Ground        none
  Active      +1 Bolt Charge per gain [Spark of Frequency]
  Active      linear-fusion-rifle/fusion-rifle/heat-weapon: +handling, +reload [Ionic Overclock]
#3 Bolt Charge at max
  Max Bolt Charge → New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover]
  Buffs         Reaper · Slice · Bolt Charge (at max) · Amplified · Armor Charge · New Tricks
  Target        Jolt
  Ground        none
  Active      +1 Bolt Charge per gain [Spark of Frequency]
  Active      linear-fusion-rifle/fusion-rifle/heat-weapon: +handling, +reload [Ionic Overclock]
```

`loop` on a three-step loop (dodge, shoot, pick up the orb) — illustrative, `…` elides:

```text
Dodge, then shoot — Skip Grenade Hunter · 3 steps
Class ability → Festival Flight (kill) → Pick up Orb of Power
✓ Repeats — each pass ends with what the next one needs

#1 Class ability
   sets off  Gambler's Dodge · Bomber · Reaper · Slice
#2 Festival Flight (kill)
   needs     Reaper ← #1 [Reaper] · Slice ← #1 [Slice] · Unraveling Rounds ← previous pass #3 [Unraveling Orbs] · …
   sets off  Slice · Reaper · Attrition Orbs (chance) · Strand Siphon (chance) · …
#3 Pick up Orb of Power
   needs     Orb of Power ← #2 [Reaper; Attrition Orbs (chance); Strand Siphon (chance)]
   sets off  Unraveling Orbs · Orb of Power

First pass
#2 Festival Flight (kill)
   doesn't set off  Unraveling Rounds — no Unraveling Rounds yet (#3 gives it) · …
```

`compare` of that loop with the same steps in another order:

```text
A  Dodge, then shoot   Class ability → Festival Flight (kill) → Pick up Orb of Power   ✓ Repeats
B  Shoot, then dodge   Festival Flight (kill) → Class ability → Pick up Orb of Power   ✓ Repeats
Class ability           A #1 · B #2: sets off the same
Festival Flight (kill)  A #2: Reaper ← #1 · B #1: Reaper ← previous pass #2 (not on B's first pass)
Pick up Orb of Power    A #3: Orb of Power ← #2 · B #3: Orb of Power ← #1
```

`?` = the source doesn't say (shown, never treated as 0) · `~` = approximate ·
`(chance)` = may not happen in game; it always fires here and is marked ([ADRs D8](ADRs.md)) ·
`doesn't stack with X` = this rule gives nothing when X's fires on the same event
([ADRs D6](ADRs.md)); `loop` lists it as wasted · `Reaper ← #1 [Reaper]` = this step needs Reaper,
which step #1 gave (from the element in brackets). Abilities are always available; an energy outcome
is a fact about what gives energy back ([ADRs D5](ADRs.md)). The loop graph renders on GitHub:
[builds/skip-grenade-hunter/loop-graph.md](builds/skip-grenade-hunter/loop-graph.md).

Action tokens: `grenade|melee|super[:hit|kill[:N]]`, `class[:air]`, `kinetic|energy|power[:hit|kill[:N]]`,
`pickup:<pickup-id>`, `max:<status-id>`, `end:<status-id>` — `N` is how many enemies that one action
hits or kills (1..20, default 1): `grenade:kill:3`, `kinetic:hit:5` ([ADRs D4](ADRs.md)); `class:air` is
the class ability used in the air, like Ascension's air move ([ADRs D9](ADRs.md)); `pickup:orb-of-power`
picks up an orb that is on the ground; `max:bolt-charge` declares Bolt Charge at max and `end:amplified`
that Amplified has ended ([ADRs D3](ADRs.md)). A step that can't happen is blocked and changes nothing
(`No orb-of-power on the ground — nothing happens.`).

## How it works

```
rules/*.yaml ─┐                         ┌─ explain          (static: trigger → outcomes)
build.yaml ───┼─ parse → validate ──────┼─ trace / play     (pure state machine: (state, step) → (state, fired))
*.loop.yaml ──┘   (railway, typestate)  ├─ loop / compare   (a designed loop's order: needs, sets off, wasted)
                                        └─ loops / graph    (cycles in the cause → effect graph)
```

* Every build element is "an element with rules": `on` trigger → `then` outcomes (+ always-on
  passives). Edges aren't stored; an edge appears when an outcome (a buff, an orb, a debuff,
  energy) matches another rule's trigger. Loops come from that cascade.
* The state is what is present: buffs on you, debuffs on the pack, pickups on the ground, and the
  states you declared ("Bolt Charge at max"). Each step is played against it and gives the next state
  and every rule it fired.
* Outcomes of one event apply in phase order (debuff → empower → damage → spawn → refund),
  then derived events cascade depth-first (depth ≤ 5; a rule never re-fires on an identical
  event up its own causal chain).
* Every number is a `GameValue`, shown as a fact with its outcome; every element keeps its provenance. **Unknown stays
  unknown**: a "?" is shown, never treated as 0.

What Loopsmith is and must do, starting with the level of abstraction:
[docs/requirements.md](docs/requirements.md). Rule and build file format and the exact engine
semantics: [docs/rule-format.md](docs/rule-format.md). Designed loops — the `.loop.yaml` format,
share links, analysis and comparison: [docs/loop-format.md](docs/loop-format.md). Example loops:
[builds/skip-grenade-hunter/loops/](builds/skip-grenade-hunter/loops/) and
[builds/skip-grenade-hunter-ascension/loops/](builds/skip-grenade-hunter-ascension/loops/).
Coding conventions (binding): [CONVENTIONS.md](CONVENTIONS.md) · decisions: [ADRs.md](ADRs.md).

## Repository layout

```
src/Loopsmith.Core/      one project, slices = folders (kernel: Domain, Functional, Phrasing, Causality)
src/Loopsmith.Cli/       host: argv → Orchestration shell → effects
src/Loopsmith.Web/       host: Blazor WebAssembly loop designer (Designer, Compare)
tests/Loopsmith.Core.Tests/   unit, golden and architecture tests
rules/                   authored causality (glossary, keywords, hunter, exotics, armor sets, mods, artifact, weapon perks)
builds/<slug>/           build.yaml, discrepancies, loop-graph.md, loops/*.loop.yaml (+ the original note, note-map and
                         scenario.txt where the user wrote a note): skip-grenade-hunter and its Ascension variant
docs/                    requirements, rule format, loop format, hosting, backlog
tools/compendium/        Destiny Data Compendium download: sheet_dump.py + Docker/Python scripts
tools/web/               prepare-pages.sh — readies a published site for GitHub Pages
.github/workflows/       CI, Pages deploy (main), PR previews
.claude/                 cloud-session hook + project subagents
```

## Data sources

| Source | Gives | Status |
|---|---|---|
| Authored rules (`rules/`) | Causality: what fires on what | ✅ used by the engine |
| [Clarity](https://github.com/Database-Clarity/Live-Clarity-Database) | Hash-keyed descriptions with numbers (mods, fragments, aspects, exotic perks, weapon traits) | Its numbers are in the first build's rules by hand (v2.0625); an ingestion slice is open |
| Destiny Data Compendium | Abilities, artifact perks, statuses, and facts such as cooldowns and chunk energy scalars | The 2026-10-09 snapshot's numbers are in the first build's rules (by hand, `compendium/<date>/<tab>#<row>` sources); parser next |
| Bungie manifest | Identity (hashes), names, icons | Open (FR-13): not needed for loop design; needs an API key |

**Getting a Compendium snapshot:** run `tools/compendium/get-compendium.ps1` (Windows) or
`get-compendium.sh` locally and hand the resulting zip to a session — see
[tools/compendium/README.md](tools/compendium/README.md). (`docs.google.com` isn't reachable from
the cloud environment unless it's added to its allowed domains.)

**Licensing.** The Compendium is one person's donation-supported work. Keep snapshots
private, out of any public repo (`snapshots/` is gitignored), never served as raw text,
and credit it. Clarity's partnerships page applies to a public site like this one (an owner check,
[docs/backlog.md](docs/backlog.md)). Bungie API use
falls under Bungie's API terms. Creator video transcripts are third-party content: keep them
locally as `builds/<slug>/transcript.txt` (gitignored) and link the video instead.

## Development workflow

1. Branch from `main`, commit, open a pull request.
2. CI runs the full test suite, and the **PR preview** workflow publishes that branch's web app
   to `https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<number>/` (the link is
   commented on the PR, updated on every push, removed when the PR closes; pull requests from
   forks get no preview).
3. Merge → the **Deploy web app** workflow republishes the live site.

`gh-pages` is the built site, owned by those workflows; a ruleset blocks its deletion and
force pushes. Details: [docs/hosting.md](docs/hosting.md).

## Roadmap

What works is in each requirement's status ([docs/requirements.md](docs/requirements.md)); the open
work in detail in [docs/backlog.md](docs/backlog.md). Next:

1. States you declare (`max:`, `end:`, [ADRs D3](ADRs.md)) and the loop analysis and comparison by
   order of triggers (FR-4, FR-6, FR-7).
2. Ingest: Compendium snapshot parsers (tab registry) + Clarity enrichment + coverage report (FR-11).
3. Manifest join: names → hashes, icons (FR-13, [ADRs D14](ADRs.md)).
4. Rule drafting from the Compendium's "On X:" phrasing (FR-12, [ADRs D10](ADRs.md)).
5. More builds; richer rules (the rule-format gaps in [docs/backlog.md](docs/backlog.md)).

Destiny 2 is a trademark of Bungie. Loopsmith is a fan project, not affiliated with Bungie.
