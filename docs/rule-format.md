# Rule & build file format (v1)

Rules author cause and effect: the triggers a build reacts to and the outcomes they set off. A number
in a rule is a fact shown with its outcome, never added up or used to decide what happens — Loopsmith
doesn't simulate the game ([ADRs D1](ADRs.md)).

Causality is authored, not scraped: every build element is a YAML entry with **rules**
(`on` trigger → `then` outcomes) and **passives** (always-on modifiers). The YAML is a
boundary format — `RuleParsing` / `BuildParsing` turn it into the Domain types in
`src/Loopsmith.Core/Domain/` and nothing else ever sees the DTOs.

All ids are kebab-case slugs (lower-case letters, digits, inner hyphens: `shinobus-vow`, `bolt-charge`).
Keys are camelCase, and so are the closed vocabulary words (`classAbility`, `exoticArmor`).
Unknown keys, unknown ids and unknown statuses are **parse errors** — all errors of a file
set are reported together, each with `file:line`
(`hunter/arc.yaml:8: unknown key 'stack' in applyBuff (allowed: status, stacks, duration)`).
A key whose value is empty, `~` or `null` counts as omitted.

## Files

```
rules/
  glossary.yaml          statuses (buffs/debuffs), pickups, summons — the keyword vocabulary
  keywords/*.yaml        kind: keyword elements — rules active in every build (Bolt Charge, Ionic Trace…)
  hunter/arc.yaml        class + subclass elements (abilities, aspects, fragments)
  exotics/armor.yaml
  armor-sets/*.yaml
  mods/armor.yaml
  mods/weapons.yaml      weapon mods (Backup Mag, Synergy…): what a weapon's mod socket holds
  artifact/current.yaml
  weapons/perks.yaml
builds/<build>/build.yaml
```

The folders are a convention: every `*.yaml` under the rules root is read (the web app embeds only
`*.yaml`, so use that extension), and element ids
are unique across all of them (`duplicate element id '<id>' (first defined at <file>:<line>)`). The root
holds exactly one `glossary.yaml`; every other file has the shape `elements: [ … ]`. When the glossary
is missing or broken, the other files are still checked for their own errors, but their references
aren't. The CLI uses the nearest `rules/` folder with a `glossary.yaml` above the build (or loop) file,
then above the current directory; `--rules <dir>` overrides it.
The catalog version is `authored-<first 12 hex chars of SHA-256 over the (path, text) pairs sorted by path>`
(paths relative to the rules root), so any edit to any rule file changes it.

## Glossary (`rules/glossary.yaml`)

```yaml
statuses:
  - { id: bolt-charge, name: Bolt Charge, kind: buff,   affinity: arc, maxStacks: 10, trait: 2935077680 }
  - { id: jolt,        name: Jolt,        kind: debuff, affinity: arc, duration: 10s, trait: 3221118171 }
pickups:
  - { id: ionic-trace,  name: Ionic Trace,  affinity: arc,     collectsAutomatically: true, trait: 3824458961 }
  - { id: orb-of-power, name: Orb of Power, affinity: neutral, collectsAutomatically: false }
summons:
  - { id: threadling, name: Threadling, damageType: strand, trait: 2724747993 }
subclasses:
  - { class: hunter, subclass: arc, name: Arcstrider, hash: 2328211300 }
finishers:
  - { class: hunter, name: Blade Carnival, hash: 4012203003 }
```

Each section is optional; ids are unique within a section. All keys shown are required except a
status's `maxStacks` (a whole number ≥ 2), `duration` and `drawnIcon`, `counted` and `decay`, and `trait`.

* `counted: true` (a buff with `maxStacks` only; `the status 'x' is counted, so it must be a buff with maxStacks`
  otherwise): the engine counts its stacks, sure and chance ones, and they decide what fires (ADRs D33). Armor Charge is
  the one counted status; `holds`, `spendStacks` and a table by stacks (below) need it. `decay` is how it loses stacks
  over time, as text from the source ("1 stack every 10 s while a passive Armor Charge mod is equipped …"): shown with
  the count, never applied.

* `trait`: Bungie's trait for the keyword (the manifest's `DestinyTraitDefinition`: the game's own keyword, as
  its tooltips name it — Jolted for Jolt), by hash. The manifest excerpt's `keywordTraits` give its name and icon
  ([Manifest excerpt](#manifest-excerpt-rulesmanifestyaml)); the designer shows the icon before the keyword's
  name in a step's text, its title, the state at its end and the keyword's details
  ([loop-format.md](loop-format.md#action-tokens)). A keyword without one (Armor Charge, Orb of Power: Bungie has no trait for them) shows its
  element's manifest icon when it has one (Slice: its weapon perk), else its `drawnIcon`, else none.

* `drawnIcon` (a status): the shape the game draws for a keyword Bungie's data has no icon file for, so the
  designer draws it, in the status's affinity — Loopsmith's drawing of the game's shape, not Bungie's file.
  `rounds`: a triangle around a round, as the game shows Unraveling Rounds in your buffs (Bungie's manifest has no
  trait, perk or item by that name). A status with a `trait` has the game's own icon, so it takes no `drawnIcon`
  (`the status 'jolt' has a trait, so the game's own icon`).

* `kind`: `buff` (on the player) · `weaponBuff` (on the player's weapons: Slice, Unraveling Rounds — a
  `buff` to the engine, wherever a rule names a buff; the designer's state shows it with the weapons) ·
  `debuff` (on the pack).
  `affinity`: `neutral|kinetic|arc|solar|void|stasis|strand|prismatic`. A status's affinity is also the
  damage type of its `strikeTarget` strikes (neutral and prismatic strike as kinetic).
* `maxStacks` says the status **stacks**, and its cap: a fact shown with the buff ("up to x10"), which a
  build's `raiseMaxStacks` passives raise ([Passives](#passives): Charged Up). It
  makes the buff declarable at its maximum — "Bolt Charge at max (x10)", `max:bolt-charge`
  ([loop-format.md](loop-format.md#action-tokens), ADRs D3) — so `stacksMaxed` triggers and `atMax`
  conditions need it. A status without `maxStacks` doesn't stack: applying it while it is active
  changes nothing.
* `duration` is a fact shown on an `applyBuff` / `debuffTarget` that gives none. A buff ends when a
  rule removes or consumes it, or when the player declares it ended (`end:amplified`); a debuff stays
  on the pack until the player declares it ended (`end:jolt`) or declares a new pack (`pack:new`).
* `collectsAutomatically: true` — collected the moment it spawns (Ionic Traces track to you);
  `false` — it lands on the ground until a `pickup:<id>` step ([loop-format.md](loop-format.md)).
* `damageType` (summons): `kinetic|arc|solar|void|stasis|strand`.
* `subclasses`: the manifest hash of each subclass item a source gives (one number or a list of
  copies), so a DIM loadout's subclass is recognised
  ([loop-format.md](loop-format.md#starting-from-a-dim-link)); a hash names one subclass (`hash 1002 of the
  subclass 'Stormcaller' is already on 'Striker'`). A manifest hash is never 0, here or on an element.
* `finishers`: each class's own finisher, by its manifest hash — the one the game plays when the player has none
  equipped (Bungie's data: the one Common, class-only item of the Finishers bucket for that class). The designer's
  HUD shows its icon on the finisher square, and a finisher step's title, until a Bungie login names the finisher the
  player's character has equipped ([loop-format.md](loop-format.md#action-tokens)). One per class (`the hunter
  already has its finisher, 'Blade Carnival'`).

## Manifest excerpt (`rules/manifest.yaml`)

Generated by [`tools/manifest/extract_manifest.py`](../tools/manifest/README.md) — don't edit it by hand. It
holds what the Bungie manifest says about every hash Loopsmith names (each `hash:` in `rules/`, every hash a
saved DIM share wears and every hash a weapon of the repo's builds and loops gives), so the web app shows a build the way DIM shows a loadout, an item's details carry
Bungie's text, and a DIM link's weapons and armor sets become the build's:

```yaml
version: "244213.26.06.29.2000-1-bnet.65864"
damageTypes:
  - { type: strand, icon: /common/destiny2_content/icons/DestinyDamageTypeDefinition_….png }
breakerTypes:
  - { hash: 2611060930, name: "Disruption", icon: /common/destiny2_content/icons/DestinyBreakerTypeDefinition_….png }
armorStats:
  - { stat: grenade, name: "Grenade", icon: /common/destiny2_content/icons/065c….png }
itemConstants:
  tierOverlays: [/img/destiny_content/items/inventory-item-tier1.png, …, /img/destiny_content/items/inventory-item-tier5.png]
  masterworkOverlay: /img/destiny_content/items/masterwork-overlay.png
  masterworkExoticOverlay: /img/destiny_content/items/masterwork-overlay-exotic.png
armorSets:
  - { hash: 499993704, name: "Luminopotent", perks: [{ pieces: 2, perk: 2769443639 }, { pieces: 4, perk: 2769443638 }] }
items:
  - { hash: 4019651319, name: "Festival Flight", kind: weapon, type: "Grenade Launcher", tier: legendary, slot: kinetic, damageType: strand, breaker: 2611060930, traits: [[1017229899, …, 923806249, …], [1546142478, …, 243981275, …]], intrinsic: 385138649, watermark: /common/…/50c3….png, icon: /common/…/e3d2….jpg }
  - { hash: 3325463374, name: "Thunderlord", kind: weapon, type: "Machine Gun", tier: exotic, slot: power, damageType: arc, traits: [[1419069769], [2779035018]], intrinsic: 4148158229, catalyst: 1550063955, breaker: 2611060930, icon: … }
  - { hash: 1550063955, name: "Thunderlord Catalyst", kind: catalyst, tier: exotic, perks: [818260022], description: "Upgrades this weapon to a Masterwork. …", icon: … }
  - { hash: 818260022, name: "Return Stroke", kind: perk, description: "Causing a lightning strike with this weapon partially reloads the magazine from reserves.", icon: … }
  - { hash: 593554567, name: "Luminopotent Mask", kind: armor, type: "Helmet", tier: legendary, slot: helmet, set: 499993704, class: hunter, icon: … }
  - { hash: 3832366019, name: "Harmonic Siphon", kind: armorMod, type: "Helmet Armor Mod", tier: basic, slot: helmet, description: "…", icon: /common/…/c917….png }
```

* `kind`: `subclass|super|grenade|melee|classAbility|movement|aspect|fragment|weapon|weaponPerk|weaponMod|intrinsic|catalyst|armor|armorMod|activityMod|artifactPerk|artifact|perk|other`.
  An `activityMod` is a mod of a raid's, an activity's or a retired season's socket (its plug category is
  `enhancements.raid_*`, `enhancements.season_*` or `enhancements.rivens_curse`, whatever its type says): Loopsmith
  doesn't support them ([loop-format.md](loop-format.md#raid-activity-and-seasonal-mods), ADRs D35).
  A `weaponMod` is a weapon mod (Bungie types it "Weapon Mod", or "Enhanced Weapon Mod" for an enhanced one, which only
  a Tier 4 or 5 weapon takes).
  A `perk` is one of the manifest's sandbox perks — what a catalyst or an armor set grants — and has no `type`.
  An `artifact` is one of the seven artifacts of Artifacts 2.0 (every artifact item whose sockets take artifact
  perks; each is written, with every one of its perks as an item): `tiers` are its perks by tier, Tier 1 first
  (`tiers: [[…7], […7], […7]]`) — its perk sockets' plug sets, each once in socket order, reach one tier further each,
  so a tier is the perks of its plug set that an earlier one doesn't have, without a socket's empty plug — and
  `slots` the highest tier each of its perk sockets takes (`slots: [1, 1, 2, 2, 2, 3, 3]`: two take Tier 1, three
  Tiers 1–2, two Tiers 1–3; not the reset socket), so a slot holds a perk of its tier or lower; and `flavor` its
  flavour text. A build's artifact is the one it names (`artifact:`), else the one that unlocks the most of its perks
  (each counted once; two that unlock as many: none, not a guess).
  A `subclass` (each of the glossary's) has `plugs`: every plug its sockets take — super, abilities, jump,
  aspects, fragments, without the empty sockets' — each written as an item too; the subclass editor lists them.
* `description`: Bungie's text (for an aspect or a fragment, whose own is empty, its perks': its effect, then a
  stat change such as `-10 Grenade ▼`), without the private-use glyphs of Destiny's icon font. It is shown
  in an item's details as Bungie's; it is never a rule.
* `slot`: a weapon's `kinetic|energy|power` and armor's `helmet|arms|chest|legs|classItem` (both required);
  an armor mod's armor slot (none: a general mod). `damageType` belongs to weapons, and so do `traits`: its
  trait columns (the "frames" sockets of its perks), each the list of perk hashes it can roll with, enhanced
  versions included (the manifest's plug set). A column that doesn't roll (an exotic's) has its one perk.
  Every perk is written as an item too (`kind: weaponPerk`, `type: "Trait"` or `"Enhanced Trait"`); a column
  needs at least one hash. A weapon's `intrinsic` is its frame (an exotic's own perk), its `catalyst` an
  exotic's catalyst item (whose `perks` are what it grants), its `mods` the weapon mods its mod socket takes (the plugs
  of its mod socket's plug set, without the empty ones; each written as a `weaponMod` item; none: it has no mod socket,
  as most exotics), its `breaker` the champion it breaks, a breaker type's
  hash: its own (an exotic's), else its frame's — a frame's perk names the breaker in brackets ("[Shield-Piercing]
  Barrier" on Adaptive Frame, "[Disruption] Overload" on Rapid-Fire Frame), as DIM reads it; armor's `intrinsic` is an exotic's perk, its
  `set` the armor set it belongs to and its `class` the class that wears it (`titan|hunter|warlock`; none: any
  class — the armor picker lists a build's class's pieces). These keys on anything else are an error (`slot is
  only allowed on a weapon, armor or an armor mod, not on a grenade`; `set is only allowed on armor, not on a
  weapon`; `perks` belongs to a catalyst; `tiers`, `slots` and `flavor` to an artifact; `plugs` to a subclass). A weapon's or armor's `watermark` is its season watermark:
  the overlay with its season's or expansion's icon in the top-left corner, drawn over its square as DIM draws it.
* `breakerTypes`: the three champion breakers (Shield Piercing, Disruption, Stagger), each with its name and icon: a
  weapon's breaker shows under its square, as DIM shows it. `armorStats`: the six armor stats (`stat`: weapons, health,
  class, grenade, super, melee), each with its name and icon: an Armor 3.0 piece's highest rolled stat
  shows in its square's top-right corner, as DIM shows it. `itemConstants`: the overlays Bungie draws on an item's
  icon (the manifest's inventory item constants): `tierOverlays` the diamonds of gear tiers 1 to 5 in order (tier
  1's draws none), `masterworkOverlay` the masterwork glow (`masterworkExoticOverlay` an exotic's). A player's copy
  shows them after their Bungie login ([loop-format.md](loop-format.md#stats)). Both are optional.
* `keywordTraits`: the traits the glossary's keywords name (`trait:`), each `{ hash, name, icon }` — the
  game's keyword icons (Jolted, Bolt Charge, Woven Mail).
* `armorSets`: each set's name and the perk each piece count grants. A build's set bonus element gives the
  set perk's hash (`luminopotent-2pc` → `hash: 2769443639`), so a DIM loadout's pieces equip it.
* `tier`: `basic|common|rare|legendary|exotic`. `icon` is a bungie.net path (the app prefixes
  `https://www.bungie.net`). A hash appears once. The file is optional: without it nothing has an icon.
* `statPlugs`: every plug of the manifest that changes an armor stat, for adding up a build's stats from a
  player's own armor the way DIM does ([loop-format.md](loop-format.md#stats)):

  ```yaml
  statPlugs:
    - { hash: 311219525, kind: base, stats: [{ stat: weapons, value: 1 }] }
    - { hash: 788990510, kind: masterwork, stats: [{ stat: weapons, value: 5 }, …, { stat: melee, value: 5 }] }
    - { hash: 2121121504, kind: tuningSocket, stats: [] }
    - { hash: 309000506, kind: tuning, stats: [{ stat: grenade, value: 5 }, { stat: melee, value: -5 }] }
    - { hash: 3122197216, kind: balancedTuning, stats: [{ stat: weapons, value: 1 }, …] }
    - { hash: 1435557120, kind: statMod, stats: [{ stat: grenade, value: 10 }] }
    - { hash: 1727069360, kind: fragment, stats: [{ stat: class, value: -10, class: warlock }, { stat: weapons, value: -10, class: hunter }, …] }
  ```

  `kind`: `base` (a piece's rolled stats: its armor-stat and intrinsic plugs), `masterwork` (Armor 3.0's: it adds
  only to a piece's stats at 0, the three its roll leaves empty), `tuningSocket` (the empty tuning socket's plug, with
  no stats: it marks the socket), `tuning` (a tuning mod: points from one stat to another), `balancedTuning` (like a
  masterwork, only to a piece's stats at 0; one piece per tuning socket), `statMod` or `fragment` (a fragment with a
  stat), as DIM reads them. `stat` is one of the six (`health|melee|grenade|super|class|weapons`), `value` a whole
  number (negative for a fragment's −10). A stat with a `class` counts for that class only (Spark of Focus and Echo of
  Persistence lower the stat of the class's own ability regeneration).

## Elements

```yaml
elements:
  - id: spark-of-shock
    name: Spark of Shock
    kind: fragment            # keyword|super|grenade|melee|classAbility|jump|aspect|fragment|exoticArmor|
                              # exoticWeapon|catalyst|armorSetBonus|armorMod|artifactPerk|weaponPerk|weaponMod
    affinity: arc
    class: hunter             # optional: hunter|titan|warlock
    hash: 1727069364          # optional manifest hash, an unsigned 32-bit number (from Clarity until the manifest join exists);
                              # a list when copies of the item share its text: hash: [3712696020, 2996369932]
    description: "Arc grenades apply Jolt on hit."   # optional
    source: compendium/2026-10-09/Arc#24             # optional, see Provenance; omitted → authored at file:line
    rules:                    # optional (default none)
      - on: { damage: { via: grenade } }
        then:
          - { debuffTarget: jolt }
        reason: "Your Arc grenades jolt everything they hit."
    passives: []              # optional (default none)
    # inert: "Ammo: Loopsmith doesn't model ammo, so no loop changes."
                              # optional: why an element has no rules and no passives on purpose
```

`id`, `name`, `kind` and `affinity` are required. A hash names one element: the same hash on two
elements is an error (`hash 1001 of 'reaper' is already on 'bomber' (mods.yaml:2)`), so a DIM loadout's
item is recognised as one element or none ([loop-format.md](loop-format.md#starting-from-a-dim-link)).
Two keys belong to some kinds only (anywhere else they are an error):

* `fragmentSlots: 2` — aspects only (`fragmentSlots is only allowed on aspects, not on a fragment`), a
  whole number ≥ 0; the build check counts fragments against it. It is the aspect's energy capacity in
  Bungie's manifest (Flow State and Tempest Strike 2, Ascension 3).
* `ability: { kind: grenade, charges: 1, chunkScalar: 0.75, baseCooldown: 151.5 }` — super, grenade, melee
  and classAbility elements only, and `kind` must be the element's kind. `charges` defaults to 1,
  `chunkScalar` and `baseCooldown` (game values) to `"?"`. The ability's profile, recorded from the Compendium:
  `chunkScalar` and `baseCooldown` are facts changing nothing (ADRs D5); `charges`, with every equipped
  `extraCharges`, says whether a use always spends the ability's last charge or the player marks the one that does
  (ADRs D36).

`kind: keyword` elements are active in every build, once; every other kind is equipped through its
build slot ([Build check](#build-check)). A `kind: jump` element is a subclass's jump (High Jump, Blink): it is
`inert` ("A jump: movement has no part in a loop."), so the build knows it and its subclass editor marks it, but it
sets nothing off and the engine never plays it. The rules have the Arcstrider's four, by that subclass's hashes; a
jump of another subclass (each has its own copies) stays a left-out plug. Besides the keywords, `keywords/guardian.yaml`
holds the Guardian's own damage resistance (34.5% against combatants, compendium Game Mechanics#106), so every build's
state counts it.

`statChecks` say something about an element at some values of a build's stat — a fact of the game the build
check states, never counted (the stat isn't simulated):

```yaml
statChecks:
  - { stat: melee, below: 50, severity: warning, says: "With {value} Melee, a Gambler's Dodge gives back less than a full melee charge, even near an enemy: …" }
  - { stat: melee, from: 50, below: 100, severity: info, says: "With {value} Melee, a Gambler's Dodge gives back a full melee charge only within 15 m of an enemy …" }
```

A check speaks when the build's `stat` is known and at least `from` (when given) and below `below`; `{value}`
is the build's value. `from` and `below` are stat values (0..200) and `from` < `below`
(`a stat check's range starts below its end (from < below)`); `severity` is `warning` or `info` (a stat never
blocks a build). An unknown stat ("?") says nothing.

`inert: "<why>"` marks an element that is known and has no part in a loop on purpose — Skip Grenade
(its hits are the grenade step itself), Feeding Frenzy (reload speed changes no step: no ammo is counted), Fastball
(how far and fast a grenade flies changes no step). It takes no rules and no passives
(`inert is only for an element with no rules and no passives`), the build check says nothing of it, and its details
say "No rules needed." with the why.

**An element is in one of three states**, and only the last is a gap:

| State | In the rule files | What the player sees |
|---|---|---|
| In the loop | `rules` or `passives` | what it sets off, in the trace and the graph |
| Outside the loop on purpose | `inert: "<why>"` | nothing under Validation; its details say "No rules needed." and why |
| Not authored yet | no element (the manifest knows it, the rules don't) | its square carries a small grey ⊘; nothing under Validation |

An element with no rules, no passives and no `inert` is a gap in the rule files, not something to show the player:
a test over `rules/` fails and names it (`ElementStatesCatalogTests`). Write its rules, or say why it needs none.

### Enhanced traits (`enhanced`)

A weapon trait's **enhanced trait** — the one a weapon's enhanced or crafted copy rolls — is its own plug with its
own hash, and the Compendium gives its figures after a ↑ in the perk's row ("8 ↑9 seconds"). A `weaponPerk` writes
it as a block, not as a second element:

```yaml
  - id: slice
    kind: weaponPerk
    hash: 923806249
    rules: [ … { applyBuff: { status: slice, duration: 8s } } … ]
    enhanced:
      hash: 3422796781          # its plug (a list when there are more)
      values: { 8s: 9s }        # the ↑ figures: a value the rules use → the enhanced one (durations, game values)
      description: "The enhanced Slice: as Slice, with a 9 s window between hits instead of 8 s."   # optional
```

The catalog gets a second element, `<id>-enhanced` ("Slice (enhanced)"): the perk — its kind, affinity, source, `inert`
and stat checks, its rules and passives — with the block's hashes and description, each value of `values` replaced
(`slice-enhanced` arms Slice for 9 s); the perk keeps its own. A reason is copied as it is, so it doesn't write a figure
the block replaces (`Dodging arms Slice, even while the weapon is stowed.`: the outcome shows the 8 s or 9 s).
Without `values` its rules are the perk's (Attrition Orbs: its ↑ figure, the orb's Super energy, is in no rule; Feeding
Frenzy: inert, its ↑ figures in its description). `rules` (optional) are what only the enhanced trait does, after the perk's
(Hatchling's second Threadling, which the enhanced trait spawns at a chance). A value the rules don't use is an error
(`enhanced.values: 7s isn't a value of slice's rules`), and so are a value given twice (`8s` and `8.0s`), a reason
that writes a replaced figure (`a reason of slice writes 8s, which its enhanced trait replaces`), `"?"` (an unknown figure is left out, not written),
`enhanced` on another kind (`only a weaponPerk has an enhanced trait, not a armorMod`) or a block without `hash`.
Every other plug of the manifest excerpt that Bungie types "Enhanced Trait" or "Enhanced Origin Trait" with the perk's
name is the enhanced trait's too — the bundled excerpt's, and what Bungie's full manifest adds after a login (an
account's enhanced copy of a weapon) — so the designer equips it from a DIM link or a picked weapon. The excerpt
generator adds every enhanced plug of a trait the rules author, and a test fails when an authored perk has one but no
`enhanced` block. A plug two enhanced traits would take (their perks share a name) is neither's. Armor has no enhanced perks: set bonuses have none in the game (only legacy raid mods do).

### Provenance (`source`)

| Form | Domain |
|---|---|
| `compendium/2026-10-09/Arc#24` | `Provenance.Compendium(snapshot, tab, row)` |
| `clarity/1727069364@2.0625` | `Provenance.Clarity(hash, version)` |
| `{ creator: <url>, quote: "<what they said>" }` | `Provenance.CreatorClaim(url, quote)` — an absolute http(s) URL and a non-empty quote |
| omitted | `Provenance.Authored(file, line)` |

Compendium rows: `<date>` is the snapshot folder (`snapshots/compendium/<date>/`), `<tab>` the tab name
as `INDEX.md` lists it (`Arc`, `Class Abilities`, `Artifact Perks` — quote the YAML value when it has a
space: `source: "compendium/2026-10-09/Class Abilities#5"`), and `<row>` the 1-based record number in that
tab's `NN_<Tab>.csv`, which is the spreadsheet's row. Archived `OLD …` tabs are never a source.

### Numbers (`GameValue`)

| Text | Domain | Note |
|---|---|---|
| `"15%"` | `Known(0.15)` | percent → fraction |
| `"+50%"` | `Known(0.5)` | sign optional |
| `"12% \| 17% \| 20%"` | `PerModCount([0.12, 0.17, 0.20])` | value by number of copies equipped (the last one for more copies) |
| `"+20 \| +40 \| +50"` | `PerModCount([20, 40, 50])` | plain numbers by copies (stat points: `modifyStat`) |
| `"~25%"`, `"25%?"` | `Approximate(0.25)` | shown `~25%` |
| `"?"`, `"?%"` | `Unknown()` | **never 0** |
| `"300% [20%]"` | PvE part `Known(3.0)` | `[…]` is the PvP value: checked, then dropped (kept by `resistDamage`; refused by `resistCombatantDamage`) |
| `2`, `"2"` | `Known(2)` | plain number |

Game values appear in `grantEnergy.amount` (or `full`), `convertStacksToEnergy.perStack`, `modifyDamage.change`,
`restoreHealth.amount`, `resistDamage`, `resistCombatantDamage.amount`, a weapon-stat `change`, `modifyStat.change` (stat points: a percent there is an
error), and `ability.chunkScalar` / `baseCooldown`.
Decimals use `.`; quote the value as the examples do (a bare `?` or `~` means something else to YAML).
Every one is shown with its outcome, with its certainty (ADRs D13); an `Unknown` reads `?`.

Durations: `5s`, `0.5s`; `"?s"`, `"?"` or omitted → no duration (`Optional.None`): an outcome then
shows the glossary's.

### Rules

```yaml
rules:                                  # Reaper (rules/mods/armor.yaml)
  - on: { kill: { via: weapon } }
    when: [ { has: reaper } ]           # optional guards
    then:
      - { removeBuff: reaper }
      - { spawn: orb-of-power }
    reason: "Your next weapon kill after a dodge drops an Orb of Power."
```

`on` and `then` (at least one outcome) are required. `reason` is optional plain English, shown in
traces with `--why`; `doesNotStackWith` is [below](#rules-that-dont-stack). `chance: true` (default
false) marks a rule that may not happen in game: "chance to / occasionally", or progress the game
doesn't show as a stacking status (Elemental Charge's progress per Ionic Trace; ADRs D8).

What the format can't count is written in one of three ways, and needs no new format (ADRs D3, D8):
* **a declaration** — a threshold the player says is reached: Slice's 5 hits end with `max:slice`;
* **a split count or *(chance)*** — progress the game doesn't show as a stacking status: a counter that kills or hits
  fill fires for sure on the count that fills it and as a chance on fewer (Spark of Discharge's 3 Arc weapon kills,
  below), and hits or damage kept up on one enemy are `sustained` (To Shreds' 6 weapon hits, Horde Shuttle's 10%,
  Attrition Orbs, Lightning Rounds' 10th hit; ADRs D34);
* **a fact** — energy per stack, refunds, chunk scalars and rule cooldowns (Impact Induction, Photonic Flare) are
  in the description or shown with the outcome; a condition the pack always meets is left out of `when` (Spark of
  Resistance's "3+ enemies within 15 m", ADRs D7).

**Triggers (`on`)** — exactly one key:

| YAML | Domain `Trigger` |
|---|---|
| `{ abilityCast: grenade }` (`grenade\|melee\|classAbility\|super`) | `AbilityCast(kind)` |
| `{ abilityCast: { ability: classAbility, input: air } }` — only that use of the ability (a secondary; `input` is `press\|slide\|air\|hold`) | `AbilityCastAs(classAbility, air)` |
| `{ kill: { via: <source> } }` | `KillAny(via)` |
| `{ kill: { via: <source>, tier: champion } }` (`minor\|major\|boss\|champion`) | `KillOfTier(via, tier)` |
| `{ kill: { via: <source>, targetHas: [jolt, sever] } }` | `KillDebuffed(via, statuses)` |
| `{ damage: { via: <source> } }` | `Damage(via)` |
| `{ damage: { via: <source>, targetHas: [sever] } }` | `DamageDebuffed(via, statuses)` |
| `{ damage: { via: weapon, targetHas: [sever], sustained: true } }` — damage kept up on one enemy: a source that needs several hits or a share of its health (To Shreds' 6 instances, Attrition Orbs' hits, Lightning Rounds' 10th hit, Horde Shuttle's 10%); fires for sure on a hit step's hits, never on a kill step's killing blow (ADRs D34) | `Damage(via, Sustained)` / `DamageDebuffed(via, statuses, Sustained)` |
| `{ damage: { via: weapon, atLeast: 3 } }` — hit at least N enemies with `via` in one action; `within: 3s`: the window the source gives them (One for All's "3 separate enemies within 3 s"), which one action meets | `DamageMultiple(via, atLeast, within)` |
| `{ damage: { via: "weapon:this", fewerThan: 3 } }` — hit fewer than N enemies (at least one) with `via` in one action | `DamageFewer(via, fewerThan)` |
| `{ kill: { via: grenade, atLeast: 2 } }` — kill at least N enemies with `via` in one action; `within: 3s`: the window the source gives them (a Siphon's "2 kills within 3 s"), which one action meets | `KillMultiple(via, atLeast, within)` |
| `{ kill: { via: "weapon:this", atLeast: 2, fewerThan: 3 } }` — kill at least N and fewer than M enemies in one action (Reaper's Tithe's x2 on exactly 2); no `within` | `KillBetween(via, atLeast, fewerThan)` |
| `{ kill: { via: "weapon:this", fewerThan: 12 } }` — kill fewer than N enemies (at least one) with `via` in one action | `KillFewer(via, fewerThan)` |
| `{ pickUp: ionic-trace }` | `PickUp(pickup)` |
| `{ buffGained: bolt-charge }` | `BuffGained(status)` |
| `{ stacksMaxed: bolt-charge }` — the player declares the buff at max | `StacksMaxed(status)` |
| `{ finisher: {} }` — the player does a finisher on an enemy of the pack (it kills it) | `Finisher()` |
| `{ criticalHealth: {} }` — the player declares they reached critical health (`health:critical`) | `CriticalHealth()` |
| `{ shieldBroken: {} }` / `{ shieldBroken: { via: weapon } }` — a combatant's shield broken by that source (`via` defaults to `any`); the player says which weapon step breaks one (`kinetic:kill:shield`) | `ShieldBroken(via)` |
| `{ energyExpended: grenade }` (`grenade\|melee\|classAbility`) — the ability's energy fully expended: its last charge used (`grenade:last`) | `EnergyExpended(kind)` |
| `{ reload: {} }` / `{ reload: { via: weapon:this } }` — a weapon reloaded: the player's Reload step (`energy:reload`) or a `reloadWeapon` outcome; `via` names a weapon (defaults to `any`) | `Reload(via)` |

A **secondary ability use** is the ability used another way, which an aspect makes do something else
(ADRs D9): the melee while sliding (`slide`: Tempest Strike's uppercut, played as `melee:slide`) or in the
air (`air`: Ballistic Slam), the class ability's air move (`air`: Ascension — the air move key while
airborne spends the class ability charge, played as `class:air`), the grenade held (`hold`: Chaos
Accelerant, Bleak Watcher, Mindspun Invocation, Unbreakable). A Super has none. It is still that
ability, so every plain rule on the ability fires on it too (the Compendium, Arc#48: the air move sets
off the equipped class ability's effects; Arc#54: Tempest Strike's uppercut triggers Combination Blow);
a trigger with an `input`, or a damage source `<ability>:<input>` (`via: melee:slide`), fires on that use
only. `input: press` (or `via: melee:press`) is the plain press only. The map form without `input` is
the plain trigger. An input the ability doesn't take is an error
(`abilityCast: a grenade isn't used as 'slide' (it takes press, hold)`); the old `airborne` key is an
unknown key (write `input: air`). [loop-format.md](loop-format.md#action-tokens) has the tokens.

`via` defaults to `any`. `targetHas` lists one or more debuffs, and the target must have all of them.
`tier` and `targetHas` together is an error (`kill cannot combine 'tier' and 'targetHas'`), and so is
`atLeast` with either of them, or with `sustained` (`damage cannot combine 'atLeast' and 'sustained'`); `damage` has no
`tier`. `sustained` (default `false`) is for a count the game doesn't show of hits or damage on one enemy: write it
instead of `chance: true` (ADRs D34). The pack in front of you is `minor` (ADRs D7),
so a `major|boss|champion` kill shows in `explain` and the graph but doesn't fire in a loop.
`atLeast` is a whole number 1..20 (`TargetCount`, ADRs D4): the player says how many enemies an
action hits ("One For All: hitting three separate targets…" → `{ damage: { via: weapon, atLeast: 3 } }`).
`fewerThan` is 2..20 and stands alone like `atLeast` (`damage cannot combine 'atLeast' and 'fewerThan'`), but on a kill
it can bound `atLeast` from above: `{ atLeast: 2, fewerThan: 3 }` is exactly 2 (`fewerThan` must be above `atLeast`: `fewer
than 2 and at least 2 never happens`), so Reaper's Tithe gives x2 on 2 kills at once, x3 on 3 and x4 on 4 or more, one rule
each. Alone, the two split one count (ADRs D31), so a counter the game doesn't show fires for sure on the kills that fill it and as a chance
on fewer — Synergy (`rules/mods/weapons.yaml`): `atLeast: 12` spawns its pickup, `fewerThan: 12` spawns it with
`chance: true`, since the kills of earlier steps may have filled it; Spark of Discharge's counter (34% per minor kill)
the same, at 3. The Siphons split "2 kills within 3 s" the same way, and Spark of Amplitude "2 kills within 4 s":
`{ kill: { via: weapon:arc, atLeast: 2, within: 3s } }` spawns the orb for sure, `fewerThan: 2` as a chance (`within` needs
`atLeast`: `'within' is the window of a kill count, so it needs 'atLeast'`). Hits split the same way: One for All
(`rules/weapons/perks.yaml`) gives its buff on `{ damage: { via: "weapon:this", atLeast: 3, within: 3s } }` for sure and on
`fewerThan: 3` as a chance, since the hits of earlier steps may make up the three (`'within' is the window of a hit count,
so it needs 'atLeast'`). The designer's trigger palette offers, for
each ability and weapon, every `atLeast` count an equipped rule needs from it, named ("2 in 3 s · Arc Siphon", "3 in 3 s · One for All",
"3 · Spark of Discharge"; rules of one count with different windows each say theirs: "2 · Spark of Amplitude in 4 s,
Harmonic Siphon in 3 s"), beside the counts up to the last that still fills something
([loop-format.md](loop-format.md#action-tokens)).
A kill hits too, so a kill action also fires `damage … atLeast`. Only player actions (abilities and
weapons) fire `atLeast` triggers, once per action — keyword strikes, strikes by this weapon and summons hit one enemy each.

A **finisher** is the player's step `finisher` ("Finisher", [loop-format.md](loop-format.md#action-tokens)). It fires
the `finisher` rules, then kills one enemy of the pack: a hit and a kill whose origin is the finisher. A finisher has
no damage type and is no weapon or ability, so of the `damage` and `kill` rules only those whose `via` is `any` fire on
it — `{ kill: { via: any, targetHas: [jolt] } }` on a finisher of a jolted enemy; `kill: { via: melee }` and
`type:arc` never. `finisher` takes no keys (`{ finisher: {} }`; anything else in it is an error). The designer offers
the step only when an equipped rule has a `finisher` trigger. What a finisher spends is counted (Armor Charge, ADRs D33):
"x3 or more" is `when: [ { holds: { status: armor-charge, atLeast: 3 } } ]` and spends all of it (`convertStacksToEnergy`
with a table `bySpent`, or `removeBuff` and an outcome whose `durationBySpent` reads what it spent); "spends x1 / x2" is
`has` (or `holds` 2) and `{ spendStacks: { status: armor-charge, count: 1 } }`. No `chance: true`: the count says
whether it fires for sure, as a chance or not at all.

`buffGained` fires on every `applyBuff` of a stacking status (one with `maxStacks`), and on an
`applyBuff` of a non-stacking status only when it wasn't active. `stacksMaxed` fires when the player
declares the buff at its maximum (`max:bolt-charge`, "Bolt Charge at max" —
[loop-format.md](loop-format.md#action-tokens), ADRs D3), never from a grant; `explain` heads its
rules "Max Bolt Charge". A `stacksMaxed` (or `atMax`) on a status without `maxStacks` is a parse error:
`'amplified' doesn't stack (no maxStacks), so it is never at max`.

**Damage sources (`via`, `against`)**:

| YAML | Domain `DamageSource` |
|---|---|
| `any` | `AnySource` |
| `weapon` | `AnyWeapon` |
| `weapon:strand` | `WeaponOfType(Strand)` |
| `weapon:harmonic` | `WeaponOfSuperType` — a weapon of your Super's element (the Harmonic mods): of the build's Super's `affinity` in the catalog; with the Super `?` (or one of no element) it matches nothing |
| `slot:power` | `WeaponInSlot(Power)` — the build's weapon in that slot (`kinetic\|energy\|power`): an exotic weapon's "this weapon", since an exotic always sits in its own slot |
| `weapon:this` | `ThisWeapon` — the weapon holding the element: only in a `weaponPerk`, `weaponMod`, `exoticWeapon` or `catalyst` (`weapon:this is the weapon holding the element: only a weaponPerk, weaponMod, exoticWeapon or catalyst has one, not a armorMod`) |
| `weapon:this:arc` | `ThisWeaponOfType(Arc)` — the weapon holding the element, when it is of that type: a rule (or a passive, or a damage change) for "this weapon, when Arc" is part of the build only on an Arc weapon (Synergy's Ionic Trace) |
| `ability` | `AnyAbility` |
| `grenade` / `melee` / `classAbility` / `super` | `AbilityOf(kind)` — every use of it, its secondaries too |
| `melee:slide` / `melee:air` / `classAbility:air` / `grenade:hold` (`<ability>:<input>`) | `AbilityUsedAs(kind, input)` — that use only: Tempest Strike's uppercut is `melee:slide` |
| `type:arc` | `OfType(Arc)` — any weapon, ability, keyword or summon of that type |
| `keyword:bolt-charge` | `KeywordOf(status)` — keyword damage (lightning strike, jolt chain…); any glossary status |
| `summon:threadling` | `SummonOf(summon)` |

**This weapon** (ADRs D31). A weapon perk's or mod's rules name no weapon: `weapon:this` is the one holding it. The build check
binds it (`Causality.WeaponBinding`): it equips the element as that weapon's — `weapon:this` becomes that weapon's slot,
`weapon:this:<type>` too when the weapon is of that type, and a rule, passive or damage change for another type is left
out — so two weapons holding Synergy are two elements, each with its own weapon's rules (not stacked copies). Unbound
(the catalog's own text: an item's details), it reads "this weapon" and matches nothing. The perks of
`weapons/perks.yaml` say `weapon:this` (Slice, Attrition Orbs, Demolitionist). A `reloadWeapon: weapon:this` and an energy
grant `byArchetype` are bound too: the reload is that weapon's, and the grant takes the amount for its archetype.

Types are `kinetic|arc|solar|void|stasis|strand`. A weapon deals its build `type`; an ability the
subclass's (kinetic on Prismatic: per-ability types aren't modelled); a keyword strike its status's
affinity; a summon its glossary `damageType`. The class ability deals no damage — it is only cast. A finisher has no
type: only `any` matches it. `weapon:harmonic` reads with the build's Super named — "Kill target with Arc weapon
(your Super's element)", "+20% damage with Arc weapon (your Super's element)" — and "weapon of your Super's element"
when the Super is `?`.

**Conditions (`when`)**: `{ has: <status> }` · `{ lacks: <status> }` · `{ targetHas: <status> }` ·
`{ atMax: <status> }` · `{ belowMax: <status> }` · `{ during: <ability> }` · `{ holds: { status: <counted status>, atLeast: N } }`
— all must hold, checked against the state when the event is processed. On a counted buff (ADRs D33) `has`, `lacks` and
`holds` read its stacks: they hold for sure when the sure stacks meet them, by chance when only the chance ones do —
the rule then fires marked *(chance)*, a passive holds as a chance — and not at all otherwise; a rule whose `holds`
alone fails leaves a note on the step (`Bulwark Finisher needs x3 Armor Charge, holds x1 — it doesn't fire.`).
`holds` on a status that isn't counted is an error (`'bolt-charge' isn't counted (no counted: true in glossary.yaml), …`).
A rule fired by an event a chance rule raised, or by picking up a pickup only chance rules dropped, is a chance too.
`during` (`grenade|melee|classAbility|super`) holds for the whole of a step that casts that ability, any use of it
(`class` and `class:air` are both the class ability), and on no other step: what is under way at the step's end, such
as Flow State's dodge (`when: [ { has: amplified }, { during: classAbility } ]`: 66% [PvP 32%] damage resistance on a
`class` step while Amplified, Compendium Arc#49). `explain` reads it `(during your class ability)`. Being in your
Super is not a `during`: a Super lasts for several steps, so casting it applies the `super` status (the `super`
keyword element) until the player declares `end:super`, and a Super's resistance is `when: [ { has: super } ]`
(Arc Staff: 90% [PvP 53%], Arc#44).
`atMax` holds while the player has that buff declared at max; `explain` reads a guarded rule
`(while Bolt Charge at max)`. It is how a rule reacts to the next hit at the maximum: Bolt Charge
(Compendium Arc#5) discharges on the next ability hit at x10.

```yaml
  - on: { damage: { via: ability } }
    when: [ { atMax: bolt-charge } ]
    then: [ { removeBuff: bolt-charge }, { strikeTarget: { via: bolt-charge, hit: kill } } ]
```

So declaring "Bolt Charge at max" fires Shinobu's Vow (New Tricks, ~40% grenade energy, a heal) and
Flashover (Amplified 15s), and the next ability hit fires the strike, which spends the buff.

`belowMax` (a status that stacks, like `atMax`) holds while the buff is on you and not declared at max; `explain` reads
it `(while Armor Charge below max)`. With `atMax` it splits a threshold the state doesn't count (ADRs D1): a rule that
needs "x3 or more" of a status fires for sure at the declared max and as a `chance` below it (ADRs D8). Armor Charge is
counted (ADRs D33), so its rules read the count instead — Emergency Reinforcement (`rules/mods/chest.yaml`):

```yaml
  - on: { criticalHealth: {} }
    when: [ { holds: { status: armor-charge, atLeast: 3 } } ]
    then:
      - { removeBuff: armor-charge }
      - { applyBuff: { status: emergency-reinforcement, durationBySpent: { from: 3, values: "6 | 9 | 12" } } }
```

A **critical health** step is the player's declaration (`health:critical`, "Critical health",
[loop-format.md](loop-format.md#action-tokens), ADRs D3): no damage you take is modelled, so only the player can say
your shields broke. `criticalHealth` takes no keys (`{ criticalHealth: {} }`); the designer offers the step only when an
equipped rule has the trigger, and the loop graph draws it as a "Critical health" node the player declares.

A **broken shield** and an ability's **last charge** are options of a step the player turns on (ADRs D36,
[loop-format.md](loop-format.md#action-tokens)). A weapon step that breaks a shield (`energy:kill:shield`) raises
`ShieldBroken` from that weapon after its first hit: `shieldBroken` takes only `via`, matched as a hit's (Shield Break
Charge says `{ shieldBroken: {} }`: the player breaks a shield only with a weapon of its element). A grenade, melee or
class ability step raises `EnergyExpended` right after its cast when it spends the last charge: on every use when the
build gives the ability one charge (its profile's `charges` plus every equipped `extraCharges`), on a use marked `:last`
when it gives more (Shinobu's Vow). The Kickstarts fire on it, as Bungie's text says ("when your grenade energy is fully
expended"):

```yaml
  - on: { energyExpended: grenade }       # Grenade Kickstart (rules/mods/armor.yaml)
    when: [ { has: armor-charge } ]
    then:
      - { convertStacksToEnergy: { consumed: armor-charge, to: grenade, byCharges: { from: 2, values: "16.2% | 21.7% | …" } } }
```

A Super has no `energyExpended` (`energyExpended: a Super has no energyExpended (use grenade, melee or classAbility)`).
The loop graph leads from each weapon to the `shieldBroken` rules and from each ability to its `energyExpended` ones.

A **reload** (ADRs D37) is the player's Reload step (`kinetic:reload`, "Reload Festival Flight",
[loop-format.md](loop-format.md#action-tokens)) or a `reloadWeapon` outcome: either raises `Reloaded` from that weapon,
which the `reload` trigger matches. No ammo is counted, so a reload is never needed and never blocked (but for an empty
slot), and what a weapon does to its magazine (Demolitionist's refill, Return Stroke's 5 rounds) is one reload, its
numbers a fact. `reload` takes only `via`, and it names a weapon: `any`, `weapon`, `weapon:<type>`, `weapon:harmonic`,
`slot:<slot>`, `weapon:this` or `weapon:this:<type>` (`reload: { via: grenade }` is an error,
`reload's via names a weapon: …`). Demolitionist (`rules/weapons/perks.yaml`, Compendium Weapon Perks#62) has both
halves of its loop:

```yaml
    rules:
      - on: { kill: { via: weapon:this } }
        then:
          - { grantEnergy: { to: grenade, amount: "10%", byArchetype: { archetypes: [fusion-rifle, glaive, shotgun, sniper-rifle], amount: "20%" } } }
      - on: { abilityCast: grenade }
        then:
          - { reloadWeapon: weapon:this }
```

The loop graph leads from each weapon to its `reload` rules, and from a `reloadWeapon` outcome both to them and to the
weapon's "Shoot" node: its magazine refilled, it shoots again (Demolitionist's loop: grenade → reload → kills → grenade
energy → grenade).

**Outcomes (`then`)** — exactly one key each:

| YAML | Domain `Outcome` |
|---|---|
| `{ grantEnergy: { to: grenade, amount: "15%" } }` / `amount: full` | `GrantEnergy(to, Fraction(v) \| Full)` |
| `{ grantEnergy: { to: leastCharged, amount: "10% \| 13% \| 15%" } }` — the ability with the least energy | `GrantLeastChargedEnergy(Fraction(v) \| Full)` |
| `{ grantEnergy: { to: grenade, amount: "10%", byArchetype: { archetypes: [shotgun, glaive], amount: "20%" } } }` — another amount on a weapon of those archetypes (Demolitionist); `amount` is a fraction, not `full` | `GrantEnergy(to, ByArchetype(v, archetypes, w))` |
| `{ convertStacksToEnergy: { consumed: armor-charge, to: grenade, perStack: "?" } }` | `ConvertStacksToEnergy(…, PerStack)` |
| `{ convertStacksToEnergy: { consumed: armor-charge, to: grenade, bySpent: { from: 3, values: "70% \| 80% \| 90% \| 100%" } } }` — the energy by the stacks spent (x3, x4…) | `ConvertStacksToEnergy(…, BySpent)` |
| `{ convertStacksToEnergy: { consumed: armor-charge, to: grenade, byCharges: { from: 2, values: "16.2% \| 21.7% \| …" } } }` — by charges: each copy and each stack spent counts one (the Kickstarts) | `ConvertStacksToEnergy(…, ByCharges)` |
| `{ spendStacks: { status: armor-charge, count: 1 } }` — a fixed number of a counted buff's stacks | `SpendStacks` |
| `{ applyBuff: { status: bolt-charge, stacks: 1, duration: 10s } }` / `{ applyBuff: amplified }` | `ApplyBuff` (stacks default 1) |
| `{ applyBuff: { status: armor-charge, stacks: "1 \| 2" } }` — stacks by copies of the element (Shield Break Charge: x1 with one copy, x2 with two; the last for more) | `ApplyBuff` (`StackGrant.PerModCount`) |
| `{ applyBuff: { status: void-overshield, durationBySpent: { from: 3, values: "8 \| 10 \| 12 \| 14" } } }` — seconds by the stacks the same rule spent before it (Bulwark Finisher); not with `duration` | `ApplyBuff(…, DurationBySpent)` |
| `{ removeBuff: new-tricks }` | `RemoveBuff` |
| `{ debuffTarget: { status: jolt, duration: 4s } }` / `{ debuffTarget: jolt }` | `DebuffTarget` |
| `{ spawn: { pickup: orb-of-power, count: 1 } }` / `{ spawn: orb-of-power }` | `Spawn` (count default 1) |
| `{ summon: { summon: threadling, count: 1 } }` / `{ summon: threadling }` | `SpawnSummon` (count default 1) |
| `{ strikeTarget: { via: bolt-charge, hit: kill } }` (`damage\|kill`; `via` names a status) | `StrikeTarget` |
| `{ strikeTarget: { via: "weapon:this", type: kinetic, hit: damage } }` — this weapon's damage dealt by an effect (Veteran's Wisdom's shockwave, Radiolaria Transposer's explosion and pool): it hits as the weapon holding the element, so every rule on that weapon's damage reacts to it; `type` (optional) is the strike's damage type, else the weapon's own; only in an element a weapon holds, like `weapon:this` | `WeaponStrike` |
| `{ modifyDamage: { against: keyword:bolt-charge, change: "+50%" } }` | `ModifyDamage` |
| `{ restoreHealth: { amount: "?", allies: true } }` (`allies` default false) — `amount` a share of your health; `{ hp: 65 }` instead: HP (Unrelenting) | `RestoreHealth(Share \| Points, allies)` |
| `{ resetCooldown: melee }` | `ResetCooldown` |
| `{ collect: orb-of-power }` | `Collect` — picks the pickup up from the ground (Powerful Attraction) |
| `{ reloadWeapon: weapon:this }` / `{ reloadWeapon: slot:power }` — reloads that one weapon: `weapon:this`, `weapon:this:<type>` or `slot:<slot>` | `ReloadWeapon` |

Statuses in `applyBuff`/`removeBuff`/`convertStacksToEnergy.consumed`/`has`/`lacks`/`atMax`/`buffGained`/`stacksMaxed`/`extraStacks`
must be glossary `buff`s; in `debuffTarget`/`targetHas` glossary `debuff`s
(`'jolt' is a debuff, but this position needs a buff`); `strikeTarget.via` and `keyword:` take either (or, for `strikeTarget`, `weapon:this`: `only this weapon strikes`).
`spawn`/`pickUp`/`collect` name glossary pickups, `summon`/`summon:` glossary summons.

* **`applyBuff`** makes the buff present; `stacks` is what one grant gives, as the source states it.
  A stacking status shows the grant — `+1 Bolt Charge`, `+1 Slice (8s)`; a non-stacking one shows the
  status — `Amplified (15s)` — and applying it while it is active changes nothing (caveat
  `already active`). The duration shown is the rule's, else the glossary's.
  A counted buff's grant (ADRs D33) adds its stacks, and every `extraStacks` that holds (Stacks on Stacks), to the sure
  stacks — to the chance ones when the rule fires as a chance — up to the build's cap; the rest is wasted (caveat
  `at cap: +1 wasted`), and sure stacks at the cap put the buff at max, raising `StacksMaxed` as a declaration does.
* **`removeBuff`** removes the buff, and its declaration at max; a counted buff spends every stack.
* **`convertStacksToEnergy`** consumes the buff if it is active — removes it and its declaration — and
  shows the energy as a fact: `spends Armor Charge → +?% grenade energy per stack`. A table (a counted buff) gives
  the value for the stacks it spent, and beside it the value with the chance ones:
  `spends Armor Charge x2 · +1 (chance) → +28.4% grenade energy (34.4% with the chance stack)`; a count outside the
  table is `?`. With the buff not active it carries the caveat `nothing to consume`.
* **`spendStacks`** takes that many of a counted buff's stacks, sure ones first, then chance ones, never below none;
  the buff ends when none are left. With none it carries the caveat `nothing to spend`.
* **`grantEnergy` to `leastCharged`** (Orbs of Restoration, Restorative Finisher) is a fact like any energy grant:
  `+10% energy to your least-charged ability` (by copies). The game picks the ability, so the loop graph draws no
  arrow from it to one ability's energy.
* **`debuffTarget`** puts the debuff on the pack, where it stays until the player ends it; no rule
  removes a debuff.
* **`collect`** picks the pickup up when it is on the ground — it leaves the ground and its pickup
  event follows, so every `pickUp` rule fires as if the player had picked it up (Recuperation, Orb of
  Power's Armor Charge). With none on the ground it carries the caveat `nothing on the ground`. The
  loop analysis counts the pickup as a need of the step, provided by the step that dropped it.
* **`grantEnergy` `byArchetype`** is the amount of the weapon holding the element: bound to a weapon of a named archetype
  it is `amount` of `byArchetype` (`+20% grenade energy`), to any other weapon the plain `amount` (`+10% grenade
  energy`). A weapon whose archetype isn't known (one whose hash the manifest excerpt doesn't know, or a weapon picked
  without its manifest type) and the catalog's own text show both: `+10% grenade energy (20% on Fusion Rifles, Glaives, Shotguns
  and Sniper Rifles)`. An enhanced trait's `values` replace both amounts.
* **`reloadWeapon`** reloads the weapon "this weapon" is bound to, or the build's in that slot: `reloads your energy
  weapon`, and a `Reloaded` event from it follows, so its `reload` rules fire as on a Reload step (ADRs D37). With no
  weapon there, or "this weapon" not bound (the catalog's own text, `reloads this weapon`), it carries the caveat
  `no weapon to reload`.

What changes the state or sets off events: `applyBuff`, `removeBuff`, `debuffTarget`, `spawn`,
`summon`, `strikeTarget`, `collect`, `spendStacks`, `reloadWeapon` (its reload event), and the consumption of
`convertStacksToEnergy`. The rest are **facts**, shown with their value and
certainty and changing nothing: `grantEnergy` (to an ability or `leastCharged`), the energy of `convertStacksToEnergy`, `resetCooldown`,
`modifyDamage` and `restoreHealth` (ADRs D1, D5).

### Rules that don't stack

**`doesNotStackWith`** — optional, a list of element ids. When the game says
two elements' grants don't stack, say it on the rule of the side that gives nothing (ADRs D6):

```yaml
  - id: tempest-strike                  # rules/hunter/arc.yaml — Compendium Arc#51
    rules:
      - on: { kill: { via: any, targetHas: [jolt] } }
        then:
          - { applyBuff: { status: bolt-charge, stacks: 1 } }
        doesNotStackWith: [dielectric]  # Dielectric's x1 on the same kill is the one you get
        reason: "Killing a jolted enemy gives you a stack of Bolt Charge."
```

* When, on the same event, a rule of a listed element also fires (its trigger matches and its
  guards hold), this rule **gives nothing**: it is reported as fired with no outcomes, naming the
  element it gave way to (`FiredRule.NotStackedWith`). The trace shows the bullet as
  `doesn't stack with Dielectric [Tempest Strike]`. It gives way even if that element's rule itself
  gave way to a third one.
* Without a listed element equipped, or on an event no rule of it matches, the rule applies as usual.
* With both equipped, the build check notes it (info, in every build summary: `explain`, `validate`, the web…) —
  `'Tempest Strike': +1 Bolt Charge on "Kill Jolted target" doesn't stack with 'Dielectric'.` — a fact for the
  build crafter, who may keep both for what else each gives;
  `explain` annotates the bullet (`+1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike]`),
  and a loop's analysis shows it as **wasted** at each step where it happens ([loop-format.md](loop-format.md)).
* In the loop graph (`graph`, `loops`), where this rule and the rule it gives way to lead from the same
  trigger to the same node, both arrows go into a **Doesn't stack** node and only the partner's arrow
  leaves it (`Kill Jolted target →[Tempest Strike, Dielectric] Doesn't stack →[Dielectric] Gain Bolt Charge`);
  rules of other elements on that trigger and target keep their direct arrow. The graph is static, so it
  pairs only rules with the *same trigger* — an approximation of the engine's same-event rule. The node
  isn't a step: a loop's length limit, its "N steps" and the loop order don't count it.
* The whole rule gives way, not one outcome: an outcome that does stack goes in a rule of its own.

Parse errors are checked over the whole catalog once every file is read, and reported at the
element's `file:line`:

* `'<id>': doesNotStackWith '<x>' is not an element of the rules`
* `'<id>': doesNotStackWith names its own element`
* a `doesNotStackWith` that leads back to its own element — two elements naming each other, or a
  longer circle (A → B → C → A): every rule in it would give way and none would apply. An error at
  each element of the circle: `'<a>': doesNotStackWith '<b>' leads back to '<a>', so none of those rules would apply; say it only on the side that gives nothing`

An empty list is an error at its own line: `rule.doesNotStackWith must list at least one element`.

### Passives

```yaml
passives:
  - effect: { extraStacks: { status: bolt-charge, extra: 1 } }
    when: [ { has: amplified } ]
    reason: "While Amplified, Bolt Charge sources grant one extra stack."
```

`effect` (exactly one key) is required, `when` and `reason` are optional.

| YAML `effect` | Domain `Passive` |
|---|---|
| `{ extraStacks: { status, extra } }` (`extra` ≥ 1) | `ExtraStacks` |
| `{ extraCharges: { ability: grenade, extra: 1 } }` | `ExtraCharges` (a fact, ADRs D5) |
| `{ modifyDamage: { against, change } }` | `ModifyDamage` |
| `{ resistDamage: "25% [10%]" }` (any damage; `[…]` the PvP value) | `ResistDamage` |
| `{ resistCombatantDamage: { against: arc, amount: "15% \| 25% \| 30%" } }` (combatants only; no `[…]`) | `ResistCombatantDamage(CombatantDamage.OfType(Arc), amount)` |
| `{ modifyWeaponStats: { archetypes: [fusion-rifle], changes: [ { stat: handling, change: "?" } ] } }` — the build's weapons of those archetypes; `weapon: this` instead of `archetypes`: the weapon holding the element (Stats for All), bound like `weapon:this` | `ModifyWeaponStats(OfArchetypes \| ThisWeapon, changes)` (non-empty lists; archetypes and stats are kebab-case words; a change is stat points, `"+35"`) |
| `{ raiseMaxStacks: { status: armor-charge, max: "4 \| 5 \| 6" } }` (a status with `maxStacks`; no `when`) | `RaiseMaxStacks` — the cap with it equipped, by copies |
| `{ modifyStat: { stat: grenade, change: "+20 \| +40 \| +50" } }` (`health\|melee\|grenade\|super\|class\|weapons`; stat points, not a percent) | `ModifyStat(stat, change)` — "+20 Grenade" |

Passives are facts and change no state. Each is shown with the value for the copies equipped (Weapon Surge
`"+10% | +17% | +22% | +25%"` with two copies: `+17% Arc weapon damage`; Charged Up with one: `Armor Charge up to x4`).
While its `when` holds, `extraStacks` adds a caveat to every
`applyBuff` of that status (`+1 from Spark of Frequency`); the others are shown in `explain`, and,
when they have a `when`, in the trace's state while it holds. `raiseMaxStacks` states the build's cap
of a stacking status while it is equipped, as the source gives it by copies (Charged Up: Armor Charge
`"4 | 5 | 6"`, so x4 with one copy; the glossary's x3 without it) — never added up (ADRs D1). Two elements
raising the same cap make it `"?"`: no source says how they combine. It always holds, so a
`raiseMaxStacks` with a `when` is an error (`raiseMaxStacks raises the build's cap, which always holds, so
it takes no when`). The cap is a fact: it shows in the status's facts (`up to x4`), in the declared step
(`Armor Charge at max (x4)`) and in `explain`'s max heading (`Max Bolt Charge (x10)`); the player still
declares it reached (ADRs D3).

`resistDamage` and `resistCombatantDamage` are damage resistances, shown in `explain` and counted in the step's
**damage resistance** (ADRs D29, [loop-format.md](loop-format.md#action-tokens)) while their `when` holds.
`resistDamage` reduces any damage: the `[…]` is its PvP value, and without one its PvP value is `?` (write
`"20% [20%]"` when they are the same). `resistCombatantDamage` reduces only combatants' damage, so it does nothing in
PvP and its `amount` takes no `[…]` (`a resistance against combatants does nothing in PvP, so it has no PvP value: drop
the [ ]`). Its `against` is `combatants` (all of it: the Guardian's base), a damage type (`arc`, `solar`, `void`,
`stasis`, `strand`), `harmonic` (the Super's element), `"within:5"` / `"beyond:30"` (combatants within or beyond that
many metres: Melee and Sniper Damage Resistance) or `blast` (area, blast and explosive damage: Concussive Dampener).
Copies of one mod take its value by copies (`"15% | 25% | 30%"`); different sources multiply what the others let
through, 1 − (1 − a) × (1 − b) × … (compendium Armor Mods#7), never added up.

`modifyStat` (the Font mods: `+20 | +40 | +50` Grenade while Armor Charge, `when: [ { has: armor-charge } ]`) is a fact
shown in `explain` and, while its `when` holds, in the step's state (`+20 Grenade [Grenade Font]` under Armor Charge).
It is never added to the build's stats: the build's stat line and the `statChecks` that read it don't count it (ADRs D1, D28).

## Build file (`builds/<build>/build.yaml`)

```yaml
# abridged and illustrative — the real one is builds/skip-grenade-hunter/build.yaml
name: Skip Grenade Hunter
author: Plunderthabooty                      # optional
source: https://www.youtube.com/watch?v=zvd6sNS463E   # optional
catalog: authored-0123456789ab               # optional pin (ADRs D15)
class: hunter
subclass: arc                                # arc|solar|void|stasis|strand|prismatic
super: arc-staff
grenade: skip-grenade
melee: combination-blow
classAbility: gamblers-dodge
jump: triple-jump                            # optional
aspects: [tempest-strike, flow-state]
fragments: [spark-of-resistance, spark-of-frequency, spark-of-shock, spark-of-discharge]
exoticArmor: shinobus-vow                    # optional
armorSetBonuses: [luminopotent-2pc, luminopotent-4pc]
armorMods: [elemental-charge, elemental-charge, grenade-kickstart, grenade-kickstart, bomber]   # repeat = stacked copies
artifact: { hash: 3821685388, slots: [3585856470, 3585856464, 2596646298, "?", "?", 1319563986, "?"] }   # optional
artifactPerks: [defibrillating-blast, flashover]
weapons:
  - { slot: kinetic, name: Festival Flight, type: strand, archetype: grenade-launcher, perks: [slice] }
  - { hash: 3325463374, catalyst: true }                   # Thunderlord, its catalyst on
  - { hash: 4019651319, mod: 3820243349 }                  # Festival Flight, Synergy in its mod socket
  - { hash: 2888266564, roll: ["?", 2209918983] }          # Crisis Inverted, a perk per trait column
stats: { weapons: 47, class: 104, grenade: 145, super: 27, melee: 79 }   # any subset; 0..200
```

* Required: `name`, `class` (`hunter|titan|warlock`), `subclass`.
* The abilities `super`, `grenade`, `melee`, `classAbility`: each an element id, or `"?"`: the build has one, but
  Loopsmith can't name it (a DIM link's super with no hash in the catalog). It shows as "?" and sets nothing off. An
  ability left out is an empty slot (a build started from scratch, [loop-format.md](loop-format.md#starting-from-scratch)):
  it shows empty, sets nothing off, and the build check doesn't call it unknown.
* `jump`: a `kind: jump` element id, or `"?"`, like the abilities; it sets nothing off either way. Left out, the build
  has no jump the rules know: none, or one of a subclass the rules don't have, which `leftOut.subclassPlugs` holds
  (a build file from before jumps were elements may hold an Arcstrider jump there too; it still opens and shows it,
  and the subclass editor's Save writes it as `jump:`).
* `artifact`: the artifact the build equips, `hash` (one of the excerpt's `kind: artifact` items), and the perk in
  each of its perk slots, `slots`, by manifest hash in slot order, `"?"` for an empty slot, so each perk is read back
  into the slot it was put in. The designer's artifact editor writes it ([loop-format.md](loop-format.md#artifact)),
  together with `artifactPerks` (the rules' perks, in slot order) and `leftOut.artifactPerks` (the others): those are
  what the build equips. Left out, the build's artifact is the one that unlocks the most of its `artifactPerks` (a build
  file, a DIM link), its perks placed in its slots (the highest tiers first, each in the free slot that takes the
  fewest tiers); so are slots that no longer hold exactly the build's perks.
* Optional: `author`, `source` (an absolute http(s) URL), `catalog`, `exoticArmor`, `artifact`, and the lists
  `aspects`, `fragments`, `armorSetBonuses`, `armorMods`, `artifactPerks`, `weapons` (default empty).
* A weapon is given by its `hash` or named in full.
  * By `hash`, its manifest item: its slot, name, damage type and archetype are read from Bungie's manifest when the
    file is read, and so are its icon, its trait columns, its intrinsic and catalyst. The manifest is the source of
    truth, so the file doesn't copy them: a `slot`, `name`, `type` or `archetype` given with a hash (a file written
    before) is ignored. A hash the manifest doesn't know as a weapon with a damage type is refused ("weapon hash 1234567
    isn't in the manifest, so the file can't be imported"). The CLI reads the excerpt (`rules/manifest.yaml`; rerun
    [tools/manifest/](../tools/manifest/README.md) for a weapon it lacks); the designer reads Bungie's full manifest for
    the hashes the excerpt lacks before it imports a build or loop file, or opens a shared loop. The file writer writes
    a weapon given by hash as its hash and what the build chooses for it, with its name in a comment after it on its
    line (`- {hash: 4019651319, perks: [slice]} # Festival Flight`; not in a flow list, which has no room for one).
  * Named in full, without a hash: `slot` (`kinetic|energy|power`), `name`, `type`
    (`kinetic|arc|solar|void|stasis|strand`) and `archetype` are required: a weapon without an archetype is refused
    ("weapon 'Any Arc weapon' needs an \`archetype\` or a \`hash\`"), since a perk that goes by its archetype would show
    both amounts. `archetype` is a kebab-case word, the manifest's type (`hand-cannon`); it picks a perk's energy grant
    `byArchetype` (Demolitionist's).
  * `perks` (element ids), `roll`, `catalyst`, `instance` and `mod` are optional either way. The name labels its
    actions ("Festival Flight (🥊 hit 5)").
* `instance`: the copy the build wears, by Bungie's instance id (a dim.gg share's, or one picked from the player's
  weapons; quoted: `instance: "6917529810172542011"`); after the player's Bungie login its tier and masterwork show on
  the weapon's square when it is theirs, and it holds that copy's perks
  ([loop-format.md](loop-format.md#weapons-from-your-account)).
* `mod`: the weapon mod in its mod socket, by manifest hash (one; none: no mod, or the build doesn't say). The
  catalog's `weaponMod` element with that hash is equipped as that weapon's (Synergy's "this weapon",
  [above](#rules)); a mod the catalog doesn't know is part of the weapon, not of the trace. A DIM link's weapon gives
  it when its socket overrides hold one, and so does a weapon picked from the player's own; the designer's mod picker
  writes it.
* `catalyst: true` turns an exotic's catalyst on (default `false`; a DIM link doesn't say, so its build starts
  off): the catalyst element (`kind: catalyst`, by the catalyst item's hash) is equipped only then. The weapon's
  intrinsic perk is equipped by hash, like its columns' perks.
* `roll`: the perk picked in each of the weapon's trait columns, by manifest hash, in column order; `"?"`
  for a column not picked (`roll: ["?", 2209918983]`). It is what the designer's perk picker writes. A
  column holds one perk: the roll's pick, else the perk `perks` names in it, else (a column that doesn't
  roll, an exotic's) its only one. The weapon's perks are what its columns hold that the catalog knows by
  hash, and the perks `perks` names that are in no column; the designer shows exactly these. A perk the
  catalog doesn't know (an enhanced version, say) is part of the weapon, not of the trace.
* `stats`: any of `health`, `melee`, `grenade`, `super`, `class`, `weapons` (the game's order; any order reads),
  whole numbers 0..200.
* `leftOut` (a build from a DIM link): what the loadout had that the catalog doesn't know, by manifest hash
  and where the loadout listed it — `leftOut: { items: [2005], subclassPlugs: [1001], armorMods: [3007],
  artifactPerks: [4008] }` (any subset). Shown with the build, never played.
* `armorCopies` (a build from a dim.gg share, or with pieces picked from the player's armor): which copy of its
  armor pieces it wears — `armorCopies: [{hash: 593554567, instance: "6917530185805135602"}, …]`, the piece's
  manifest hash and the copy's instance id (a number, quoted). Unknown to the rules; the Stats section adds the
  stats up from these copies when the logged-in player owns them ([loop-format.md](loop-format.md#stats)).
* `armorModPlugs`: which copy of an armor mod the rules know the build holds, by manifest hash, for a mod one of
  whose copies isn't its element's first hash (many mods have a cheaper copy the seasonal artifact allows: Arc Siphon
  3 energy, or 1). It lists every copy of such a mod, in `armorMods`' order: each of the element's mods holds the next
  one of these that is its element's, else its first hash (`armorMods: [arc-siphon, arc-siphon]` with
  `armorModPlugs: [2724068510, 3847471926]`: one of each). Written by a DIM link and by the designer's mod picker
  ([loop-format.md](loop-format.md#armor-mods)); a mod not named here holds its element's first hash. The copy
  decides only its energy cost: both play alike.
* Stats and archetypes are shown in the build summary, and hashes are recorded (D14); the engine
  doesn't use them.

### Build check

`BuildValidation` checks the build against the catalog. Blocking issues stop it
(`Build '<name>' is invalid:` and one `✗` line each); warnings (`!`) and info (`i`) show in its summary.
What the rules don't know yet — a `"?"` ability, a weapon perk, mod or catalyst the catalog has no element for,
what a DIM link left out — is part of the build, not of the trace, and no line here: its square carries the ⊘
([above](#elements)), and the designer lists the left-out items under "Not in Loopsmith yet". An `inert` element says
nothing either. What Loopsmith doesn't support — a DIM link's raid, activity or seasonal mods — gets one info line.

| Issue | Severity |
|---|---|
| `Unknown <slot> '<id>' — no such element in the rule catalog.` | Blocking |
| `'<name>' (<Kind>) can't go in the <slot> slot.` — each slot takes its own kind; a weapon's perks take `weaponPerk` or `exoticWeapon`, its catalyst `catalyst`, its mod `weaponMod`; a `keyword` element goes in no slot | Blocking |
| `'<name>' belongs to another class, not <Class>.` — the element's `class` isn't the build's | Blocking |
| `'<name>' (<Affinity>) does not fit the <Subclass> subclass.` — a super, grenade, melee, jump, aspect or fragment of another affinity; neutral and kinetic ones fit any, and Prismatic takes any | Blocking |
| `<n> aspects equipped; at most 2.` · `'<name>' is equipped <n> times; aspects and fragments are unique.` · `<n> fragments equipped; the aspects grant <m> slots.` | Blocking |
| `Fragment slots unknown for <aspects> — fragment count not checked.` | Info |
| `<weapon>'s roll: <perk> isn't a perk of its column <n> in the manifest excerpt.` — only when the excerpt has the weapon's columns | Warning |
| `<weapon>: '<perk>' gives way to the other perk of its column — a column holds one perk.` — a perk `perks` names in a column the roll (or another named perk) already fills; it doesn't count | Warning |
| `<weapon> has no catalyst in the manifest excerpt, so catalyst: true does nothing.` | Warning |
| `<weapon>'s mod: <mod> isn't a mod its mod socket takes in the manifest excerpt.` — only when the excerpt lists what it takes | Warning |
| an equipped element's `statChecks` that the build's stats meet ([Elements](#elements)): `With 40 Melee, a Gambler's Dodge gives back less than a full melee charge …` | as the check says |
| a rule that doesn't stack with an equipped element ([above](#rules-that-dont-stack)) | Warning |
| `Build pinned catalog <pinned>; using <current>.` | Info |
| `Ignored from the DIM loadout: <mods> — raid, activity and seasonal mods aren't supported.` — the left-out mods the manifest says are an `activityMod`, each once with its copies (`Herd Thinner ×2`; [loop-format.md](loop-format.md#raid-activity-and-seasonal-mods)) | Info |

## Engine semantics (what an author can rely on)

A step is a player action or a declaration. The engine turns it into events, fires every rule an event
matches and cascades what those rules set off. The state a step plays against is what is present: the
buffs on you (and which of them the player declared at max), the debuffs on the pack, the pickups on
the ground.

* **Player actions → events.** `UseClassAbility` emits `AbilityCast(classAbility)` (and `EnergyExpended(classAbility)` on its last charge) — with the
  input `air` for `class:air` (the air move that spends the charge), which every `abilityCast:
  classAbility` rule matches too; only the rules that name the input tell the two apart. A
  secondary cast (`melee:slide:kill`) emits the same events as its press, each carrying the input:
  `AbilityCast(melee, slide)`, then hits and kills whose origin is `Ability(melee, type, slide)` —
  `via: melee` matches them, `via: melee:slide` matches only them.
  `CastAbility(k, hit, N)` (grenade, melee, super) emits `AbilityCast(k)`, then `EnergyExpended(k)` when the use spends
  the last charge (grenade and melee: every use with one charge, a `:last` one with more; ADRs D36), then N
  `Damaged(Ability(k, subclass damage type))` — one per enemy — then N `Killed(…)` if `hit = kill`,
  then one `TargetsHit(origin, N, hit)` for the `atLeast` triggers. `FireWeapon(slot, hit, N)` emits
  N `Damaged(Weapon(slot, type))` — with `ShieldBroken(Weapon(slot, type))` after the first when the step breaks a
  shield (`:shield`) — N `Killed` if a kill, then `TargetsHit`. `UseClassAbility` emits `EnergyExpended(classAbility)`
  after its cast the same way. N (the target count, 1..20)
  is the player's: the engine can't know how many enemies a hit catches (ADRs D4).
  `pickup:<id>` picks one pickup up: one `PickedUp(p)`, and that pickup is no longer on the ground.
  `PerformFinisher` (`finisher`) emits `Finisher`, then one `Damaged(Finisher)` and one `Killed(Finisher)` on the pack
  in front of you (one enemy, `minor`; no `TargetsHit`), and is never blocked. Only `via: any` matches the finisher
  origin.
* **Declarations** (ADRs D3, [loop-format.md](loop-format.md#action-tokens)). `max:<status>`
  ("Bolt Charge at max") emits `StacksMaxed(status)`, so the `stacksMaxed` rules fire and cascade, and
  marks the buff declared at max: `atMax` holds until a rule removes or consumes the buff, or the
  player ends it. `end:<status>` ("Amplified ends", "Jolt ends") removes the buff, with its
  declaration, or the debuff from the pack, and derives no event. `pack:new` ("New pack", ADRs D7) puts
  a new pack in front of you: none of the old pack's debuffs, the buffs on you and the pickups on the
  ground unchanged; it always holds and derives no event. `health:critical` ("Critical health") emits
  `CriticalHealth`, so the `criticalHealth` rules fire and cascade; it always holds and changes no state.
* **Abilities are always available** (ADRs D5). A step is blocked only when it can't happen at all —
  the pickup isn't on the ground (`No orb-of-power on the ground — nothing happens.`), no weapon in that
  slot, a `max:` on a buff that doesn't stack, isn't active or is already at max, an `end:` on a status
  that isn't active, a declaration naming no status of the rules
  ([loop-format.md](loop-format.md#action-tokens) has the messages).
  A blocked step changes nothing.
* Each event **fully cascades** before the next one of the same action is emitted, so a kill
  sees the debuffs its own hit applied (grenade hit → Spark of Shock jolts → the kill counts as
  "kill jolted target"), and the second enemy's hit sees what the first one's applied.
* **Kill/damage target statuses** are a snapshot of the target's debuffs when the event is emitted.
  The target is the pack in front of you (ADRs D7), `minor`; its debuffs stay after a kill, until the
  player ends them.
* **Derived events:** `applyBuff` of a stacking status → `BuffGained(status)` every time; of a
  non-stacking status → `BuffGained(status)` only when it wasn't active. `StacksMaxed(status)` comes
  only from a `max:` step. `spawn` of a `collectsAutomatically` pickup → one `PickedUp` per count,
  immediately (the source states the count), otherwise the pickups land on the ground. `summon` → one
  `Damaged(Summoned(…))` per summon. `strikeTarget` → `Damaged(Keyword(…))` (+ `Killed` if `hit: kill`).
  `debuffTarget`, `removeBuff`, `convertStacksToEnergy` and the facts derive no event.
* **Guards** (`when`) are checked against the state as it is when the event is processed, before any
  outcome of that event applies; `atMax` reads the player's declaration.
* **Phase order** of the outcomes fired by one event: Debuff → Empower (`applyBuff`,
  `removeBuff`, `modifyDamage`) → Damage (`strikeTarget`, `summon`) → Spawn → Refund
  (energy, health, cooldowns) → cascade the derived events (depth + 1). Within a phase, outcomes keep
  the equip order (the build's slots, then keyword elements) and each rule's own order.
* **Termination** (ADRs D23): cascade depth ≤ 5, and a rule never re-fires on an identical event up
  its own causal chain (A → B → A stops). Sibling occurrences — two traces spawned, the three enemies
  of `grenade:kill:3` — each fire.
* **Stacked mods:** an element equipped N times fires its rules once; `PerModCount` picks the
  N-th value (last value if N is larger), in its outcomes and in its passives.
* **`weapon:harmonic`** matches a weapon whose type is the damage type of the build's Super (its element's
  `affinity`); with the Super `?`, or one the catalog doesn't have, it matches nothing.
* **Rules that don't stack** (`doesNotStackWith`, ADRs D6): of the rules one event fires, a rule
  that lists another firing rule's element gives way — it is reported with no outcomes and
  `NotStackedWith` = that element's name, and its outcomes take no part in the phase order.
* **Facts** (ADRs D1, D5): energy outcomes (`leastCharged` too), `modifyDamage` and `restoreHealth` are shown with their
  value, certainty and a caveat when the value is missing (`amount unknown` / `value unknown`), and
  change no state. An `Unknown` value stays `?` (never 0). Ability profiles and
  `extraCharges` are recorded from the Compendium; only their charges are read, for the last charge (ADRs D36).
* `chance: true` rules fire and are marked *(chance)* in every view (ADRs D8) — on every enemy of a
  multi-target action.
