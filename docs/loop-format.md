# Designed loops: file format (v1), starting from scratch or a DIM link, sharing, analysis, comparison

**The product of Loopsmith is a loop someone designs**
([ADRs D2](../ADRs.md#d2--the-designed-loop-is-the-product)): an ordered list of steps, each a trigger
("dodge", "throw a grenade and kill three") or a state the player declares ("Bolt Charge at max",
D3). You pick a build, choose a step, see everything it sets off and what is available next, choose
again, and so on — the build can come from a DIM link, or start from scratch. Loopsmith describes cause and effect
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

`grenade[:hold]|melee[:slide|air]|super[:hit|kill[:N]]` · `class[:air]` · `kinetic|energy|power[:hit|kill[:N]]` · `finisher` ·
`pickup:<pickup-id>` · `max:<status-id>` · `end:<status-id>` · `pack:new`

Tokens are read case-insensitively. Errors read `Unknown action '<token>'. Use …` or
`Invalid target count in '<token>': use a whole number from 1 to 20 …`.

**Triggers.** An ability or weapon action without `:kill` only damages. `N` is how many enemies that
one action hits (or kills), 1..20, default 1 — the player says it, the engine can't know (ADRs D4):
`grenade:kill:3` kills three, `kinetic:hit:5` shoots five, `energy:kill:2` kills two. A count needs
`:hit` or `:kill` before it (`grenade:3` is an error), and `class` takes no count. A **secondary ability
use** goes between the ability and its hit (ADRs D9): `melee:slide:kill` is the melee while sliding
(Tempest Strike's uppercut), `melee:air` the melee in the air, `grenade:hold` the grenade held (Chaos
Accelerant, Bleak Watcher), `class:air` the class ability's air move — the air move key while airborne
spends its charge, like Ascension's. A secondary fires every rule of its ability plus the ones that name
the input ([rule-format.md](rule-format.md#rules)); an ability takes only the inputs the game has
(`grenade:slide`, `class:hold` and `super:hold` are errors). `finisher` — "Finisher" — finishes one enemy of the pack: it fires the `finisher` rules, then
its hit and kill fire the `damage` and `kill` rules whose source is `any` (a finisher has no damage type
and is no weapon or ability, so `kill: { via: any, targetHas: [jolt] }` fires on a finisher of a jolted
enemy; [rule-format.md](rule-format.md#rules)); it takes no count and is never blocked. `pickup:<id>`
(any kebab-case id) picks that pickup up — one `PickedUp` event — and it is
then no longer on the ground. A weapon action for an empty slot, or a pickup that isn't on the ground
(`No orb-of-power on the ground — nothing happens.`), is a **blocked step**: it changes nothing.

**States you declare** (ADRs D3). What only play decides — that a threshold is reached, that a status
has ended, that the next enemies are a new pack — the player declares as a step of its own:

* `max:<status>` — "Bolt Charge at max (x10)": that buff is at its maximum. The label names the build's cap, a
  fact: the glossary's `maxStacks`, or the cap a `raiseMaxStacks` passive states for its copies (Charged Up: "Armor
  Charge at max (x4)" with one copy — [rule-format.md](rule-format.md#passives)); the player declares it reached, nothing counts toward it.
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
  debuff on the pack has ended and is removed. It raises no event, so no rule reacts to it. Blocked
  unless the status is active: `Amplified isn't active — nothing ends.` ·
  `No buff or debuff 'amplifyed' in the rules — nothing ends.`
* `pack:new` — "New pack": the next enemies are a new pack, with none of the old pack's debuffs; the
  buffs on you and the pickups on the ground stay ([ADRs D7](../ADRs.md)). It raises no event and
  always holds. A loop that meets a new group of enemies each pass starts with it, so the next pass's
  first hit isn't credited with debuffs the new group doesn't have; a loop against one boss leaves it
  out.

**Written form.** Written tokens are lower-case and carry the count only when it is more than one
(`grenade:kill`, `kinetic`, `grenade:kill:3`, `finisher`, `max:bolt-charge`, `end:amplified`, `pack:new`). Every token written
reads back as the same action (`grenade:hit` is written `grenade`). Labels read "Grenade (kill 3)",
"Festival Flight (hit 5)" (a weapon by its build name), "Sliding melee (kill)", "Class ability (in the air)",
"Finisher", "Pick up Orb of Power", "Bolt Charge at max (x10)", "Amplified ends", "New pack".

**What `play` and the web designer offer.** Only steps that can happen: the abilities, the equipped
weapons, the pickups on the ground and the **states you declare** — `pack:new` always, `max:` for
each active buff that stacks and isn't at max yet, `end:` for each active status. A secondary (`melee:slide`, `class:air`)
is offered only when an equipped rule names it (without one it fires exactly what the press fires); `finisher` (with
the abilities) always, as the game always allows one — in a build where no rule reacts to a finisher and none fires on
a kill from any source, it sets nothing off. Every token still reads anywhere
(CLI, loop files, share links).

The web designer's **Next trigger** is laid out like Destiny's HUD: the Super's diamond, then the grenade, melee and
class ability squares with the build's own icons in the subclass's colour, each with the secondary uses the build's
rules name below it — a different trigger from the press, captioned by its input ("slide", "air", "hold") with the
icon of the element that names it (Tempest Strike, Ascension) — and a dashed **Finisher** square (the game has no HUD square for it; a finisher has no element and no damage
type) with the finisher's own icon: after a Bungie login the one the player's character of the build's class has
equipped (read from its Finishers slot; named from Bungie's full manifest when the excerpt doesn't have it), else the
class's own, the one the game plays when none is equipped (Blade Carnival for a Hunter, the glossary's `finishers`) —
then the three weapons with their icon, damage type's icon and colour. Tapping the Super, an ability or a weapon
shows its actions under the HUD — hit or kill, and how many enemies that one action catches — and it stays picked
for the next step; a secondary's own hit and kill show the same way ("Sliding melee · Tempest Strike"); the class
ability, its air move and the finisher act at one tap. Above the HUD, where the game lists your buffs, the states to declare are grouped by
status — on you, on your weapons, on the pack (with **New pack**) — each with its own buttons ("Bolt Charge: at max
(x10) · ends"), and what is on the ground to pick up.

**How the designer shows a step.** The same facts and wording as the CLI's `trace`, one line each: the
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
(×3). An
element's name shows a preview on hover and its details on a tap. Every keyword an event or outcome names — a
status, a debuff by its adjective ("Jolted"), a pickup, a summon — is in its affinity's colour wherever it is in
the line ("+1 **Bolt Charge**", "Grenade kill on **Jolted** target"), and the ones the rules have as an element
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
Power waiting for `pickup:orb-of-power`; an Ionic Trace never waits, it collects itself). A phone stacks the
four. A status that switches other elements' passives on says how many ("Amplified · 1 effect": Spark of
Frequency's extra Bolt Charge); its details list them under **While Amplified** in their **In this build**
card, each with where it reaches. A passive that changes weapon stats (Ionic Overclock: Linear Fusion Rifles, Fusion Rifles and Heat
Weapons) shows in the weapons column next to the build's weapons of those types while it holds; when none of
them is one, the details say it does nothing here and Validation says so too. A status's own effect (Woven
Mail's damage resistance) is in its details, not in the state. Every status opens its details on a tap — one the
rules have no element for (Amplified) shows the glossary's facts and what it switches on — and its facts ("up to
x5 · 8s") show on hover. The CLI's `--state` prints the same four lines, then one `While <status>` line for each
status that switches something on.
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

While the build has no super, grenade, melee or class ability (named or "?") and no weapon, it has nothing to set things
off, so the **Next trigger** panel is greyed out, nothing in it can be tapped, and a line says why ("Give the build a
super, an ability or a weapon first"); this holds for any build in that state. Once the editor gives it an ability, the
panel offers it; an ability slot still empty is a dashed square named for its kind in the HUD too (an ability is always
possible in the game, ADRs D5, so it can still be cast — it sets off what reacts to any cast).

## Starting from a DIM link

The web designer's Home starts a loop from a DIM link ([ADRs D27](../ADRs.md#d27--a-dim-link-starts-a-build-of-what-the-catalog-knows-by-hash-the-rest-is-left-out)):
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
manifest first (the button says so; a few seconds the first time — [hosting.md](hosting.md#bungies-manifest)),
and the same happens when a shared loop names them. The loadout's items are named only by manifest hash, so
the build holds what the catalog recognises by hash, and nothing is guessed:

| From the loadout | Becomes |
|---|---|
| the subclass item | `class` and `subclass` — the glossary's `subclasses` ([rule-format.md](rule-format.md#glossary-rulesglossaryyaml): the nine Light ones so far, whose hashes DIM's source lists); a Stasis, Strand or Prismatic loadout, or one without a subclass, can't start a build yet |
| the subclass's plugs, in socket order | the super, grenade, melee and class ability, aspects and fragments the catalog has a hash for, for the loadout's class (another class's, such as the Hunter's Spark of Shock on a Striker, is left out); an ability it has none for is `"?"` |
| the other equipped items (and the Loadout Optimizer's exotic) | `exoticArmor`; a weapon the manifest excerpt knows ([rule-format.md](rule-format.md#manifest-excerpt-rulesmanifestyaml)) becomes one of `weapons` — its slot, name, damage type, archetype and `hash`, and as its `roll` the perk each trait column rolls with among the item's `socketOverrides` (a loadout saved with its perks; most name the weapon only) |
| the armor pieces, by their manifest set | `armorSetBonuses`: each set perk the pieces grant (2 or 4 of a set) that the catalog has a hash for; the pieces themselves (legendary armor) are left out |
| `parameters.mods` (repeats are stacked copies) | `armorMods` |
| `parameters.artifactUnlocks` | `artifactPerks` |
| anything else — legendary armor, a jump, a weapon the excerpt doesn't know, a hash no element has | `leftOut`, by hash and where the loadout listed it |

The loadout's name becomes the build's, without Destiny's icon glyphs (DIM draws them with its own
font: "Arc  - Skipp grenade" reads "Arc - Skipp grenade"). The build is written as an ordinary build
file (comments say where it came from), so the loop
embeds, exports, shares and replays it like any other ([rule-format.md](rule-format.md#build-file-buildsbuildbuildyaml)).
The designer shows the build the way DIM shows a loadout — the subclass with its super, abilities,
aspects and fragments, the artifact (the one that unlocks the most of the build's artifact perks — none on a
tie — tappable for Bungie's text and which of its perks the build has) and its perks, the weapons (kinetic, energy, heavy) with their element and
one perk per trait column the manifest excerpt gives — the build's pick (`roll`), else the perk it names
in that column, else the one perk a column that doesn't roll has (an exotic's), else "?" — each armor piece with the mods
of its slot (the slot the manifest gives the mod; other mods are general) — with Bungie's icons from the
manifest excerpt; what the rules don't know sits where DIM would show it, its icon as bright as any other's, with a
small grey ⊘ in its lower-right corner ("Not in Loopsmith yet: it sets nothing off in your loop"). It also lists the
left-out items under **Not in Loopsmith yet** (collapsed, with how many of each part: open it to see them), by their
manifest name, each a link to its light.gg page; the build check adds `Unknown (?): …` and `Left out of the build: …` as info.
What the manifest says has no part in a loop is not a gap, and it isn't listed: its tile in the loadout has no ⊘,
and its tooltip says why — a stat or tuning mod (Grenade Mod: +10 Grenade; Balanced Tuning — it changes only stats,
which the build's stats count when they add up from the player's armor, [Stats](#stats)), a legendary armor piece
(its set's bonuses already count from the pieces worn; the piece carries stats and mod slots) or a jump. An element the rules
mark `inert` ([rule-format.md](rule-format.md#elements): Skip Grenade, Feeding Frenzy, the ammo, weapon-handling and
resistance mods) is known: its details say "No rules needed." and why, and the
build check doesn't warn about it. A loadout for another class than its subclass's is an error, and so is text that
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

**An item's details.** Tapping a square opens its details, one card each: Bungie's text (from the manifest
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
build's weapon entry gets `catalyst: true`, the catalyst's element is equipped, and the steps replay. A
catalyst whose effect the rules can't express yet (Thunderlord's Return Stroke refills ammo, which isn't
modelled) is shown, and Validation says it sets nothing off.

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
subclass editor with the build's super, grenade, melee, class ability, jump, two aspects and fragments; tapping a plug's
square opens its details, and **Change** opens, inside that slot (across the editor's width), the list of everything the
subclass can hold there — Bungie's manifest, with icons and names: the ones the rules know are marked, the rest carry ⊘,
a jump none (it has no part in a loop). One list is open at a time; picking a plug puts it in the slot and closes it. An aspect
or a fragment can't be equipped twice; ✕ leaves a fragment's slot empty, while an aspect has no ✕: it is only swapped
for another. The aspects open the fragment slots (`fragmentSlots` in the rules: Tempest Strike
and Flow State 4, Tempest Strike and Ascension 5); the count follows the aspects at once. An aspect whose count the rules
don't give shows "?" slots and limits nothing. An aspect change that closes slots takes out the last fragments and names
them. **Cancel**, ✕ or Escape throws the changes away.

**Save** rewrites only the embedded build file's `super`, `grenade`, `melee`, `classAbility`, `aspects`, `fragments`
and `leftOut` (a plug the rules don't know, and the jump, are `leftOut.subclassPlugs`, shown ⊘ and listed under "Not in
Loopsmith yet" — a jump isn't); the build check runs again and the steps replay with the new build, in place. The Next
trigger HUD, the Validation list and **Analyze** follow it. When the stats were added up from the player's armor
(they equal what that armor gives for the old subclass), they are added up again with the new fragments (Spark of Shock
gone gives back its −10 Grenade); stats the player typed stay as typed. The export and the share link carry the new
subclass. What the subclass can hold comes from the manifest excerpt (`plugs` of the subclass item, written by
`tools/manifest/` for each of the glossary's nine subclasses). A plug is the rules' element only when the rules know it
for the build's class: the rules author Skip Grenade and the Sparks for the Hunter, so on a Striker or a Stormcaller
they are left out (⊘), in the editor and from a DIM link alike.

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
  has them all), and `catalyst: true` when its catalyst socket holds the catalyst. Only those entries of the embedded
  build change, and the steps replay. The Stats section's line says so ("Its weapons are yours too, with their
  perks."), or, for weapons that aren't theirs, to pick their own with ⋯.
* **⋯ next to each weapon slot** opens the **weapon picker** for it, after the login on **Yours**: the player's
  weapons for that slot, the copy the build names first (✓ when it is theirs; for someone
  else's share none is ticked, so their own copy can be used to load its perks), then that weapon's other copies, then
  the rest by name; each with its tier, masterwork, breaker and element as in the loadout. Hovering one (or a
  first tap) shows its type, tier and its perks column by column (a column that rolled two or more shows them framed
  together, the one it holds on DIM's blue), and where it is; a click (a second tap, or "Use this
  weapon") puts it in the build: its entry is written anew (`{slot, name, type, archetype, perks: [], hash, roll,
  catalyst, instance}`, from the manifest and its sockets), or added for an empty slot, and the steps replay. A build
  wears one exotic weapon (by its manifest item, or an exotic weapon element among a hand-written weapon's perks):
  another exotic isn't listed while it does (the picker says which to swap); and one weapon a slot. Weapons the
  excerpt doesn't know are named by Bungie's full manifest, read once after the login with the armor (the Bungie
  login section shows its progress bar; the page keeps working meanwhile — [hosting.md](hosting.md#bungies-manifest));
  one it can't place is counted, not listed.
* **Perks your copy doesn't have** — the perk picker still offers every perk a column rolls with. For a weapon whose
  copy (its `instance`: picked from Yours, or the player's own dim.gg share's) is theirs, each perk the copy doesn't
  have in that column carries a small amber **!** in its square's upper-right corner (clear of the ⊘ in the lower
  right and of an enhanced trait's arrow on the left) and "Not on your copy" under its name. Picking one puts the same
  **!** on the weapon's perk square and adds a warning under Validation: "Festival Flight: your copy doesn't have Hatchling in column 2 (it has Elemental Honing
  (enhanced) or Attrition Orbs)." Each perk of a column that rolled two or more is the copy's; so is a base perk whose
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
  cannon", "arc"; for armor its name or its set's). Each weapon's footer shows its damage type and its breaker (its
  own, else its frame's: Adaptive Frame pierces barriers), and the item shown is framed with its footer. The list comes
  from Bungie's item table, read once per tab when a picker first opens (its progress bar shows;
  [hosting.md](hosting.md#bungies-manifest)). The one-exotic rule holds as
  in the player's pickers: no exotic is listed while another slot holds one (the picker says which to swap).
* **A pick** — hovering an item (or a first tap) shows its name, type and damage type (for armor, its set); a click (a
  second tap, or "Use this weapon" / "Wear this piece") puts it in. Bungie's full manifest is read for it first when
  the excerpt lacks it (its trait columns, catalyst or set): the picker shows the bar and stays open, and when it can't
  be read it says so and the build doesn't change. A **weapon**'s entry is then written anew, or added for an empty
  slot, as `{slot, name, type, archetype, perks: [], hash}`: no roll (each rolling column shows "?", picked with the
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
