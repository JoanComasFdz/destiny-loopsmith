# Designed loops: file format (v1), starting from scratch or a DIM link, sharing, analysis, comparison

**The product of Loopsmith is a loop someone designs**
([ADRs D2](ADRs.md#d2--the-designed-loop-is-the-product)): an ordered list of steps, each a trigger
("dodge", "throw a grenade and kill three") or a state the player declares ("Bolt Charge at max",
D3). You pick a build, choose a step, see everything it sets off and what is available next, choose
again, and so on — the build can come from a DIM link, or start from scratch. Loopsmith describes cause and effect
([ADRs D1](ADRs.md#d1--loopsmith-describes-cause-and-effect-it-doesnt-simulate-the-game)), so a
loop is judged by its **order of triggers**: whether each step gets what it needs from the steps
before it, and whether the order repeats. The loop is data: it can be saved, shared, imported,
replayed and compared with another order.

## File: `*.loop.yaml`

A loop file is **self-contained**: it embeds the full text of the build file it was designed
with, so it replays anywhere that has the rule catalog.

<!-- LoopFileParsingTests reads the next yaml block -->
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

Same tokens everywhere: loop files, web share links and a golden test's scenario file
(`builds/skip-grenade-hunter/scenario.txt`: one token per line or comma-separated; `#` starts a comment):

`grenade[:hold]|melee[:slide|air]|super[:air|combo|guard][:hit|kill[:N]]` · `class[:air]` · `kinetic|energy|power[:hit|kill[:N]]` ·
`kinetic|energy|power:reload` · `kinetic|energy|power:ready` · `ammo:special|heavy[:slide]` · `finisher` · `pickup:<pickup-id>` · `max:<status-id>` · `end:<status-id>` · `pack:new` · `health:critical` · `health:hit` — and last, an option:
`:last` on a grenade, melee or class ability step, `:shield` and `:precision` on a weapon step

Tokens are read case-insensitively. Errors read `Unknown action '<token>'. Use …` or
`Invalid target count in '<token>': use a whole number from 1 to 20 …`.

**Triggers.** An ability or weapon action without `:kill` only damages. `N` is how many enemies that
one action hits (or kills), 1..20, default 1 — the player says it, the engine can't know (ADRs D4):
`grenade:kill:3` kills three, `kinetic:hit:5` shoots five, `energy:kill:2` kills two. A **hit** step is damage kept
up on each enemy without killing it: a rule that needs several hits or a share of the enemy's health (To Shreds'
Unravel, Horde Shuttle's Threadling, Attrition Orbs' orb, Lightning Rounds' strike) fires there for sure. A **kill**
step's hit is the killing blow, one blow, so those rules don't fire on it: add a hit step before the kill when the loop
keeps shooting (ADRs D34). A count needs
`:hit` or `:kill` before it (`grenade:3` is an error), and `class` takes no count. A **secondary ability
use** goes between the ability and its hit (ADRs D9): `melee:slide:kill` is the melee while sliding
(Tempest Strike's uppercut), `melee:air` the melee in the air, `grenade:hold` the grenade held (Chaos
Accelerant, Bleak Watcher), `class:air` the class ability's air move — the air move key while airborne
spends its charge, like Ascension's — and the Super's `super:air:hit` (its heavy attack in the air), `super:combo:kill`
(its light-light-heavy combo: Arc Staff's Palm Strike) and `super:guard:hit:N` (its guard: N attacks reflected, each a
hit on an enemy of the pack). A secondary fires every rule of its ability plus the ones that name
the input ([rule-format.md](rule-format.md#rules)); an ability takes only the inputs the game has
(`grenade:slide`, `class:hold`, `super:hold` and `melee:combo` are errors). `finisher` — "Finisher" — finishes one enemy of the pack: it fires the `finisher` rules, then
its hit and kill fire the `damage` and `kill` rules whose source is `any` (a finisher has no damage type
and is no weapon or ability, so `kill: { via: any, targetHas: [jolt] }` fires on a finisher of a jolted
enemy; [rule-format.md](rule-format.md#rules)); it takes no count and is never blocked. `pickup:<id>`
(any kebab-case id) picks that pickup up — one `PickedUp` event — and it is
then no longer on the ground. A weapon action for an empty slot, or a pickup that isn't on the ground
(`No orb-of-power on the ground — nothing happens.`), is a **blocked step**: it changes nothing.

**Reload** (ADRs D37). `kinetic:reload`, `energy:reload`, `power:reload` — "Reload Festival Flight": the player reloads
that weapon, as a step of its own. No ammo is counted (Loopsmith isn't a simulator, ADRs D1), so a reload is never
needed: the player declares it when the loop reloads, and it raises `Reloaded` from that weapon, so the `reload` rules
fire ([rule-format.md](rule-format.md#rules)). It takes no count and no option (`energy:reload:3`, `energy:reload:shield`
and `grenade:reload` are errors); a slot with no weapon is a blocked step (`No weapon in the Kinetic slot — nothing
happens.`). A rule can reload a weapon too (`reloadWeapon`): the trace shows `reloads your energy weapon`, and that
weapon's `reload` rules fire as on a Reload step. A refill is no reload (ADRs D42): Demolitionist's grenade and Return
Stroke's lightning strike refill the weapon (`refillWeapon`), the trace shows `refills your energy weapon`, and no
`reload` rule fires. In the designer,
tapping a weapon in the step HUD shows **🔄 reload** next to its 🥊 hit and 💀 kill, in every build.

**Pick up an ammo brick** (ADRs D43). `ammo:special`, `ammo:heavy` — "Pick up a Heavy ammo brick"; with `:slide`
(`ammo:special:slide`), "Slide over a Special ammo brick": the player picks up a brick of that ammo, walking or sliding
over it, as a step of its own. Bricks drop from enemies, which only play decides, so the step is always available and
never blocked, and it names no weapon. No ammo is counted, so the player declares it when the loop picks up a brick
they aren't full on; it raises `AmmoPickedUp`, so the `ammoPickedUp` rules fire: on any brick (All-Star, Adhesive
Ordnance), on that brick, and on a slide only those without `by:` and those with `by: slide` (Spark of Momentum: +3
Bolt Charge). A refill a perk gives is a fact, no reload: no `reload` rule fires. It takes no count and no option but
the slide (`ammo:heavy:3`, `ammo:primary`, `energy:ammo` are errors). In the designer, **📦 Ammo brick** sits below the
weapons in every build; tapping it shows **special** and **heavy**, and **🛝 by sliding** next to them when a rule of
the build reacts to a slide.

**Ready a weapon** (ADRs D44). `kinetic:ready`, `energy:ready`, `power:ready` — "Ready Buzzard": the player readies
(swaps to) that weapon, as a step of its own. Which weapon is in hand isn't kept; it raises `WeaponReadied` from that
weapon, so the `ready` rules fire (Mechaneer's Tricksleeves: Spring-Loaded Mounting on a sidearm at critical health).
Like a reload it takes no count and no option (`energy:ready:3`, `energy:ready:shield` are errors), and a slot with no
weapon is a blocked step. In the designer, tapping a weapon shows **✋ ready** before 🔄 reload,
in every build, enabled only when a rule of the build reacts to readying that weapon.

**Options of a step** (ADRs D36). What only play decides about a step itself goes last in its token:

* `:shield` — a weapon step breaks a combatant's shield (`energy:kill:shield`, `kinetic:hit:3:shield`), with damage of
  its element: say it only when the weapon's element matches the shield's. It breaks one, with the step's first hit,
  and raises `ShieldBroken`, so the `shieldBroken` rules fire (Shield Break Charge: x1 Armor Charge, x2 with two
  copies). An ability step takes no `:shield` (`grenade:kill:shield` is an error).
* `:precision` — a weapon step's hits and kills land on the enemies' weak spot: precision kills (`energy:kill:precision`,
  `kinetic:kill:3:precision`: all three are). Its kills raise `Killed` as precision kills, so the kill rules with
  `hit: precision` fire on them and on no other kill (Spark of Brilliance: a precision kill of a blinded enemy blinds
  the enemies around it), the ones with `hit: body` never do (Hatchling's chance on fewer than 3 body kills); every
  other kill rule fires as on any kill. A hit step takes it too (`energy:hit:precision`):
  the game has precision hits, though no rule reacts to one yet. It goes with `:shield`, written after it
  (`energy:kill:shield:precision`, read in either order): a shield takes no precision damage while it holds, but the step
  breaks it with its first hit and its killing blow can then be a headshot. An ability step, a Super and a reload take no
  `:precision` (`grenade:kill:precision`, `super:kill:precision`, `energy:reload:precision` are errors).
* `:last` — a grenade, melee or class ability step spends the ability's last charge, its energy fully expended
  (`grenade:kill:last`, `melee:slide:kill:last`, `class:last`), and raises `EnergyExpended`, so the `energyExpended`
  rules fire (the Kickstarts spend your Armor Charge). With one charge in the build every use is the last one, so a
  plain `grenade` raises it too; with more (Shinobu's Vow's second grenade) only the use marked `:last` does. A Super
  and a weapon take no `:last` (`super:last`, `kinetic:last` are errors).

**States you declare** (ADRs D3). What only play decides — that a threshold is reached, that a status
has ended, that the next enemies are a new pack — the player declares as a step of its own:

* `max:<status>` — "Bolt Charge at max (x10)": that buff is at its maximum. The label names the build's cap, a
  fact: the glossary's `maxStacks`, or the cap a `raiseMaxStacks` passive states for its copies (Charged Up: "Armor
  Charge at max (x4)" with one copy — [rule-format.md](rule-format.md#passives)); the player declares it reached, nothing counts toward it
  — but Armor Charge, which is counted (below, "Armor Charge is counted"): its `max:` is offered while its sure stacks are below the
  cap, and makes them the cap (the player says the chance ones came, or more than the loop shows).
  It raises `StacksMaxed`, so every
  `stacksMaxed` rule fires and cascades, and the buff stays declared at max (the `atMax` condition reads
  it) until a rule consumes or removes it, or an `end:` step ends it. In the Skip Grenade build:

  ```
  #5 Bolt Charge at max (x10)
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
  debuff on the pack has ended and is removed. `end:super` — "Super ends": casting the Super puts you in it (the
  `super` status, while its damage resistance counts) and every step after it is still in it, until this step. It raises no event, so no rule reacts to it. Blocked
  unless the status is active: `Amplified isn't active — nothing ends.` ·
  `No buff or debuff 'amplifyed' in the rules — nothing ends.`
* `pack:new` — "New pack": the next enemies are a new pack, with none of the old pack's debuffs; the
  buffs on you and the pickups on the ground stay ([ADRs D7](ADRs.md)). It raises no event and
  always holds. A loop that meets a new group of enemies each pass starts with it, so the next pass's
  first hit isn't credited with debuffs the new group doesn't have; a loop against one boss leaves it
  out.
* `health:critical` — "Critical health": you reached critical health (your shields broke). Loopsmith models no
  damage you take (ADRs D1), so the player declares it. It puts **Critical health** on you (among your buffs) until
  `end:critical-health` ("Critical health ends": your shields are full again), and raises `CriticalHealth`, so every
  `criticalHealth` rule fires and cascades. It holds unless you are already at critical health
  (`Already at critical health — declare it ended first (end:critical-health).`): a loop that repeats ends it before
  it reaches it again. Emergency Reinforcement reacts to it: it
  spends your Armor Charge for its buff, whose 10% damage resistance (PvP `?`) counts in the step's damage
  resistance until `end:emergency-reinforcement` — for sure with x3 or more sure Armor Charge, as a *(chance)* when
  only chance stacks make it x3, not at all below (ADRs D33); its 6 | 9 | 12 s go by the stacks it spent.
* `health:hit` — "Hit at critical health": while at critical health, you are damaged. The player declares it (ADRs D1,
  D3), as often as the loop needs. It raises `HitAtCriticalHealth`, so every `hitAtCriticalHealth` rule fires and
  cascades, and changes no state. It holds only while Critical health is on you, so a step before it must reach it
  (`Not at critical health — declare Critical health (health:critical) first.`). Spark of Instinct reacts to it: its
  burst jolts the pack (its damage and the time between bursts are facts in its description).

**Armor Charge is counted** (ADRs D33). Its stacks are part of the state: the **sure** ones, from rules that always
fire, and the **chance** ones, from chance rules and from what they set off (an orb only a chance rule dropped — a
Siphon's from one kill — gives a chance stack). "Dodge for Reaper → weapon kill → pick up the orb" shows **Armor
Charge x1** (x2 with Stacks on Stacks); a Siphon's orb from one kill adds **+1 (chance)**: `x1 · +1 (chance)`. A grant
stops at the build's cap (x3; x4–x6 with Charged Up), the rest wasted (caveat `at cap: +1 wasted`); sure stacks at
the cap are at max. A Kickstart spends every stack and shows its energy for the copies and stacks
(`spends Armor Charge x2 · +1 (chance) → +28.4% grenade energy (34.4% with the chance stack)`); a finisher that needs
x3 fires for sure with x3 sure stacks, as a *(chance)* when the chance ones make it x3, and not at all below — the
step says `Bulwark Finisher needs x3 Armor Charge, holds x1 — it doesn't fire.`; a fixed spend (Healthy Finisher's
x1) takes sure stacks first. The decay (one stack every 10 s, slower with Time Dilation) is shown, never applied: time
isn't modelled. The next pass starts from what the last one ended with.

**Written form.** Written tokens are lower-case and carry the count only when it is more than one
(`grenade:kill`, `kinetic`, `grenade:kill:3`, `finisher`, `max:bolt-charge`, `end:amplified`, `pack:new`, `health:critical`, `health:hit`). Every token written
reads back as the same action (`grenade:hit` is written `grenade`). Labels read "Grenade (💀 kill 3)",
"Festival Flight (🥊 hit 5)" (a weapon by its build name), "Sliding melee (💀 kill)" — 💀 marks a kill step and 🥊 a
hit step, in the steps' titles and the HUD's buttons alike — "Festival Flight (💀 kill · 🛡 shield)", "Festival Flight (💀 kill ·
🎯 precision)", "Festival Flight (💀 kill · 🛡 shield · 🎯 precision)", "Grenade (💀 kill ·
⬜ spend fully)", "Class ability (⬜ spend fully)", "Class ability (in the air)",
"Finisher", "Pick up Orb of Power", "Bolt Charge at max (x10)", "Amplified ends", "New pack", "Critical health",
"Hit at critical health", "Critical health ends".

**What the web designer offers.** Only steps that can happen: the abilities, the equipped
weapons, the pickups on the ground and the **states you declare** — `pack:new` always, `health:critical` while you
aren't at critical health, when an equipped rule reacts to it or to a hit at it (the designer shows it with the buffs
**On you**), `health:hit` while you are, when an equipped rule reacts to it (its **hit** button sits beside
"Critical health" **On you**, with **ends**), `max:` for
each active buff that stacks and isn't at max yet, `end:` for each active status. A secondary (`melee:slide`, `class:air`)
is offered only when an equipped rule names it (without one it fires exactly what the press fires); `finisher` (with
the abilities) always, as the game always allows one — in a build where no rule reacts to a finisher and none fires on
a kill from any source, it sets nothing off. Every token still reads anywhere
(loop files, share links).

The web designer's **Next trigger** is laid out like Destiny's HUD: the Super's diamond, then the grenade, melee and
class ability squares with the build's own icons in the subclass's colour, each (the Super's diamond too) with the
secondary uses the build's rules name below it — a different trigger from the press, captioned by its input ("slide",
"air", "hold", "combo", "guard") with the icon of the element that names it (Tempest Strike, Ascension, Arc Staff) — and a dashed **Finisher** square (the game has no HUD square for it; a finisher has no element and no damage
type) with the finisher's own icon: after a Bungie login the one the player's character of the build's class has
equipped (read from its Finishers slot; named from Bungie's full manifest when the excerpt doesn't have it), else the
class's own, the one the game plays when none is equipped (Blade Carnival for a Hunter, the glossary's `finishers`) —
then the three weapons with their icon, damage type's icon and colour. Tapping the Super, an ability or a weapon
shows its actions under the HUD — hit or kill, and how many enemies that one action catches. The counts go up to the
last one that still fills something the build can gain, from the state the step starts in: a buff that stacks below
its cap (Bolt Charge x10, Armor Charge at the build's cap) or an ability's energy below a full charge (the Ionic Traces
that refill the grenade); each count says what fills up there and what needs it — an equipped rule that needs that
many from that ability or weapon in one action ("1 enemy", "2 in 3 s · Arc Siphon", "3 · Spark of Discharge", "3 in 3 s ·
One for All", "2 · Reaper's Tithe",
"4 enemies · Armor Charge x4, Bolt Charge x10", "7 enemies · grenade full", "12 · Synergy"; rules of one count with
different windows each say theirs, "2 · Spark of Amplitude in 4 s, Harmonic Siphon in 3 s"). A larger count a rule needs
is offered too, and so is a step's own count (a loop file's `grenade:kill:15`). The sums only bound the choices: they are
never game state and decide nothing (ADRs D1, D5) — and it stays picked
for the next step; a secondary's own hit and kill show the same way ("Sliding melee · Tempest Strike"); the class
ability, its air move and the finisher act at one tap. A row's option (ADRs D36) shows beside its count, a box the
player ticks: **🛡 shield** on each weapon when an equipped rule reacts to a broken shield (Shield Break Charge), **🎯
precision** on each weapon when an equipped rule reacts to a precision kill (Spark of Brilliance), and
**⬜ Spend fully** (its last charge) on the grenade, melee or class ability when the build gives it more than one charge and an equipped
rule reacts to its energy fully expended (Shinobu's Vow and Grenade Kickstart). Ticked, it goes with the row's hit and
kill (`energy:kill:shield`, `energy:kill:precision`, `grenade:kill:last`) and stays ticked for the next steps; a class ability with the option
shows its button under the HUD instead of acting at one tap. Above the HUD, where the game lists your buffs, the states to declare are grouped by
status — on you, on your weapons, on the pack (with **New pack**) — each with its own buttons ("Bolt Charge: at max
(x10) · ends"), and what is on the ground to pick up.

**How the designer shows a step.** The same facts and wording as the trace as text (`TraceRenderer`), one line each: the
event ("Grenade hit"); under it, where what it set off comes from, with that item's icon — "Fragment ·
Arcstrider" (the subclass), "Weapon perk · Festival Flight", "Armor mod · Luminopotent Cloak" (the piece in
the mod's slot the build shows, else the slot), "Artifact perk · Tablet of Ruin", "Armor set ·
Luminopotent", "Exotic armor", and for the keywords every build has, what each is in the game: "Arc buff" (Bolt
Charge), "Arc debuff" (Jolt), "Arc elemental pickup" (Ionic Trace), "Pickup" (an Orb of Power) — in the order
they first fired; under each, the elements from there
(their icon and name, in their affinity's colour; the Orb of Power, which Bungie gives no icon, a ⚪), and under
each element every outcome it gave, as a bulleted list ("• +1 Bolt Charge"). A cascade ("↳ Bolt Charge gained")
nests under the event that set it off — a pickup picked up shows a 📍 instead ("📍 Picked up Orb of Power"), on its
own step as where Powerful Attraction collects it — and the same event several times in a row shows once, counted
(×3), with what the first one set off: an action against five enemies shows its first hit, then "Grenade 🥊 hit on Jolted
target ×4" with the "Bolt Charge gained" under it once, not five hits each followed by its cascade. A later one that
sets off less than the first shows in it too (Festival Flight's second kill, after the first consumed Reaper, only
gives Woven Mail again: "Festival Flight 💀 kill on Severed + Unraveled target ×2" with Reaper's orb and Woven Mail once),
whatever the earlier ones left on the target when it changes nothing they set off (a skip grenade's four kills, the
last three on a target the first one's Photonic Flare blinded, show as one "Grenade 💀 kill … ×4"); one that sets off
something new stays apart (the first grenade hit jolts the pack, the next ones on Jolted targets also set off Jolt's
strike). Kills of several enemies in one action say the tightest window a rule
they fired needs ("Festival Flight 💀 killed 2 enemies in 3 s": Strand Siphon). A hit is marked 🥊 and a kill 💀 in the event
too, as in the step's title. An
element's name shows a preview on hover and its details on a tap. Every keyword an event or outcome names — a
status, a debuff by its adjective ("Jolted"), a pickup, a summon — is in its affinity's colour wherever it is in
the line ("+1 **Bolt Charge**", "Grenade 💀 kill on **Jolted** target"), and the ones the rules have as an element
(Jolt, Bolt Charge, Ionic Trace, Combination Blow) preview and open the same way. A keyword shows the game's
own icon before its name ([rule-format.md](rule-format.md#glossary-rulesglossaryyaml), `trait`) — in the step's
text, in the state at its end and in its details (Unraveling Rounds, which Bungie's data has no icon for, shows the
shape the game draws for it, the glossary's `drawnIcon`: a triangle around a round, in Strand green); a step's title shows the icon of what it does (its weapon, its
ability, the pickup or the status it declares, the finisher the HUD shows), and an event that is a weapon's hit or kill shows that weapon's. An element
whose rules the engine fired apart (a debuff first, energy last) shows at each of its places, in that order. "why" adds each rule's reason under its element; "caveats" adds the outcomes'
caveats. Each step ends with its **state** — what is present after it — laid out like the game's HUD: the buffs
on you on the left (Bolt Charge, Amplified, Woven Mail), your weapons' buffs in the middle (a glossary
`weaponBuff`: Slice, Unraveling Rounds, each with the weapon whose own perk gives it — Slice with Festival Flight),
the debuffs on the pack on the right (Jolt, Sever, Unravel), the pickups on the ground below them (an Orb of
Power waiting for `pickup:orb-of-power`, "(chance)" when only chance rules dropped it; an Ionic Trace never waits, it
collects itself). A phone stacks the four. Armor Charge shows its count on its chip ("Armor Charge x1 · +1 (chance)")
and in a row under the ground; tapping the row (*▸ Armor Charge*) lists each step that gained or spent some since it
was last gained from none ("#6 Orb of Power: +1 → x1", "#5 Elemental Charge: at cap, +1 wasted → x3", "the pass
before: x2") and its decay, never applied; tapping it again hides them. The trace as text with its state prints the same as an
`Armor Charge` line. A status that switches other elements' passives on says how many ("Amplified · 1 effect": Spark of
Frequency's extra Bolt Charge); its details list them under **While Amplified** in their **In this build**
card, each with where it reaches. A passive that changes weapon stats (Ionic Overclock: Linear Fusion Rifles, Fusion Rifles and Heat
Weapons) shows in the weapons column next to the build's weapons of those types while it holds; when none of
them is one, the details say it does nothing here and Validation says so too. A status's own effect (Woven
Mail's damage resistance) is in its details, not in the state's chips: it counts in the **damage resistance** below
them. Every status opens its details on a tap — one the
rules have no element for (Armor Charge) shows the glossary's facts and what it switches on — and its facts ("up to
x5 · 8s") show on hover. Under them, the **damage resistance** right then (ADRs D29): a small table with a PvE and a PvP column. Its first row,
*All damage*, multiplies every resistance passive that holds — the Guardian's base 34.5% against combatants, Spark
of Resistance, Woven Mail while it is on you, Amplified's 15% against combatants, the Super's 90% while you're in it
(from its cast to `end:super`), Flow State's 66% on a dodge step while Amplified — the way the game stacks them, 1 − (1 − a) × (1 − b) × …: 72.98% PvE and
32.5% PvP with the first three (the base does nothing in PvP). Each resistance against some combatants only adds a row of
its own, PvE only (PvP 0%): *Arc combatants* (Arc Resistance, or Harmonic Resistance with an Arc Super), *Combatants
within 5 m*, *Combatants beyond 30 m*, *Combatants' area, blast and explosive damage*, all damage's sources and its own
together. A source whose value is "?" makes its total "?". Tapping the title (*▸ Damage resistance*) lists each row's
sources with their PvE and PvP values (each opens its details on a tap) and the formula; tapping it again hides them.
Under it, when any holds, the **damage bonus** right then: a row for each damage source a bonus makes harder (*Melee*:
Combination Blow while it is on you; *Bolt Charge*: Flashover, always; *Arc weapon*: Arc Weapon Surge while you hold
Armor Charge), with each source's own value, never added up ("+50%"; one the rules don't know, "?", never 0). Tapping
its title (*▸ Damage bonus*) lists each row's sources with their values, each opening its details on a tap; a status's own
bonus (Combination Blow's) shows here, not in its chip. The trace as text doesn't print it.
The totals are facts, never read to decide what a step fires; like every percentage the designer shows, they keep up to two decimals (72.98%, 11.25%). The trace as text with its state prints the same four lines, a
`Resistance` line (`PvE 72.98% · PvP 32.5%  (Guardian 34.5% × Spark of Resistance 25% [PvP 10%] × Woven Mail 45% [PvP
25%])`) and an `against …` line for each row of its own, then one `While <status>` line for each status that switches
something on.
Each step can be moved up or down, removed or dragged onto another step's place; the loop
replays in its new order.

## Starting from scratch

Home's **Start from scratch** button, above the DIM link, opens the designer on a new loop, *Arc Hunter loop*, for a
build named *Arc Hunter* (class `hunter`, subclass `arc`: Arcstrider) with nothing equipped: no super, grenade, melee or
class ability, no aspects, fragments, weapons, armor, mods, artifact perks or stats. Its build file names only that
(`name`, `class`, `subclass`, and a comment saying where it came from): an ability left out is an empty slot, not `"?"`
([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)), so the export and the share link carry it as it
is and it reopens empty.

The Subclass box shows the Arcstrider icon and a dashed square named for each empty ability (Super, Class ability,
Melee, Grenade); the Weapons and Armor boxes show their empty slots, as for every build in the designer (a slot
without a weapon or a piece is a dashed square named for it). The player fills it from there: **⋯** on the Subclass
box opens the subclass editor ([Editing the subclass](#editing-the-subclass)), and ⋯ next to each weapon and armor
slot picks one from every item of the game, or after a Bungie login one of their own ([Every item](#every-item)).

While the build equips nothing at all (no ability, named or "?", no aspect, fragment, weapon, armor piece, mod,
artifact or artifact perk, nothing left out), the **Next trigger** panel is greyed out, nothing in it can be tapped, and
a line says why ("Equip something first"); this holds for any build in that state. Anything equipped turns it on: the
finisher, a new pack and the declared states need nothing equipped, so a build with only a fragment (a rule scenario's
build, Spark of Volts) can tap the Finisher. Once the editor gives it an ability, the panel offers it; an ability slot
still empty is a dashed square named for its kind in the HUD too (an ability is always possible in the game, ADRs D5,
so it can still be cast — it sets off what reacts to any cast).

## Starting from a DIM link

The web designer's Home starts a loop from a DIM link ([ADRs D27](ADRs.md#d27--a-dim-link-starts-a-build-of-what-the-catalog-knows-by-hash-the-rest-is-left-out)):
paste it, press the button, and the designer opens with the loadout's build, named after the
loadout, its link as the build's `source` (the designer shows both). It reads the two kinds of link
DIM itself opens:

* **A dim.gg share** — what DIM's Share button copies (`https://dim.gg/4j5nz4q/Skip-Grenade`, or just
  the id). The loadout is on DIM's servers: the web app asks DIM's API for it
  (`GET https://api.destinyitemmanager.com/loadout_share?shareId=…`, open to any caller: no key, no
  header — [hosting.md](hosting.md#dim-links)). When DIM can't be reached or doesn't hand it over, the
  message says how to get the other kind: the dim.gg page's "Open Loadout in DIM" link carries the whole
  loadout (copy that link and paste it instead); a share DIM doesn't know is said so.
* **A link that carries its loadout** — DIM's Loadouts page with the loadout as JSON in `?loadout=`
  (D2ArmorPicker, guardian.report): read in the browser, nothing is fetched.

Anything else is `That isn't a DIM link …`. Items the manifest excerpt lacks are read from Bungie's full
manifest first, which the tab loaded when the app opened (the button says so — [hosting.md](hosting.md#bungies-manifest)),
and the same happens when a shared loop names them. The loadout's items are named only by manifest hash, so
the build holds what the catalog recognises by hash, and nothing is guessed:

| From the loadout | Becomes |
|---|---|
| the subclass item | `class` and `subclass` — the glossary's `subclasses` ([rule-format.md](rule-format.md#glossary-rulesglossaryyaml): the nine Light ones, whose hashes DIM's source lists, and the six Stasis and Strand ones); a Prismatic loadout, or one without a subclass, can't start a build yet |
| the subclass's plugs, in socket order | the super, grenade, melee and class ability, the jump, aspects and fragments the catalog has a hash for, for the loadout's class and subclass (another class's, such as the Hunter's Spark of Shock on a Striker, is left out); an ability it has none for is `"?"`, a jump it has none for is left out (it has no part in a loop) |
| the other equipped items (and the Loadout Optimizer's exotic) | `exoticArmor`; a weapon the manifest excerpt knows ([rule-format.md](rule-format.md#manifest-excerpt-rulesmanifestyaml)) becomes one of `weapons` — its `hash` (its slot, name, damage type, ammo type and archetype are the manifest's), and as its `roll` the perk each trait column rolls with among the item's `socketOverrides` (a loadout saved with its perks; most name the weapon only) |
| the armor pieces, by their manifest set | `armorSetBonuses`: each set perk the pieces grant (2 or 4 of a set) that the catalog has a hash for; the pieces themselves (legendary armor) are left out |
| `parameters.mods` (repeats are stacked copies) | `armorMods` |
| `parameters.artifactUnlocks` | `artifactPerks` |
| anything else — legendary armor, a jump the rules don't have (another subclass's), a weapon the excerpt doesn't know, a hash no element has | `leftOut`, by hash and where the loadout listed it |

The loadout's name becomes the build's, without Destiny's icon glyphs (DIM draws them with its own
font: "Arc  - Skipp grenade" reads "Arc - Skipp grenade"). The build is written as an ordinary build
file (comments say where it came from), so the loop
embeds, exports, shares and replays it like any other ([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)).
The designer shows the build the way DIM shows a loadout — the subclass with its super, abilities,
aspects and fragments, the artifact (the one the build names, else the one that unlocks the most of the build's
artifact perks — none on a tie — tappable for Bungie's text and which of its perks the build has) and its perks, the weapons (kinetic, energy, heavy) with their element and
one perk per trait column the manifest excerpt gives — the build's pick (`roll`), else the perk it names
in that column, else the one perk a column that doesn't roll has (an exotic's), else "?" — each armor piece with the mods
of its slot (the slot the manifest gives the mod; other mods are general) — with Bungie's icons from the
manifest excerpt; what the rules don't know sits where DIM would show it, its icon as bright as any other's, with a
small grey ⊘ in its lower-right corner ("Not in Loopsmith yet: it sets nothing off in your loop"). It also lists the
left-out items under **Not in Loopsmith yet** (collapsed, with how many of each part: open it to see them), by their
manifest name, each a link to its light.gg page. The ⊘ and that list say it all: Validation adds no line for what the
rules don't know yet ([rule-format.md](rule-format.md#elements): an element is in the loop, outside it on purpose, or
not authored yet).
What the manifest says has no part in a loop is not a gap, and it isn't listed: its tile in the loadout has no ⊘,
and its tooltip says why — a stat or tuning mod (Grenade Mod: +10 Grenade; Balanced Tuning — it changes only stats,
which the build's stats count when they add up from the player's armor, [Stats](#stats)), a legendary armor piece
(its set's bonuses already count from the pieces worn; the piece carries stats and mod slots) or a jump the rules don't
have. An element the rules mark `inert` ([rule-format.md](rule-format.md#elements): Skip Grenade, Feeding Frenzy, the
ammo, weapon-handling and resistance mods, the Arcstrider's jumps) is known: its details say "No rules needed." and why, and the
build check doesn't warn about it. A raid's, an activity's or a seasonal mod isn't supported: it is kept in `leftOut`
but ignored, faded with a ✕ instead of ⊘, not listed under **Not in Loopsmith yet**, and Validation says once what it
ignored ([Raid, activity and seasonal mods](#raid-activity-and-seasonal-mods)). A loadout for another class than its subclass's is an error, and so is text that
isn't a loadout.

**Picking a weapon's perks.** A DIM link usually carries the weapon, not its roll, so the designer lets you pick
it: click a perk square (a column that rolls) and choose among what the manifest says that column rolls
with (enhanced versions included; the ones the rules know are marked), or **Clear** it back to "?". An
enhanced trait is drawn as the game and DIM draw it — Bungie gives it the base perk's image — with a gold glow over
the circle's lower half, a gold bar down its left side and a gold up-arrow, on the weapon's perk square, in the
picker and in a step's text ("Slice (enhanced)"). The
pick goes into the embedded build's weapon entry as `roll` ([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)) —
only that entry is rewritten, so the build file's comments stay — and the steps replay with the new
build: a perk the rules know now sets things off; one they don't is shown with its ⊘, and said so under
Validation. Hand-written builds pick the same way; a perk they name in `perks` sits in its column and
gives way to a pick there. The Home cards stay read-only. After a Bungie login, the perks the player's own copy doesn't
have are marked, and picking one is warned about ([Weapons from your account](#weapons-from-your-account)).

**A weapon's mod.** A weapon with a mod socket (a legendary; most exotics have none) shows its mod as a square after
its perks — dashed with a + while the socket is empty. A DIM link gives it when the loadout saved it, and so does a
weapon picked from the player's own. In the designer, clicking it opens the **weapon mod picker**, under the weapon: every mod Bungie's
manifest says that weapon's mod socket takes — not marked in green as perks are: the rules know every mod the
Compendium lists, and one they don't (an old Adept mod) carries its ⊘ — the enhanced ones (framed in gold)
listed too but dimmed and not clickable unless the weapon is known to be Tier 4 or 5 — after a Bungie login, the
player's copy's tier says; without one, an enhanced mod can't be picked here (a DIM link or a build file can still name
one). **Remove mod** empties the socket. The pick goes into the embedded build's weapon entry as `mod`
([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)), only that entry is rewritten, and the steps replay.
Almost every mod is inert (stats, handling, ammo, radar: "No rules needed."); **Synergy** fills a counter with this
weapon's kills and spawns a pickup of the weapon's element — Kinetic an Orb of Power, Arc an Ionic Trace, Solar a
Firesprite, Void a Void Breach, Stasis a Stasis Shard, Strand a Tangle. Twelve kills in one step (eight with the
enhanced mod) fill it for sure; fewer spawn the pickup as *(chance)*, since earlier steps' kills count too. The trigger
palette's enemy count offers 12 · Synergy on that weapon (and any count an equipped rule needs from it) beside 1 to 9.

**An item's details.** Tapping a square opens its details, under its name and type line (a weapon's names its ammo
type, the manifest's: "Exotic Machine Gun · Heavy"), one card each: Bungie's text (from the manifest
excerpt); where the rules' facts come from — for a Compendium row, Loopsmith's words for it (written from the row,
never copied), its tab and row ("Exotic Weapons tab, row 144 · snapshot of 2026-10-09") and a link to the sheet;
Clarity's entry, a creator's claim (their words and link) or Loopsmith's own rule (its file and line) otherwise;
and Loopsmith's rules: what it sets off in this build trigger by trigger — or that the rules don't know it yet.
A keyword (a buff, a debuff, a pickup) also gets an **In this build** card: the build's other elements that deal
with it, by role — what gives it (applies it, creates it), reacts to it, uses it up, needs it, strikes with it,
changes it, and the passives switched on while it is there. Armor Charge: Elemental Charge and the Orb of Power
give it, Grenade Kickstart uses it up. Its own rules stay in the rules card.
Every keyword in it (Bolt Charge, Jolted, Ionic Trace) is in its affinity's colour. The name links to the item's light.gg page, and so
does a weapon's name in the loadout. An exotic armor shows its exotic perk; an exotic weapon its intrinsic,
each perk its columns hold and its catalyst. Legendary armor and weapons open nothing: their perks and mods
carry what there is to know. The **armor sets** sit under the armor: each set with the pieces worn (a DIM
loadout's) and its 2- and 4-piece perks, dimmed when not active, each with its own details.

**An exotic's catalyst.** An exotic weapon with a catalyst shows it as a square after its perks, off until the
build turns it on (a DIM link doesn't say). In the designer its details switch it on or off: the embedded
build's weapon entry gets `catalyst: true`, the catalyst's element is equipped, and the steps replay. Thunderlord's
Return Stroke refills it on each lightning strike (a kill, or a hit step's 10th hit: ADRs D42). A catalyst outside the
loop is shown, and its details say "No rules needed." and why; one the rules don't know yet carries the ⊘. Validation says nothing of either.

**Shared DIM builds on Home.** A share can ship with the app as `builds/<slug>/dim-loadout.json` —
its link and the loadout as DIM's API hands it over (the share page's "Open Loadout in DIM" link holds
the same as `?loadout=` JSON, without the items' instance ids):

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

## Editing the subclass

The designer's Subclass box has **⋯** next to its title, for every build (the Home cards stay read-only). It opens the
subclass editor, laid out like the game's subclass screen: the subclass's icon, its name in capitals and "Hunter
Subclass" as the header (the subclass's own name, Strand, and no icon when the manifest excerpt doesn't know its item);
the super on the left; Abilities (class ability, jump, melee, grenade) and the two Aspects in one row beside it;
Fragments under them. At phone width the super sits above the groups, and a group that doesn't fit its row wraps. Each
group is a grey panel of squares, and each square has a footer with its **⋯** (and a fragment's ✕). Tapping a plug's
square (or the subclass's icon) opens its details, and the slot's **⋯** opens a dropdown under that slot (above it when
the screen has no room below; it may spill out of the editor),
one option per line, with everything the subclass can hold there — Bungie's manifest, with icons and names. The ones the
rules don't know carry ⊘ (every Arcstrider plug is known, its four jumps `inert`), except a jump the rules don't have
(another subclass's), which carries none: it has no part in a loop. The dropdown floats over the slots after
it, so none of them moves. One dropdown is open at a time; picking a plug puts it in the slot and closes it, and ⋯ again, a
tap outside it or Escape closes it without a change (Escape closes only the dropdown, not the editor). Hovering over
a plug (an option, a slot of the editor, or a square of the Subclass box) shows its name and Bungie's description of it
(the manifest's; the rules' when the excerpt has none), and for a ⊘ plug why it is marked. An aspect
or a fragment can't be equipped twice; ✕ leaves a fragment's slot empty and the other fragments stay in their slots
(Save writes them in slot order: the build keeps no places, so the editor shows them packed next time), while an aspect
has no ✕: it is only swapped for another. The aspects open the fragment slots (`fragmentSlots` in the rules: Tempest Strike
and Flow State 4, Tempest Strike and Ascension 5); the count follows the aspects at once. An aspect whose count the rules
don't give shows "?" slots and limits nothing. An aspect change that closes slots takes out the last fragments and names
them; the fragments left keep their slots, or are packed from the first one when a closed slot held one. A build with more
fragments than its aspects open (a build file or a DIM link: build validation only warns) shows them all, and a warning
under Fragments says Save needs aspects that open enough slots, or fewer fragments. **Cancel**, ✕ or Escape throws the changes away.

**Save** rewrites only the embedded build file's `super`, `grenade`, `melee`, `classAbility`, `jump`, `aspects`,
`fragments` and `leftOut` (a plug the rules don't know is in `leftOut.subclassPlugs`, shown ⊘ and listed under "Not in
Loopsmith yet"; so is a jump the rules don't have, which isn't ⊘ or listed; a jump the rules know is `jump:`, and a
build file that still lists one under `leftOut.subclassPlugs` opens with it and is saved with it as `jump:`; an entry
the file didn't have is added at its end); the build check runs again and the steps replay with the new build, in place. The Next
trigger HUD, the Validation list and **Analyze** follow it. When the stats were added up from the player's armor
(they equal what that armor gives for the old subclass), they are added up again with the new fragments (Spark of Shock
gone gives back its −10 Grenade); stats the player typed stay as typed. The export and the share link carry the new
subclass. What the subclass can hold comes from the manifest excerpt (`plugs` of the subclass item, written by
`tools/manifest/` for each of the glossary's fifteen subclasses). A plug is the rules' element only when the rules know it
for the build's class: the rules author Skip Grenade and the Sparks for the Hunter, so on a Striker or a Stormcaller
they are left out (⊘), in the editor and from a DIM link alike.

## Artifact

In the designer the **Artifact** box has ⋯ (also on a loop from scratch, whose box shows an empty artifact): it opens
the **artifact editor**, laid out like the game's artifact screen since Monument of Triumph (Artifacts 2.0). The Home
cards stay read-only.

* **The artifact.** The header shows the artifact's icon, its name and "Artifact"; ⋯ beside the name opens a dropdown
  of the seven artifacts (NPA Repulsion Regulator, Queensfoil Censer, Hunter's Journal, Slayer Baron Apothecary Satchel,
  Tablet of Ruin, Implement of Curiosity, Encrypted Data Disk), each with how many of its perks are picked, and "No
  artifact". With no artifact the editor lists them in its body. One artifact is equipped at a time.
* **Under the header**, as in the game, its flavour text and what it works best with (an element with its icon, a
  weapon type by name).
* **Its seven perk slots** stand in three groups under the game's marks: two take Tier 1 (◆), three Tiers 1–2 (◆◆),
  two Tiers 1–3 (◆◆◆) (the excerpt's `slots`, [rule-format.md](rule-format.md)). Each slot is a square with a footer
  holding ⋯ and, when it holds a perk, ✕, like the subclass editor's fragments. **⋯** opens a dropdown under the slot
  (above it when there's no room below), floating over the slots after it: every perk the slot takes, under a heading
  per tier ("Tier 2 ◆◆"), each with its icon, name and tier; a perk in another slot is greyed out ("equipped"), a perk
  the rules don't know carries ⊘ (it sets nothing off) and can still be picked. Picking one puts it in the slot; ⋯
  again, a tap outside it or Escape closes it. **✕** empties the slot; the other perks stay in theirs. Tapping a perk's
  square opens its details. A slot only offers what it takes, so nothing is counted: at most 7 perks, 5 of Tiers 2–3
  and 2 of Tier 3 follow from the slots.
* **Switching artifact** shows the slots last picked for that artifact while this loop is open in the designer
  (starting from the loop's own artifact and slots); one not set up yet starts with empty slots. Switching back brings the
  earlier picks back. They are remembered for the tab only, not saved in the loop file or the browser; opening another
  loop starts afresh.
* **Save** rewrites only the embedded build file's `artifact` (its hash and the perk in each slot, `"?"` for an empty
  one; removed for no artifact), `artifactPerks` (the rules' perks) and the artifact perks in `leftOut` (the others),
  each in slot order, so the perks come back in the slots they were put in; the build check runs again and the steps replay
  (take Flashover out and maxing Bolt Charge no longer makes you Amplified). The export and the share link carry them.
  **Cancel**, ✕ or Escape throws the changes away (Escape closes only the dropdown when it is open). A loop that
  doesn't name its artifact (a build file, a DIM link) opens with the one that unlocks the most of its perks, each placed
  in the free slot that takes the fewest tiers (the highest tiers first).

## Armor mods

In the designer every armor piece shows its **mod sockets** next to it, and its energy under them ("Energy 7 / 10";
"7 / ?" until the player's Bungie login gives the capacity of their copy of the piece, [Stats](#stats)); the
**General mods** row under the armor holds every piece's general socket. The Home cards stay read-only: they show the
mods as before, each piece with the mods of its slot and the general mods together ([ADRs D30](ADRs.md#d30--armor-mods-are-picked-in-the-pieces-sockets-the-energy-budget-holds-when-its-capacity-is-known)).

* **The sockets** are the game's, from Bungie's manifest (the listing read when the app opens, with every weapon and
  armor piece): a piece's general socket, the sockets of its slot's mods (three on an Armor 3.0 piece, two on an older
  one) and its artifice socket when it has one. A raid's or an activity's socket isn't shown: Loopsmith doesn't support
  their mods ([Raid, activity and seasonal mods](#raid-activity-and-seasonal-mods)). A slot with no piece whose sockets Loopsmith can
  read — none worn (a loop from scratch), or a piece the listing lacks — holds the mods the build has for it and one free
  socket, and gives the General row one socket. An empty socket is a dashed square named for what it takes
  ("Helmet", "General", "Artifice"); a filled one shows its mod, ⊘ when the rules don't know it, and its
  energy cost in its corner. On a phone (640 px wide or less) the sockets are as big as a piece's square, four to a
  line, so a piece's artifice socket stays on its row.
* **Placing.** A build holds its mods as a list, as a DIM loadout does (`armorMods`, `armorModPlugs` and the mods in
  `leftOut`), so the designer places them: each in the first free socket of the pieces that takes it, in the build's
  order; the general mods fill the General row and count against the pieces with the most energy left, the costliest
  first (as DIM assigns them). A mod no free socket takes (more mods than the pieces hold) shows under **Mods with no
  socket**; one that goes in no mod socket (a tuning mod, in a Tier 5 copy's tuning socket) under **Tuning and other
  mods**; a raid's, an activity's or a seasonal one under **Not supported**. All stay in the build.
* **The mod picker.** Tapping a socket opens it: every mod of the game the socket takes, by name, the subclass's
  element first (Arc Siphon and Arc Targeting on an Arc build) with a filter for them only ("Arc only"; none for
  Prismatic or a socket without elemental mods), and a search box. Each shows its icon, its energy cost and what
  Bungie says it needs ("Must Be Selected in the Seasonal Artifact": the cheaper copy of a mod; Guardian Rank aside);
  a mod the rules don't know carries ⊘ (it sets nothing off), as in the loadout (no colour: every slot and general mod
  is in the rules, so only what is missing is marked), and a mod that changes only
  stats (a general or artifice mod) needs no rule. A mod whose cost is more than the piece has left, when its copy's
  capacity is known, can't be picked and says why ("Solar Siphon costs 3 energy: the helmet has 2 left"; for a
  general mod, that no piece has that much left); without a login nothing is refused. Picking one puts it in that
  socket, in place of the one there; **Clear this socket** takes it out; **About …** opens the details of the mod in
  the socket. Picking the same mod again stacks its copies (Elemental Charge ×2). No picker offers a raid's, an
  activity's or a seasonal mod.
* **What changes.** Only the embedded build file's mod entries: `armorMods` (the rules' mods), `armorModPlugs` (the
  exact copy, for a mod with a cheaper one: [rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)) and the
  mods in `leftOut`; Bungie's manifest is read first for a mod the excerpt lacks (its name and icon). The build check
  runs again and the steps replay; stats added up from the player's armor are added up again (a stat mod changes
  them), typed ones stay. The export and the share link carry the mods.

### Raid, activity and seasonal mods

Loopsmith doesn't support the mods of a raid's socket (Salvation's Edge, Crota's End, Root of Nightmares, King's Fall,
Vow of the Disciple, Vault of Glass, Garden of Salvation, Deep Stone Crypt), an activity's (Last Wish, the Nightmare
Hunts, the Dreaming City) or a retired season's (Black Armory, Opulence, Undying: Bungie deprecated them, they no
longer work) ([ADRs D35](ADRs.md#d35--raid-activity-and-seasonal-armor-mods-arent-supported-a-dim-links-are-kept-but-ignored-and-no-picker-offers-them)).
They act on an encounter's mechanics (the Relic, a Vex relay, Taken enemies), which a loop doesn't hold. Bungie's plug
category says which mods they are (`enhancements.raid_*`, `enhancements.season_*`, `enhancements.rivens_curse`), not
their names, so a new raid's are covered too.

* **No picker offers them**: a piece's raid or activity socket isn't shown, and the mod picker lists none of their mods.
* **A DIM link that has them** keeps them in the build's `leftOut`, as the loadout had them, so a shared loop still
  shows them — but they are ignored: they set nothing off and take no socket. Their square is faded and grey, with a
  small ✕ where ⊘ goes ("Not supported: raid, activity and seasonal mods are ignored."); on the Home card it sits with
  the general mods, in the designer under **Not supported**; **About …** says the same. They aren't counted under
  **Not in Loopsmith yet** (they aren't a gap), and the build check says once what it ignored, as info:
  `Ignored from the DIM loadout: Herd Thinner ×2, Taken Barrier — raid, activity and seasonal mods aren't supported.`

## Stats

A build's `stats:` are its six armor stats (`health`, `melee`, `grenade`, `super`, `class`, `weapons` — the game's
order, which the designer shows as bars and Loopsmith writes; 0..200; one the build doesn't give is "?"). They are facts of the build, never counted down: the engine simulates no
energy (ADRs D1, D5). What they change is said by the build check, from the elements' `statChecks`
([rule-format.md](rule-format.md#elements)) — Gambler's Dodge gives back 1% of a melee charge per Melee point,
twice that within 15 m of an enemy, so below 50 Melee it warns that even near an enemy the melee doesn't come back
in full (and from 50 to 99 it says that only near an enemy it does); a Combination Blow kill refills the class
ability in full only from 70 Class.

In the designer the Stats section sets them, into the embedded build's `stats:` (only that line changes), and the
steps replay:

* **Typed** — "Edit": six inputs, a whole number 0..200 each, or empty for "?".
* **From the player's own armor** — "Log in with Bungie" ([hosting.md](hosting.md#bungie-login)). Loopsmith asks
  Bungie for read-only access and reads the player's armor and weapons (worn, carried and in the vault). A dim.gg share names
  the copy of each piece it wears (its instance id, kept as the build's `armorCopies`), so:
  * **their own share** — every copy is in their account: the build wears those pieces, and its stats are set from
    them at once;
  * **anyone else's** (or a link that names no copies) — nothing changes yet: they pick their own pieces with ⋯.

  The Stats section then says only that, in one line ("Your armor is loaded…", or "You can now pick your armor: tap
  ⋯…"), with **Log out**; it shows no total of its own. Log out forgets the player's armor: the build keeps the
  stats and pieces it has, the pickers list [every item](#every-item) only, and the login text and button come back.
  After the login the **armor picker** (⋯ next to each armor slot) opens on **Yours**: the player's pieces for that
  slot (for the build's class; Bungie's icons), the copy the build
  wears first (✓ in its square's lower-right corner, its stats shown), then its other copies, then the rest by name.
  Hovering a piece (or a first tap, on a phone) shows its stats, its set and where it is; a click (a second tap, or "Wear this piece") wears it. The piece
  then is the build's: an exotic the rules know is its exotic, any other piece a left-out item; its copy goes into
  `armorCopies`; its **set bonuses follow the pieces**: two pieces of another set leave Luminopotent's 2-piece bonus
  only, and the other set's 2-piece perk shows too. The steps replay, and the stats are set from the armor once every
  slot has a piece the player owns (a piece not picked yet counts as their copy with the most points of the same item).
  A build wears one exotic: an exotic is listed only in the exotic's own slot (the picker says how to wear one
  elsewhere). Pieces the excerpt doesn't know are named by Bungie's full manifest, read once after the login; one it
  can't place is counted, not listed. The stats are added up the way DIM adds up a loadout, from the manifest
  excerpt's `statPlugs` ([rule-format.md](rule-format.md#manifest-excerpt-rulesmanifestyaml)): each copy's roll (its
  armor-stat plugs) and masterwork (Armor 3.0's adds only to its stats at 0; Armor 2.0's +2 to each at full energy);
  the build's stat mods (Grenade Mod ×5: +50 Grenade) and tuning (Balanced Tuning: +1 to a piece's stats at 0, one per
  tuning socket — one with no socket left adds nothing); and its fragments' stats (Spark of Shock: −10 Grenade). A
  build that names no piece for some slot (a hand-written one) gets its stats this way once a piece is picked for each
  with ⋯; a piece whose roll the excerpt doesn't know leaves the stats as they were.

**Tiers and masterworks.** After the login the loadout shows the player's copies the way DIM does: each armor piece
and weapon the build wears shows its copy's **gear tier** (Bungie's diamonds down the square's left edge; tier 1 has
none), and a **masterworked** copy has a gold border and the masterwork glow (an exotic's its own); the armor picker's
pieces show theirs, and the piece shown says it ("Tier 5 · Masterworked"). The copy is the one the build names when
it is the player's (their own share, or a piece picked; a weapon's `instance`), else their own copy of that item, as
DIM resolves a shared loadout's items: for armor the one the picker ticks (the most points), for a weapon its highest
tier. Each square is drawn as DIM draws it: its **season watermark** over it (the season's or expansion's icon in
the top-left corner), and in the top-right corner a legendary weapon's **frame** (Adaptive Frame; an exotic shows
none) or, after the login, an Armor 3.0 piece's **stat focus** (its highest rolled stat's icon). Under each weapon's
square a light footer (gold for a masterworked copy) shows its **breaker** (a champion icon: the copy's, after the
login, else the weapon's own — Thunderlord's Disruption — or its frame's: Adaptive Frame pierces barriers, Rapid-Fire
Frame overloads; shown before the login too), its **element** and, after the login, the copy's **power**; an armor
piece gets the footer with its power after the login. The pickers' items show the same. The icons and overlays are
the manifest excerpt's (`breakerTypes`, `armorStats`, an item's `watermark`, `itemConstants`); the tier, masterwork
flag, breaker and power are Bungie's profile's for the copy (its instance's `gearTier`, `breakerTypeHash` and
`primaryStat`, the item's `state`). The pages carry `<meta name="darkreader-lock">`: the site is dark already, and a
dark-mode extension such as Dark Reader would otherwise repaint the light footers dark and hide their icons.

<a id="weapons-from-your-account"></a>**Weapons from your account.** The login reads the player's weapons too, with the
plugs in their sockets and, socket by socket, the perks each copy can switch to (Bungie's reusable plugs: a column that
rolled two perks or more has each of them):

* **The build's own copies** — a weapon whose `instance` (a dim.gg share's) is theirs takes that copy's perks: in each
  trait column, the plug of its sockets the column rolls with (a share saves only the perks it was told to; the copy
  has them all), `catalyst: true` when its catalyst socket holds the catalyst, and its mod. Only those entries of the embedded
  build change, and the steps replay. The Stats section's line says so ("Its weapons are yours too, with their
  perks."), or, for weapons that aren't theirs, to pick their own with ⋯.
* **⋯ next to each weapon slot** opens the **weapon picker** for it, after the login on **Yours**: the player's
  weapons for that slot, the copy the build names first (✓ when it is theirs; for someone
  else's share none is ticked, so their own copy can be used to load its perks), then that weapon's other copies, then
  the rest by name; each with its tier, masterwork, breaker and element as in the loadout. Hovering one (or a
  first tap) shows its type, tier and its perks column by column, the columns side by side (a column that rolled two
  or more stacks them top to bottom, framed together, the one it holds on top on DIM's blue; on a phone the perks show
  as icons only, their names in their tooltips), and where it is — the details keep room for the most perks a column
  of the listed weapons holds, so "Use this weapon", in their upper right corner, stays put from one weapon to the
  next; a click (a second tap, or "Use this weapon") puts it in the build: its entry is written anew (`{hash, perks: [], roll,
  catalyst, instance, mod} # <its name>`, from the manifest and its sockets), or added for an empty slot, and the steps replay. A build
  wears one exotic weapon (by its manifest item, or an exotic weapon element among a hand-written weapon's perks):
  another exotic isn't listed while it does (the picker says which to swap); and one weapon a slot. Weapons the
  excerpt doesn't know are named by Bungie's full manifest, which the tab holds, once after the login with the armor
  (the Bungie login section shows its progress bar; the page keeps working meanwhile — [hosting.md](hosting.md#bungies-manifest));
  one it can't place is counted, not listed.
* **Perks your copy doesn't have** — the perk picker still offers every perk a column rolls with. For a weapon whose
  copy (its `instance`: picked from Yours, or the player's own dim.gg share's) is theirs, each perk the copy doesn't
  have in that column carries a small amber **!** in its square's upper-right corner (clear of the ⊘ in the lower
  right and of an enhanced trait's arrow on the left) and "Not on your copy" under its name. Picking one puts the same
  **!** on the weapon's perk square and a yellow warning under Validation, one line per perk, whether or not the rules
  know it ("Festival Flight: your copy doesn't have Hatchling in column 2 (it has Elemental Honing (enhanced) or
  Attrition Orbs)."; "Festival Flight: your copy doesn't have Slice (enhanced) in column 1 (it has Slice)."). Each perk of a column that rolled two or more is the copy's; so is a base perk whose
  enhanced version the copy has (the enhanced trait does all it does), but not an enhanced perk whose base the copy
  has. A weapon picked from Every item or written in the build file isn't compared, and nothing of it is saved: the
  marks and the warning go when the player logs out.

Nothing of the login is kept (the player's armor and weapons stay in the tab until they log out or it closes); the
stats and the copies are, in the loop's build, so a shared loop carries its author's numbers and pieces.

<a id="every-item"></a>**Every item.** In the designer every weapon slot (kinetic, energy, heavy) and every armor slot
(helmet, arms, chest, legs, class item) shows, an empty one too, each with **⋯**, with or without a login (Home's
cards stay read-only). Without a login ⋯ opens its picker on **Every item**; after one, a **Yours / Every item** switch
picks between the player's own items (above) and these, starting on Yours:

* **The list** — every legendary and exotic weapon of the game for that slot, or every legendary and exotic armor piece
  for that slot and the build's class: every version of each (a season's reissue is an item of its own, told apart by
  its watermark and, for armor, its set: one version may be in an armor set another isn't), the one the slot holds
  first (✓), then by name, a name's versions side by side. A search box narrows it as you type, by name or type ("hand
  cannon", "arc"; for armor its name or its set's); for weapons, a row of weapon type chips and one of element chips
  narrow it too (one chip of each row at a time, combined with the search). An exotic the rules don't know yet
  carries ⊘ (an exotic weapon is known by its intrinsic perk, an exotic piece by its hash: a version the rules don't
  name carries it too); a legendary never does, its perks and set bonuses show what the rules know once it is worn.
  Each weapon's footer shows its damage type and its breaker (its
  own, else its frame's: Adaptive Frame pierces barriers), and the item shown is framed with its footer. The list comes
  from Bungie's item table, listed once when the app opens (its startup screen; a later visit reads the list saved in
  the browser while the manifest's version holds;
  [hosting.md](hosting.md#bungies-manifest)), so a picker opens at once. The one-exotic rule holds as
  in the player's pickers: no exotic is listed while another slot holds one (the picker says which to swap).
* **A pick** — hovering an item (or a first tap) shows its name, type and damage type (for armor, its set); a click (a
  second tap, or "Use this weapon" / "Wear this piece") puts it in. Bungie's full manifest is read for it first when
  the excerpt lacks it (its trait columns, catalyst or set): the picker shows the bar and stays open, and when it can't
  be read it says so and the build doesn't change. A **weapon**'s entry is then written anew, or added for an empty
  slot, as `{hash, perks: []} # <its name>`: no roll (each rolling column shows "?", picked with the
  perk picker; a column that doesn't roll shows its perk) and its catalyst off. A **piece** goes into its slot as one
  picked from the player's armor does (an exotic the rules know is the build's exotic, any other piece a left-out item,
  the set bonuses following the pieces), with no copy: the stats stay as they were.
* **Exotic class items** (Relativism, Stoicism, Solipsism) are listed like any exotic, but their two random perks
  (spirits) can't be picked yet: the build wears the item without them.
* **Empty this slot**, at the foot of a filled slot's picker, takes the weapon (with its perks) or the piece off: a step
  that uses that weapon slot becomes a blocked step, an exotic taken off is no longer the build's exotic, and the set
  bonuses follow the pieces left.

The steps replay after each change, and the loop's build carries it (export, share link).

## Sharing

* **File:** export / import the `.loop.yaml`.
* **Link (web app):** `<app address>#loop=<payload>` where `payload` = base64url (no padding) of the
  raw-DEFLATE-compressed UTF-8 loop file. Opening the link imports the loop into the designer.
  Everything stays in the URL fragment — nothing is sent to a server. A payload that doesn't decode
  is an error (`The link's loop payload is damaged …`), and so is one over 2 MB once decompressed.

## Analysis

The analysis follows the loop's order (the web designer's Analysis view).
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
  * a pickup it picks up: `Orb of Power ← #3 [Reaper; Strand Siphon (chance)]`
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

**The report as text** (the web lays the same out and shows this text under
*The report as text*). Illustrative, `…` elides:

```
Dodge, then shoot — Skip Grenade Hunter · 4 steps
New pack → Class ability → Festival Flight (💀 kill) → Pick up Orb of Power
✓ Repeats — each pass ends with what the next one needs

#1 New pack

#2 Class ability
   on its own       Reaper · Slice · Gambler's Dodge · Bomber

#3 Festival Flight (💀 kill)
   needs            Slice ← #2 [Slice] · Unraveling Rounds ← previous pass #4 [Unraveling Orbs] · Reaper ← #2 [Reaper]
   in this order    Slice · Unraveling Rounds · Reaper · To Shreds
   on its own       Strand Siphon (chance)

#4 Pick up Orb of Power
   needs            Orb of Power ← #3 [Reaper; Strand Siphon (chance)]
   in this order    Unraveling Orbs · Orb of Power

First pass (from a fresh spawn, where it differs)
#3 Festival Flight (💀 kill)
   doesn't set off  Unraveling Rounds
```

## Comparison

Two loops' **orders** side by side (the web Compare page). The loops may use different
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
A  Dodge, then shoot   New pack → Class ability → Festival Flight (💀 kill) → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)
B  Shoot, then dodge   New pack → Festival Flight (💀 kill) → Class ability → Pick up Orb of Power   ✓ Repeats   (Skip Grenade Hunter)

New pack                A #1 · B #1: sets off the same
Class ability           A #2 · B #3: sets off the same
Festival Flight (💀 kill)  A #3 · B #2: sets off the same
   A needs     Slice ← #2 · Unraveling Rounds ← previous pass #4 · Reaper ← #2 (differs on A's first pass)
   B needs     Slice ← previous pass #3 · Unraveling Rounds ← previous pass #4 · Reaper ← previous pass #3 (differs on B's first pass)
Pick up Orb of Power    A #4 · B #4: sets off the same
   A needs     Orb of Power ← #3
   B needs     Orb of Power ← #2

Links only in A
   #2 Class ability → #3 Festival Flight (💀 kill): Slice · Reaper

Links only in B
   previous pass #3 Class ability → #2 Festival Flight (💀 kill): Slice · Reaper
```

A trigger that sets off different elements in the two loops lists them as `only in A` / `only in B`;
one only a loop has reads `only in A (#7)`.
