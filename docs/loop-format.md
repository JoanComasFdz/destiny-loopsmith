# Designed loops: file format (v1), starting from a DIM link, sharing, analysis, comparison

**The product of Loopsmith is a loop someone designs**
([ADRs D2](../ADRs.md#d2--the-designed-loop-is-the-product)): an ordered list of steps, each a trigger
("dodge", "throw a grenade and kill three") or a state the player declares ("Bolt Charge at max",
D3). You pick a build, choose a step, see everything it sets off and what is available next, choose
again, and so on — the build can come from a DIM link. Loopsmith describes cause and effect
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
`pickup:<pickup-id>` · `max:<status-id>` · `end:<status-id>` · `pack:new`

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
has ended, that the next enemies are a new pack — the player declares as a step of its own:

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
* `pack:new` — "New pack": the next enemies are a new pack, with none of the old pack's debuffs; the
  buffs on you and the pickups on the ground stay ([ADRs D7](../ADRs.md)). It raises no event and
  always holds. A loop that meets a new group of enemies each pass starts with it, so the next pass's
  first hit isn't credited with debuffs the new group doesn't have; a loop against one boss leaves it
  out.

**Written form.** Written tokens are lower-case and carry the count only when it is more than one
(`grenade:kill`, `kinetic`, `grenade:kill:3`, `max:bolt-charge`, `end:amplified`, `pack:new`). Every token written
reads back as the same action (`grenade:hit` is written `grenade`). Labels read "Grenade (kill 3)",
"Festival Flight (hit 5)" (a weapon by its build name), "Class ability (in the air)",
"Pick up Orb of Power", "Bolt Charge at max", "Amplified ends", "New pack".

**What `play` and the web designer offer.** Only steps that can happen: the abilities, the equipped
weapons, the pickups on the ground and a group **States you declare** — `pack:new` always, `max:` for
each active buff that stacks and isn't at max yet, `end:` for each active status. `class:air` is offered only when an equipped rule has an
airborne trigger (without one it fires exactly what `class` fires). Every token still reads anywhere
(CLI, loop files, share links).

## Starting from a DIM link

The web designer's Home starts a loop from a DIM link ([ADRs D27](../ADRs.md#d27--a-dim-link-starts-a-build-of-what-the-catalog-knows-by-hash-the-rest-is-left-out)):
paste it, press the button, and the designer opens with the loadout's build, named after the
loadout, its link as the build's `source` (the designer shows both). It reads the two kinds of link
DIM itself opens:

* **A dim.gg share** — what DIM's Share button copies (`https://dim.gg/4j5nz4q/Skip-Grenade`, or just
  the id). The loadout is on DIM's servers: the web app asks DIM's API for it
  (`GET https://api.destinyitemmanager.com/loadout_share?shareId=…` with the app's key as
  `X-API-Key`), which needs Loopsmith registered with DIM ([hosting.md](hosting.md#dim-links)).
  Until it is, a dim.gg link answers `Loopsmith can't open dim.gg links yet …`.
* **A link that carries its loadout** — DIM's Loadouts page with the loadout as JSON in `?loadout=`
  (D2ArmorPicker, guardian.report): read in the browser, nothing is fetched.

Anything else is `That isn't a DIM link …`. The loadout's items are named only by manifest hash, so
the build holds what the catalog recognises by hash, and nothing is guessed:

| From the loadout | Becomes |
|---|---|
| the subclass item | `class` and `subclass` — the glossary's `subclasses` ([rule-format.md](rule-format.md#glossary-rulesglossaryyaml): the nine Light ones so far, whose hashes DIM's source lists); a Stasis, Strand or Prismatic loadout, or one without a subclass, can't start a build yet |
| the subclass's plugs, in socket order | the super, grenade, melee and class ability, aspects and fragments the catalog has a hash for; an ability it has none for is `"?"` |
| the other equipped items (and the Loadout Optimizer's exotic) | `exoticArmor`; a weapon the manifest excerpt knows ([rule-format.md](rule-format.md#manifest-excerpt-rulesmanifestyaml)) becomes one of `weapons` — its slot, name, damage type, archetype and `hash`, no perks (a loadout names the weapon, not its roll) |
| `parameters.mods` (repeats are stacked copies) | `armorMods` |
| `parameters.artifactUnlocks` | `artifactPerks` |
| anything else — legendary armor, a jump, a weapon the excerpt doesn't know, a hash no element has | `leftOut`, by hash and where the loadout listed it |

The loadout's name becomes the build's, without Destiny's icon glyphs (DIM draws them with its own
font: "Arc  - Skipp grenade" reads "Arc - Skipp grenade"). The build is written as an ordinary build
file (comments say where it came from), so the loop
embeds, exports, shares and replays it like any other ([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)).
The designer shows the build the way DIM shows a loadout — the subclass with its super, abilities,
aspects and fragments, the artifact perks, the weapons (kinetic, energy, heavy) with their element and
their selected perks — the ones the build names, else an exotic's fixed ones from the manifest, and a "?"
for each trait column still unknown — each armor piece with the mods
of its slot (the slot the manifest gives the mod; other mods are general) — with Bungie's icons from the
manifest excerpt; what the rules don't know sits where DIM would show it, dimmed. It also lists the
left-out items under **Not in Loopsmith yet**, by their manifest name, each a link to its light.gg page; the build check adds `Unknown (?): …` and `Left out of the build: …` as info. A loadout for
another class than its subclass's is an error, and so is text that isn't a loadout.

**Shared DIM builds on Home.** A share can ship with the app as `builds/<slug>/dim-loadout.json` —
its link and the loadout as DIM's share page carries it (the page's "Open Loadout in DIM" link holds
it as `?loadout=` JSON; item instance ids left out):

```json
{ "link": "https://dim.gg/2jguuoq/Arc-Skipp-grenade", "loadout": { "name": "…", "classType": 1, "equipped": [ … ], "parameters": { "mods": [ … ] } } }
```

Home shows each one as a card — DIM offers no embeddable preview (its share page has only a stock
link image) — with the build's name, class and subclass, the same DIM-style loadout, and how much it
left out; "Start designing" opens it like a pasted
link, without asking DIM. The owner's own Skip Grenade build is the first
([builds/skip-grenade-hunter/dim-loadout.json](../builds/skip-grenade-hunter/dim-loadout.json); how it
differs from the hand-written `build.yaml` is in that build's discrepancies). The hand-written builds
stay on Home as links, with "import a build.yaml" and "import a loop".

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
✓ Repeats — on the first pass, #1 (Pick up Orb of Power) can't happen yet: No orb-of-power on the ground — nothing happens.
✗ Breaks at #3 (Pick up Orb of Power): No orb-of-power on the ground — nothing happens.
The loop has no steps.
```

The first when no step is blocked; the second when the repeating pass has no blocked step but the
first pass has one (it names the first pass's first blocked step); the third when the repeating pass
has a blocked step (the first one, with its message).

**The steps** of the repeating pass, each with:

* **needs** — what was already there when the step began and the step uses, and which step provided
  it:
  * a pickup it picks up: `Orb of Power ← #3 [Attrition Orbs (chance); Reaper; Strand Siphon (chance)]`
    (the step, then the elements that dropped it there);
  * a buff a rule consumes or a guard reads: `Reaper ← #2 [Reaper]`;
  * a debuff on the pack a trigger needs: `Jolt ← #3 [Spark of Shock]`;
  * a declared state: `Bolt Charge at max ← #7`.

  The provider is the latest step since the thing last arrived (it has been there from then on) where
  a rule that always fires provided it, else the latest one, so a sure drop is named before a chance
  one; `← previous pass #<k>` when it carries over from the pass before. What the step gives itself
  before it uses it — its hit jolts the pack, then its kill reads Jolt — is part of the step, not a
  need.
* **in this order** — the elements that fire there because of an earlier step: their rule used
  something an earlier step provided, or read what such a rule gave earlier in the step, or fired on
  an event such a rule raised (Dielectric's Bolt Charge on a pack an earlier grenade jolted sets off
  Shinobu's Vow). When the step's own action needs an earlier step (the orb it picks up, the buff it
  declares at max), everything it sets off is in this order;
* **on its own** — the elements that fire there wherever the step goes in the loop. Both lists mark
  each *(chance)* one (ADRs D8);
* **wasted** — a rule that gave way there because it doesn't stack with another element's
  ([rule-format.md](rule-format.md#rules-that-dont-stack), ADRs D6):
  `Tempest Strike — doesn't stack with Dielectric`.

**The chain** — the needs drawn as arrows, one per pair of steps: from the step that provided a need
to the step that needs it, labelled with what flows (`#2 → #3: Slice · Reaper`); one whose needs came
only from chance rules is marked *(chance)*. The web draws the links within a pass above the steps:
each step is a card whose left edge has a notch at the top, where the lines into it land (an
arrowhead), and a square slot at the bottom, where the lines out of it start. A step's lines leave its
slot as one stroke and split along it (a junction dot), one lane per later step they feed, the nearest
splitting off first; lines into the same step merge on its notch; where a step's stroke crosses a
line passing by from an earlier step, the stroke hops over it; a line by chance is faded. Tapping a step lists what it needs and what it feeds. A link
from the pass before (`previous pass #4 → #3: Unraveling Rounds`) shows in the step's needs, not as
an arrow.

Where the first pass differs — a step blocked there, or an element that fires on one pass and not on
the other — a **First pass** block lists those steps: *blocked*, *doesn't set off* (what fires in the
repeating pass but not yet on the first) and *only here* (what fires only on the first). A value the
sources don't give (`?`, ADRs D13) and a *(chance)* rule show on the bullets of the trace, where they
happen.

**The report as text** (CLI `loop`, `a` in `play`; the web lays the same out and shows this text under
*The report as text*). Illustrative, `…` elides:

```
Dodge, then shoot — Skip Grenade Hunter · 4 steps
New pack → Class ability → Festival Flight (kill) → Pick up Orb of Power
✓ Repeats — each pass ends with what the next one needs

#1 New pack

#2 Class ability
   on its own       Reaper · Slice · Gambler's Dodge · Bomber

#3 Festival Flight (kill)
   needs            Slice ← #2 [Slice] · Unraveling Rounds ← previous pass #4 [Unraveling Orbs] · Reaper ← #2 [Reaper]
   in this order    Slice · Unraveling Rounds · Reaper · To Shreds
   on its own       Attrition Orbs (chance) · Strand Siphon (chance)

#4 Pick up Orb of Power
   needs            Orb of Power ← #3 [Attrition Orbs (chance); Reaper; Strand Siphon (chance)]
   in this order    Unraveling Orbs · Orb of Power

First pass (from a fresh spawn, where it differs)
#3 Festival Flight (kill)
   doesn't set off  Unraveling Rounds
```

## Comparison

Two loops' **orders** side by side (CLI `compare`, the web Compare page). The loops may use different
builds; both replay with the same catalog. The comparison uses each loop's repeating pass:

* each loop's order on one line, with its verdict;
* then trigger by trigger (a declared state counts as one), matched by occurrence of the same action —
  A's first `grenade:kill` with B's first `grenade:kill` — its step number in each loop, where its
  needs come from there (marked when that loop's first pass differs there), and the elements that
  fire in only one of them;
* the triggers only one loop has, listed as such;
* the links of each loop's chain the other loop lacks — no link between the same two triggers, the
  same way round the loop — listed (`Links only in A`); the web draws each loop's chain with only
  those links.

Here the same steps in another order: the shot after the dodge gets Slice and Reaper from #2; the shot
before it gets them from the previous pass, so on B's first pass it sets off neither. The chains say it
in one line each.

```
A  Dodge, then shoot   New pack → Class ability → Festival Flight (kill) → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)
B  Shoot, then dodge   New pack → Festival Flight (kill) → Class ability → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)

New pack                A #1 · B #1: sets off the same
Class ability           A #2 · B #3: sets off the same
Festival Flight (kill)  A #3 · B #2: sets off the same
   A needs     Slice ← #2 · Unraveling Rounds ← previous pass #4 · Reaper ← #2 (differs on A's first pass)
   B needs     Slice ← previous pass #3 · Unraveling Rounds ← previous pass #4 · Reaper ← previous pass #3 (differs on B's first pass)
Pick up Orb of Power    A #4 · B #4: sets off the same
   A needs     Orb of Power ← #3
   B needs     Orb of Power ← #2

Links only in A
   #2 Class ability → #3 Festival Flight (kill): Slice · Reaper

Links only in B
   previous pass #3 Class ability → #2 Festival Flight (kill): Slice · Reaper
```

A trigger that sets off different elements in the two loops lists them as `only in A` / `only in B`;
one only a loop has reads `only in A (#7)`.

## CLI

```bash
loopsmith play    builds/skip-grenade-hunter/build.yaml --save my.loop.yaml --name "My loop"
loopsmith loop    builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml [--trace] [--why] [--caveats]
loopsmith compare builds/skip-grenade-hunter/loops/infinite-skip-grenades.loop.yaml \
                  builds/skip-grenade-hunter/loops/melee-first.loop.yaml
```

`loop` prints the build summary, the order, the verdict and the steps (needs / in this order / on its own / wasted, and
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
