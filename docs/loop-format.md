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

### Action tokens

Same tokens everywhere (CLI `--actions`, `play`, loop files, web share links):

`grenade[:kill]` · `melee[:kill]` · `super[:kill]` · `class` · `kinetic|energy|power[:kill]` ·
`pickup:<pickup-id>` · `wait[:<seconds>]` — without `:kill` the hit only damages.

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
