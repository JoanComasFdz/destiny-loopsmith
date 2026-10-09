# Designed loops (v1): file format, sharing, analysis, comparison

**The product of Loopsmith is a loop someone designed.** You pick a build, choose a trigger
("dodge", "throw grenade and kill"), see everything it fires and what is available next, choose
again, and so on. The resulting **loop** is data: it can be saved, shared, imported, replayed
and compared with another loop.

## File: `*.loop.yaml`

A loop file is **self-contained**: it embeds the full text of the build file it was designed
with, so it replays anywhere that has the rule catalog.

```yaml
# Loopsmith loop v1
loop: Infinite skip grenades            # required — the loop's name
author: Joan                            # optional
description: |                          # optional, free text
  Dodge to arm Slice and Reaper, skip grenade into the pack, then shoot.
catalog: authored-e4426d03166b          # optional — catalog version it was designed against
steps:                                  # required (may be empty), in order
  - do: class                           # an action token (below)
    note: Arm Slice + Reaper            # optional
  - do: grenade:kill
  - do: pickup:orb-of-power
build: |                                # required — the build file's full text (docs/rule-format.md)
  name: Skip Grenade Hunter
  class: hunter
  ...
```

* Keys are exactly `loop`, `author`, `description`, `catalog`, `steps`, `build`; each step has
  exactly `do` and optional `note`. Unknown keys are errors (`file:line: message`).
* **Writing** emits the keys in that order (build last, as a literal block scalar) and must
  round-trip: `ParseLoopFile(WriteLoopFile(design)) == design` (same name, author, description,
  catalog, steps, notes and *byte-identical* build text).
* **Importing** a loop whose `catalog` differs from the current catalog is allowed and reported
  as Info (the build's own validation issues still apply). An invalid embedded build or an
  unknown action token is an error.
* **Quoting** (v1 decision): the writer goes through YamlDotNet's emitter, which quotes whatever
  YAML needs. Text no block style carries exactly — `\r` (CRLF files), tabs and other control
  characters, NEL/LS/PS, a BOM, text made only of line breaks, whitespace-only leading lines, and
  `null`/`~`/empty — is written double-quoted with escapes. So `build` is a literal block unless its
  text contains such characters; it is byte-identical either way.
* **Locations** (v1 decision): the embedded build is parsed as `<loop file>#build`, so its own
  errors read `x.loop.yaml#build:<line within the build text>: …`.

### Action tokens

Same tokens everywhere (CLI `--actions`, `play`, loop files, web share links):

`grenade[:kill]` · `melee[:kill]` · `super[:kill]` · `class` · `kinetic|energy|power[:kill]` ·
`pickup:<pickup-id>` · `wait[:<seconds>]` — without `:kill` the hit only damages. Written tokens keep
every digit of a wait (`wait:2.25`), so they round-trip.

## Sharing

* **File:** export / import the `.loop.yaml`.
* **Link (web app):** `…/#loop=<payload>` where `payload` = base64url (no padding) of the
  raw-DEFLATE-compressed UTF-8 loop file. Opening the link imports the loop into the designer.
  Everything stays in the URL fragment — nothing is sent to a server.

## Analysis (`LoopReport`)

Running a loop = playing its steps **back to back, cycle after cycle**, starting from a fresh
spawn (grenade, melee, class ability charged; super empty), up to `maxCycles` (default 10).
Cycle *n* starts from the state cycle *n − 1* ended in.

| Field | Meaning |
|---|---|
| `Cycles` | each cycle's resolutions, the first blocked step (if any) and the energy at its end |
| `CompletedCycles` | cycles finished before any step was blocked (not enough energy, nothing to pick up…) |
| sustainable | `CompletedCycles == MaxCycles` — the loop feeds itself |
| `EnergyAtStart` | energy at the fresh spawn |
| `Sources` | how many times each element fired — **steady state** = the last completed cycle (cycle 1 if none completed) |
| `Outcomes` | steady-state counts: `Kills` (kill actions + killing strikes), `<Pickup> spawned`, `<Status> maxed` |
| `Uptime` | steady state: after how many of the cycle's steps each buff was active |
| `UnknownValues` | steady-state outcomes whose value is unknown (`?`) and therefore not applied — the real loop is stronger |
| `ChanceRules` | steady-state bullets marked *(chance)* — fired in v1, not guaranteed in game |

Net energy per cycle (steady state) = energy at the end of the last completed cycle minus
energy at the end of the cycle before it (or minus `EnergyAtStart` when only one cycle completed).

v1 decisions the table above leaves open (`Simulation.LoopRunning`, `Domain.LoopReportArithmetic`):

* A cycle with a blocked step is still **played to its end** (a blocked step changes nothing) and
  is the last cycle run; `Blocked` is its first blocked step (`StepIndex` 0-based).
* When no cycle completed, cycle 1 is the steady state for **every** metric, net energy included
  (end of cycle 1 minus `EnergyAtStart`).
* A loop **without steps** runs no cycle: 0 completed, not sustainable, net energy 0,
  `Outcomes = [Kills 0]`. `maxCycles` below 1 runs one cycle.
* `Kills` = kill actions that were performed (not blocked) + applied `strikeTarget … hit: kill`.
  `<Pickup> spawned` sums `spawn` counts (auto-collected pickups included). `<Status> maxed` counts
  each `StacksMaxed` event once, however many rules reacted to it.
* Order: `Sources` most fired first; `Outcomes` = `Kills`, then spawned, then maxed (first
  appearance); `Uptime` longest first (ties in the order the buffs were gained).

## Comparison (`LoopComparison`)

Two reports side by side (the loops may use different builds). One row per metric with both
values and which side is better (`Advantage.Left/Right/None`):

| Metric | Better |
|---|---|
| Steps per cycle | — (shown, not judged) |
| Sustained cycles (`10+` when sustainable) | more |
| Net grenade / melee / class ability / super energy per cycle | higher |
| Kills per cycle | more |
| `<Pickup> spawned` per cycle (union of both reports' pickups) | more |
| `<Status> maxed` per cycle | more |
| `<Buff>` uptime (union of both reports' buffs) | higher |
| Unknown values | fewer |
| Chance bullets | fewer |

v1 decisions: on equal completed cycles a sustainable loop beats one that broke; uptime is judged
as the fraction of the cycle's steps (shown `5/7`; a buff a report lacks is `0/<steps>`, a loop
without steps shows `—`); outcome rows keep the order Kills → spawned → maxed over the union;
values print with a sign (`+0.25`, `-0.1`, `0`). `ComparisonRendering` marks the better value ✓.

## CLI

```bash
loopsmith play    builds/skip-grenade-hunter/build.yaml --save my.loop.yaml --name "My loop"
loopsmith loop    builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml [--cycles 20] [--trace]
loopsmith compare builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
                  builds/skip-grenade-hunter/loops/melee-first.loop.yaml
```

In `play`, every action played becomes a step; `u` undoes it, `n <note>` notes the last step,
`d <text>` sets the description (`\n` = new line), `a` analyses the loop so far, `w` saves now and
`q` saves on quit (a design without steps is never saved, so it can't overwrite a loop file).
