# Designed loops: file format (v1), sharing, analysis, comparison

**The product of Loopsmith is a loop someone designs**
([ADRs D2](../ADRs.md#d2--the-designed-loop-is-the-product)): an ordered list of steps, each a trigger
("dodge", "throw a grenade and kill three") or a state the player declares ("Bolt Charge at max",
D3). You pick a build, choose a step, see everything it sets off and what is available next, choose
again, and so on. Loopsmith describes cause and effect
([ADRs D1](../ADRs.md#d1--loopsmith-describes-cause-and-effect-it-doesnt-simulate-the-game)), so a
loop is judged by its **order of triggers**: whether each step gets what it needs from the steps
before it, and whether the order repeats. The loop is data: it can be saved, shared, imported,
replayed and compared with another order.

## File: `*.loop.yaml`

A loop file is **self-contained**: it embeds the full text of the build file it was designed
with, so it replays anywhere that has the rule catalog.

```yaml
# Loopsmith loop v1
loop: Dodge, grenade, shoot             # required — the loop's name
author: Joan                            # optional
description: |                          # optional, free text
  Dodge to arm Slice and Reaper, skip grenade into the pack, shoot, grab Reaper's orb.
catalog: authored-e4426d03166b          # optional — catalog version it was designed against
steps:                                  # required (may be empty), in order
  - do: class                           # an action token (below)
    note: Arm Slice + Reaper            # optional
  - do: grenade:kill
  - do: kinetic:kill
  - do: pickup:orb-of-power
  - do: max:bolt-charge
    note: Shinobu's Vow and Flashover; the next grenade hit strikes
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
  as Info (`Loop designed against catalog <designed>; replaying with <current>.`, ADRs D15); the
  build's own [check](rule-format.md#build-check) still applies. An unknown action token or an invalid
  embedded build is an error.
* **Quoting:** the writer goes through YamlDotNet's emitter, which quotes whatever YAML needs. Text
  no block style carries exactly — `\r` (CRLF files), tabs and other control characters, NEL/LS/PS, a
  BOM, text made only of line breaks, whitespace-only leading lines, and `null`/`~`/empty — is written
  double-quoted with escapes. So `build` is a literal block unless its text contains such characters;
  it is byte-identical either way.
* **Locations:** the embedded build is parsed as `<loop file>#build`, so its own errors read
  `x.loop.yaml#build:<line within the build text>: …`.

### Action tokens

Same tokens everywhere: CLI `--actions a,b,…` and `--scenario <file>` (one token per line or
comma-separated; `#` starts a comment), `play`, loop files and web share links:

`grenade|melee|super[:hit|kill[:N]]` · `class[:air]` · `kinetic|energy|power[:hit|kill[:N]]` ·
`pickup:<pickup-id>` · `max:<status-id>` · `end:<status-id>`

Tokens are read case-insensitively. Errors read `Unknown action '<token>'. Use …` or
`Invalid target count in '<token>': use a whole number from 1 to 20 …`.

**Triggers.** An ability or weapon action without `:kill` only damages. `N` is how many enemies that
one action hits (or kills), 1..20, default 1 — the player says it, the engine can't know (ADRs D4):
`grenade:kill:3` kills three, `kinetic:hit:5` shoots five, `energy:kill:2` kills two. A count needs
`:hit` or `:kill` before it (`grenade:3` is an error), and `class` takes no count. `class:air` is the
class ability used in the air — an air move that spends its charge, like Ascension's: it fires every
class ability rule plus the ones with an airborne trigger ([rule-format.md](rule-format.md#rules),
ADRs D9). `pickup:<id>` (any kebab-case id) picks that pickup up — one `PickedUp` event — and it is
then no longer on the ground. A weapon action for an empty slot, or a pickup that isn't on the ground
(`No orb-of-power on the ground — nothing happens.`), is a **blocked step**: it changes nothing.

**States you declare** (ADRs D3). What only play decides — that a threshold is reached, that a status
has ended — the player declares as a step of its own:

* `max:<status>` — "Bolt Charge at max": that buff is at its maximum. It raises `StacksMaxed`, so every
  `stacksMaxed` rule fires and cascades, and the buff stays declared at max (the `atMax` condition reads
  it) until a rule consumes or removes it, or an `end:` step ends it. In the Skip Grenade build:

  ```
  #5 Bolt Charge at max
    Max Bolt Charge → New Tricks and +~40% grenade energy and heals you and allies [Shinobu's Vow] + Amplified (15s) [Flashover]
  ```

  and the next ability hit discharges it (Bolt Charge's `atMax` rule, Compendium Arc#5). The cap
  ("up to x10") is a fact the player reads when deciding to declare. A `max:` is blocked unless the buff
  stacks (`maxStacks` in the [glossary](rule-format.md#glossary-rulesglossaryyaml)), is active and isn't
  declared at max already — checked in that order:
  `No buff 'bolt-charg' in the rules — nothing to declare.` ·
  `Jolt is a debuff — only a buff on you can be at max.` ·
  `New Tricks doesn't stack — end it with end:new-tricks.` ·
  `Bolt Charge isn't active — nothing to declare at max.` ·
  `Bolt Charge is already at max.`
* `end:<status>` — "Amplified ends", "Jolt ends": the buff on you (with its declaration at max) or the
  debuff on the pack has ended and is removed. It raises no event, so no rule reacts to it. Blocked
  unless the status is active: `Amplified isn't active — nothing ends.` ·
  `No buff or debuff 'amplifyed' in the rules — nothing ends.`

**Written form.** Written tokens are lower-case and carry the count only when it is more than one
(`grenade:kill`, `kinetic`, `grenade:kill:3`, `max:bolt-charge`, `end:amplified`). Every token written
reads back as the same action (`grenade:hit` is written `grenade`). Labels read "Grenade (kill 3)",
"Festival Flight (hit 5)" (a weapon by its build name), "Class ability (in the air)",
"Pick up Orb of Power", "Bolt Charge at max", "Amplified ends".

**What `play` and the web designer offer.** Only steps that can happen: the abilities, the equipped
weapons, the pickups on the ground and a group **States you declare** — `max:` for each active
buff that stacks and isn't at max yet, `end:` for each active status. `class:air` is offered only when an equipped rule has an
airborne trigger (without one it fires exactly what `class` fires). Every token still reads anywhere
(CLI, loop files, share links).

## Sharing

* **File:** export / import the `.loop.yaml`.
* **Link (web app):** `<app address>#loop=<payload>` where `payload` = base64url (no padding) of the
  raw-DEFLATE-compressed UTF-8 loop file. Opening the link imports the loop into the designer.
  Everything stays in the URL fragment — nothing is sent to a server. A payload that doesn't decode
  is an error (`The link's loop payload is damaged …`), and so is one over 2 MB once decompressed.

## Analysis

The analysis follows the loop's order (CLI `loop`, `a` in `play`, the web designer's Analysis view).
Each step resolves as in [rule-format.md](rule-format.md#engine-semantics-what-an-author-can-rely-on).

1. **First pass** — the steps in order from a fresh spawn: no buffs, a clean pack, nothing on the
   ground.
2. **Repeating pass** — the loop is played again from where the previous pass ended (the buffs and
   their declarations, the debuffs on the pack and the pickups on the ground carry over), until a pass
   starts the way an earlier pass started: the same buffs (the same ones declared at max), debuffs
   and pickups. That pass is the **repeating pass**. The state is only what is present and
   declared, so this settles within a few passes.
3. A **blocked step** — nothing to pick up, no weapon in that slot, a declaration that doesn't hold
   ([action tokens](#action-tokens)) — changes nothing, and the pass goes on with the next step.

**Verdict**, one of:

```
✓ Repeats — each pass ends with what the next one needs
✓ Repeats — on the first pass, #3 (Pick up Orb of Power) has nothing to pick up yet
✗ Breaks at #3 (Pick up Orb of Power): No orb-of-power on the ground — nothing happens.
The loop has no steps.
```

The first when no step is blocked; the second when the repeating pass has no blocked step but the
first pass has one (it names the first pass's first blocked step); the third when the repeating pass
has a blocked step (the first one, with its message).

**The steps** of the repeating pass, each with:

* **needs** — what the step uses and which step provided it (here for the loop of the
  [file example](#file-loopyaml)):
  * a pickup it picks up: `Orb of Power ← #3 [Reaper; Attrition Orbs (chance); Strand Siphon (chance)]`
    (the step, then the elements that dropped it);
  * a buff a rule consumes or a guard reads: `Reaper ← #1 [Reaper]`;
  * a debuff on the pack a trigger needs: `Jolt ← #2 [Spark of Shock]`;
  * a declared state: the grenade at #2 reads `Bolt Charge at max ← previous pass #5` —
    `← previous pass #<k>` whenever a need carries over from the pass before.
* **sets off** — the elements whose rules fired there, each *(chance)* one marked (ADRs D8);
* **wasted** — a rule that gave way there because it doesn't stack with another element's
  ([rule-format.md](rule-format.md#rules-that-dont-stack), ADRs D6):
  `Tempest Strike — doesn't stack with Dielectric`.

Where the first pass differs — a step blocked there, or an element that fires on one pass and not on
the other — a **First pass** block lists those steps and why (what an earlier step hadn't provided
yet). A value the sources don't give (`?`, ADRs D13) and a *(chance)* rule show on the bullets of the
trace, where they happen.

**The report as text** (CLI `loop`, `a` in `play`; the web lays the same out and shows this text under
*The report as text*). Illustrative, `…` elides:

```
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

## Comparison

Two loops' **orders** side by side (CLI `compare`, the web Compare page). The loops may use different
builds; both replay with the same catalog. The comparison uses each loop's repeating pass:

* each loop's order on one line, with its verdict;
* then trigger by trigger (a declared state counts as one), matched by occurrence of the same action —
  A's first `grenade:kill` with B's first `grenade:kill` — its step number in each loop, where its
  needs come from there, and the elements that fire in only one of them and why (what an earlier step
  provided, or didn't);
* the triggers only one loop has, listed as such;
* where a loop's first pass differs, it says so.

```
A  Dodge, then shoot   Class ability → Festival Flight (kill) → Pick up Orb of Power   ✓ Repeats
B  Shoot, then dodge   Festival Flight (kill) → Class ability → Pick up Orb of Power   ✓ Repeats
Class ability           A #1 · B #2: sets off the same
Festival Flight (kill)  A #2: Reaper ← #1 · B #1: Reaper ← previous pass #2 (not on B's first pass)
Pick up Orb of Power    A #3: Orb of Power ← #2 · B #3: Orb of Power ← #1
```

## CLI

```bash
loopsmith play    builds/skip-grenade-hunter/build.yaml --save my.loop.yaml --name "My loop"
loopsmith loop    builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml [--trace] [--why] [--caveats]
loopsmith compare builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
                  builds/skip-grenade-hunter/loops/melee-first.loop.yaml
```

`loop` prints the build summary, the order, the verdict and the steps (needs / sets off / wasted, and
the first pass where it differs); `--trace` adds the full trace of the first pass, and of the
repeating pass when it differs (`--why` and `--caveats` add reasons and caveats to it, `--verbose`
both). `compare` prints the comparison above. The rules are found from the loop file (`compare`: the
first one) as from a build file ([rule-format.md](rule-format.md#files)), or `--rules <dir>`.

In `play` (the loop is named `<build name> loop` unless `--name` says otherwise), everything played
becomes a step: type its number in the list (abilities, weapons, pickups on the ground and the states
you can declare) or a token (with a count, `grenade:kill:3`, to aim at several enemies;
`max:bolt-charge` to declare a state). `u` undoes the last step, `n <note>` notes it, `d <text>` sets
the description (`\n` = new line), `a` analyses the loop so far, `e` explains the build, `s` shows the
state, `r` starts over. `w` saves now and `q` saves on quit, both only once a step is designed
("No steps designed — nothing saved."), so an empty design never overwrites a loop file. Saving needs
`--save`.
