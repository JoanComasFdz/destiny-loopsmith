# Loopsmith

**See what your Destiny 2 build actually does.**

DIM shows what you have equipped. Loopsmith takes a build (class, subclass, abilities,
aspects, fragments, exotic, armor set, mods, artifact perks, weapons and perks) and
explains its **gameplay loop**. For each trigger (dodge, grenade kill, pick up an orb,
max Bolt Charge) it shows every outcome that fires, which element caused it, what that
unlocks next, and where the loop closes back on itself.

> Status: **initial CLI prototype.** Engine, CLI and the first build
> ([Skip Grenade Hunter](builds/skip-grenade-hunter/)) work end to end on authored rules.
> Manifest / Compendium ingestion comes next (see [Roadmap](#roadmap)).

## Quick start

Requires the .NET 10 SDK.

```bash
dotnet build Loopsmith.slnx
dotnet run --project src/Loopsmith.Cli -- explain  builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- simulate builds/skip-grenade-hunter/build.yaml \
    --scenario builds/skip-grenade-hunter/scenario.txt --state
dotnet run --project src/Loopsmith.Cli -- play     builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- loops    builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Cli -- graph    builds/skip-grenade-hunter/build.yaml --loops-only > loop.mmd
```

| Command | What you get |
|---|---|
| `explain` | Every trigger in the build → the outcomes it fires `[source]`, in the same shape as a hand-written build note (`--tree` for an aligned tree) |
| `simulate` | A sequence of actions (`--actions grenade:kill,class,energy:kill` or `--scenario file`), one block per step: the event → outcomes `[source]`, cascades indented `↳`, then energy bars, buffs, target debuffs and ground pickups (`--state`). `--why` shows the reason text; `--caveats` shows the unknowns |
| `play` | Interactive: pick the next action by number or token, see what fires and what's available now |
| `loops` | Discovered loops: cycles in the cause → effect graph, ability-energy loops first |
| `graph` | A Mermaid flowchart of the graph, with loop edges drawn thick. It renders on GitHub and at mermaid.live |
| `validate` | The build checked against the rule catalog (unknown elements, wrong slots, inert elements) |

Action tokens: `grenade[:kill]`, `melee[:kill]`, `super[:kill]`, `class`,
`kinetic|energy|power[:kill]`, `pickup:<id>`, `wait[:<seconds>]`.

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
Design proposal (requirements, data sources, architecture, roadmap, risks):
[docs/design/loopsmith-design-v0.3.html](docs/design/loopsmith-design-v0.3.html).
Coding conventions (binding): [CONVENTIONS.md](CONVENTIONS.md) · decisions: [ADRs.md](ADRs.md).

## Repository layout

```
src/Loopsmith.Core/      one project, slices = folders (kernel: Domain, Functional, Phrasing, Causality)
src/Loopsmith.Cli/       host: argv → Orchestration shell → effects
tests/Loopsmith.Core.Tests/   unit, golden and architecture tests
rules/                   authored causality (glossary, keywords, class, exotics, mods, artifact, perks)
builds/<slug>/           build.yaml, the original note, note-map, discrepancies, sources
docs/                    rule format, design proposal
tools/compendium/        sheet_dump.py — Destiny Data Compendium snapshot tool
.claude/                 cloud-session hook + project subagents
```

## Data sources

| Source | Gives | Status |
|---|---|---|
| Authored rules (`rules/`) | Causality: what fires on what | ✅ used by the engine |
| [Clarity](https://github.com/Database-Clarity/Live-Clarity-Database) | Hash-keyed descriptions with numbers (mods, fragments, aspects, exotic perks, weapon traits) | Used to author the first build (v2.0625); ingestion slice next |
| Destiny Data Compendium | Abilities, cooldowns, chunk energy scalars, artifact perks, statuses | Snapshot via `tools/compendium/sheet_dump.py`; parser next |
| Bungie manifest | Identity (hashes), names, icons | Next (needs an API key) |

**Getting a Compendium snapshot** (run locally; `docs.google.com` isn't reachable from the
cloud environment):

```bash
docker run --rm -v "$PWD":/w -w /w -u "$(id -u):$(id -g)" -e HOME=/tmp -e PIP_DISABLE_PIP_VERSION_CHECK=1 python:3.12-slim \
  sh -c 'pip install -q --user --no-warn-script-location requests beautifulsoup4 && python tools/compendium/sheet_dump.py "https://docs.google.com/spreadsheets/d/1WaxvbLx7UoSZaBqdFr1u32F2uWVLo-CJunJB4nlGUE4/edit" snapshots/compendium/$(date +%F)'
```

**Licensing.** The Compendium is one person's donation-supported work. Keep snapshots
private, out of any public repo (`snapshots/` is gitignored), never served as raw text,
and credit it. Check Clarity's partnerships page before a public site. Bungie API use
falls under Bungie's API terms. `builds/*/transcript.txt` is a creator's video transcript
kept for reference in this private repo; remove it before making the repo public.

## Roadmap

1. ✅ Skeleton: kernel, slices, architecture tests, CI.
2. ✅ Rules for the first build; simulation (match, guard, phase order, cascade, energy scalar); CLI.
3. ✅ Golden test: the engine reproduces the build note.
4. Ingest: Compendium snapshot parsers (tab registry) + Clarity enrichment + coverage report (FR-7, FR-9).
5. Manifest join (names → hashes, icons), catalog versions (FR-8).
6. Rule drafting from the Compendium's "On X:" phrasing (FR-10).
7. API host, then the UI (DIM-like builder + step picker + trace).

Destiny 2 is a trademark of Bungie. Loopsmith is a fan project, not affiliated with Bungie.
