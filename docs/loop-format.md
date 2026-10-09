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
  exactly `do` and optional `note`. Unknown keys are errors (`file:line: message`), and every error of
  the file is reported at once (`x.loop.yaml:4: step 2.do: Unknown action 'grenade:explode'. …`).
  A key whose value is empty, `~` or `null` counts as omitted; `catalog` must not be blank.
* **Writing** starts with the `# Loopsmith loop v1` comment and emits the keys in that order (build
  last, as a literal block scalar), leaving out absent optional keys; no steps are written `steps: []`,
  a description of several lines is a literal block, and long lines are never folded. It must
  round-trip: `ParseLoopFile(WriteLoopFile(design)) == design` (same name, author, description,
  catalog, steps, notes and *byte-identical* build text).
* **Importing** a loop whose `catalog` differs from the current catalog is allowed and reported
  as Info (`Loop designed against catalog <designed>; replaying with <current>.`); the build's own
  [check](rule-format.md#build-check) still applies. An unknown action token or an invalid embedded
  build is an error.
* **Quoting** (v1 decision): the writer goes through YamlDotNet's emitter, which quotes whatever
  YAML needs. Text no block style carries exactly — `\r` (CRLF files), tabs and other control
  characters, NEL/LS/PS, a BOM, text made only of line breaks, whitespace-only leading lines, and
  `null`/`~`/empty — is written double-quoted with escapes. So `build` is a literal block unless its
  text contains such characters; it is byte-identical either way.
* **Locations** (v1 decision): the embedded build is parsed as `<loop file>#build`, so its own
  errors read `x.loop.yaml#build:<line within the build text>: …`.

### Action tokens

Same tokens everywhere: CLI `--actions a,b,…` and `--scenario <file>` (one token per line or
comma-separated; `#` starts a comment), `play`, loop files and web share links:

`grenade|melee|super[:hit|kill[:N]]` · `class[:air]` · `kinetic|energy|power[:hit|kill[:N]]` ·
`pickup:<pickup-id>` · `wait[:<seconds>]` — without `:kill` the hit only damages.

`N` is how many enemies that one action hits (or kills), 1..20, default 1 — the player says it, the engine
can't know (ADRs D22): `grenade:kill:3` kills three, `kinetic:hit:5` shoots five, `energy:kill:2` kills two.
A count needs `:hit` or `:kill` before it (`grenade:3` is an error), and `class` takes no count.
`class:air` is the class ability used in the air — an air move that spends its charge, like Ascension's:
it fires every class ability rule plus the ones with an airborne trigger
([rule-format.md](rule-format.md#rules)). It always reads (CLI, loop files, share links), but `play` and
the web designer offer it only when an equipped rule has an airborne trigger.
`wait` is 5 s; `wait:2.5` (or `wait:2.5s`) any time above 0. Tokens are read case-insensitively.
`pickup:<id>` is any kebab-case id: one that isn't on the ground when played is a blocked step.
Errors read `Unknown action '<token>'. Use …` or `Invalid target count in '<token>': use a whole number from 1 to 20 …`.

Written tokens are lower-case and carry the count only when it is more than one (`grenade:kill`,
`kinetic`), so every loop file written before counts existed reads and writes back unchanged; they keep
every digit of a wait (`wait:2.25`). Every token written reads back as the same action (`grenade:hit`
is written `grenade`, `wait` as `wait:5`). Labels read "Grenade (kill 3)", "Festival Flight (hit 5)",
"Class ability (in the air)", "Wait 5s" (a weapon by its build name).

## Sharing

* **File:** export / import the `.loop.yaml`.
* **Link (web app):** `<app address>#loop=<payload>` where `payload` = base64url (no padding) of the
  raw-DEFLATE-compressed UTF-8 loop file. Opening the link imports the loop into the designer.
  Everything stays in the URL fragment — nothing is sent to a server. A payload that doesn't decode
  is an error (`The link's loop payload is damaged …`), and so is one over 2 MB once decompressed.

## Analysis (`LoopReport`)

Running a loop = playing its steps **back to back, cycle after cycle**, starting from a fresh
spawn (no buffs, an undebuffed pack, nothing on the ground), up to `maxCycles` (default 10;
CLI `--cycles <n>`). Cycle *n* starts from the state cycle *n − 1* ended in (buffs, debuffs and
pickups carry over). Each step resolves as in [rule-format.md](rule-format.md#engine-semantics-what-an-author-can-rely-on).

**Ability energy isn't simulated** (ADRs D21): abilities are always available, so a cycle breaks only
on a step that can't happen at all — a pickup that isn't on the ground, a weapon slot that's empty.
Energy outcomes show in the trace as explanations
(`+11.3% grenade and melee energy and +13.5% class ability energy [Ionic Trace]`) and aren't added up:
the analysis shows what works together and what is **wasted** (rules that don't stack), not exact
energy totals.

The report carries `LoopName`, `BuildName`, `StepCount` and `MaxCycles`, then:

| Field | Meaning |
|---|---|
| `Cycles` | each cycle's resolutions and the first blocked step (if any) |
| `CompletedCycles` | cycles finished before any step was blocked (nothing to pick up, no weapon in that slot) |
| repeatable (`IsRepeatable`) | `CompletedCycles == MaxCycles` — every step can be played again and again |
| `Sources` | how many bullets each element fired (one per rule per event) — **steady state** = the last completed cycle (cycle 1 if none completed). A rule that gave way (`doesNotStackWith`) isn't counted as fired |
| `Wasted` | steady state: how many times an element's rule gave nothing because a rule of an element it doesn't stack with fired on the same event (`doesNotStackWith`, [rule format](rule-format.md#rules-that-dont-stack)), per (element, partner): `7× Tempest Strike doesn't stack with Dielectric`; `nothing — everything that fired stacked` when empty. `CountWasted()` sums it |
| `Outcomes` | steady-state counts: `Kills` (targets of the kill actions + killing strikes), `<Pickup> spawned`, `<Status> maxed` |
| `Uptime` | steady state: after how many of the cycle's steps each buff was active |
| `UnknownValues` | steady-state outcomes whose value is unknown (`?`) and therefore not applied — the real loop is stronger |
| `ChanceRules` | steady-state bullets marked *(chance)* — fired in v1, not guaranteed in game (a rule that gave way isn't counted) |

v1 decisions the table above leaves open (`Simulation.LoopRunning`, `Domain.LoopReportArithmetic`):

* A cycle with a blocked step is still **played to its end** (a blocked step changes nothing) and
  is the last cycle run; `Blocked` is its first blocked step (`StepIndex` 0-based).
* When no cycle completed, cycle 1 is the steady state for **every** metric, `Wasted` included.
* A loop **without steps** runs no cycle: 0 completed, not repeatable, nothing fired or wasted,
  `Outcomes = [Kills 0]`. `maxCycles` below 1 runs one cycle.
* `Kills` = the targets of the kill actions that were performed (not blocked) — `grenade:kill:3` is
  3 — + applied `strikeTarget … hit: kill`.
  `<Pickup> spawned` sums `spawn` counts (auto-collected pickups included). `<Status> maxed` counts
  each `StacksMaxed` event a rule reacted to, once however many did (one no rule reacts to isn't counted).
* Order: `Sources` most fired first (ties in order of first appearance); `Wasted` most first (same
  ties); `Outcomes` = `Kills`, then spawned, then maxed (first appearance); `Uptime` longest first (ties
  in the order the buffs were gained).

The report as text (CLI `loop`, `a` in `play`; the web lays the same figures out and shows this text
under *The report as text*) opens with the verdict, one of:

```
✓ Repeatable — all 10 cycles completed back to back
✗ Breaks in cycle 1 at step 4 (pickup:orb-of-power): No orb-of-power on the ground — nothing happens. 0 of 10 cycles completed.
```

then the steady state (`Steady state (cycle 10, the last completed)`): the outcomes, `Fired per cycle`,
`Wasted per cycle`, `Buff uptime` (`5/6 steps`), and the `?` and `(chance)` counts. A loop without steps
reads `The loop has no steps — nothing to run (0 cycles completed).`

## Comparison (`LoopComparison`)

Two reports side by side (the loops may use different builds; both are replayed with the same
catalog and cycle count). One row per metric with both values and which side is better
(`Advantage.Left/Right/None`):

| Metric | Better |
|---|---|
| Steps per cycle | — (shown, not judged) |
| Repeatable cycles (`<maxCycles>+` when repeatable: `10+`) | more |
| Kills per cycle | more |
| `<Pickup> spawned` per cycle (union of both reports' pickups) | more |
| `<Status> maxed` per cycle | more |
| Wasted per cycle (doesn't stack) — `CountWasted()` | fewer |
| `<Buff>` uptime (union of both reports' buffs) | higher |
| Unknown values | fewer |
| Chance bullets | fewer |

v1 decisions: on equal completed cycles a repeatable loop beats one that broke; equal values are
even; a count a report lacks is 0; uptime is judged as the fraction of the cycle's steps (shown `5/7`;
a buff a report lacks is `0/<steps>`, a loop without steps shows `—`); outcome rows keep the order
Kills → spawned → maxed over the union (left report's first), then the wasted row, then uptime.
`ComparisonRendering` marks the better value ✓ and ends with a tally:
`✓ better: <left> on 4 metrics · <right> on 6 metrics · 8 even or not judged`.

## CLI

```bash
loopsmith play    builds/skip-grenade-hunter/build.yaml --save my.loop.yaml --name "My loop"
loopsmith loop    builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml [--cycles 20] [--trace] [--why] [--caveats]
loopsmith compare builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
                  builds/skip-grenade-hunter/loops/melee-first.loop.yaml [--cycles 20]
```

`loop` prints the build summary, the report and the steps; `--trace` adds every step of cycle 1
(`--why` and `--caveats` add reasons and caveats to it). The rules are found from the loop file
(`compare`: the first one) as from a build file ([rule-format.md](rule-format.md#files)), or `--rules <dir>`.

In `play` (the loop is named `<build name> loop` unless `--name` says otherwise), every action played
becomes a step: type its number in the list or a token (with a count, `grenade:kill:3`, to aim at
several enemies). `u` undoes the last step, `n <note>` notes it, `d <text>` sets the description
(`\n` = new line), `a` analyses the loop so far, `e` explains the build, `s` shows the state, `r`
starts over. `w` saves now and `q` saves on quit, both only once a step is designed ("No steps
designed — nothing saved."), so an empty design never overwrites a loop file. Saving needs `--save`.
