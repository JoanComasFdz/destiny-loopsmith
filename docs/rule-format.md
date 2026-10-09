# Rule & build file format (v1)

Causality is authored, not scraped: every build element is a YAML entry with **rules**
(`on` trigger → `then` outcomes) and **passives** (always-on modifiers). The YAML is a
boundary format — `RuleParsing` / `BuildParsing` turn it into the Domain types in
`src/Loopsmith.Core/Domain/` and nothing else ever sees the DTOs.

All ids are kebab-case slugs (lower-case letters, digits, inner hyphens: `shinobus-vow`, `bolt-charge`).
Keys are camelCase, and so are the closed vocabulary words (`classAbility`, `exoticArmor`).
Unknown keys, unknown ids and unknown statuses are **parse errors** — all errors of a file
set are reported together, each with `file:line`
(`hunter/arc.yaml:8: unknown key 'stack' in applyBuff (allowed: status, stacks, duration, restart)`).
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
  artifact/current.yaml
  weapons/perks.yaml
builds/<build>/build.yaml
```

The folders are a convention: every `*.yaml` / `*.yml` under the rules root is read, and element ids
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
  - { id: bolt-charge, name: Bolt Charge, kind: buff,   affinity: arc, maxStacks: 10 }
  - { id: jolt,        name: Jolt,        kind: debuff, affinity: arc, duration: 10s }
pickups:
  - { id: ionic-trace,  name: Ionic Trace,  affinity: arc,     collectsAutomatically: true }
  - { id: orb-of-power, name: Orb of Power, affinity: neutral, collectsAutomatically: false }
summons:
  - { id: threadling, name: Threadling, damageType: strand }
```

Each section is optional; ids are unique within a section. All keys shown are required except a
status's `maxStacks` (a whole number ≥ 1) and `duration`.

* `kind`: `buff` (on the player) · `debuff` (on the target).
  `affinity`: `neutral|kinetic|arc|solar|void|stasis|strand|prismatic`. A status's affinity is also the
  damage type of its `strikeTarget` strikes (neutral and prismatic strike as kinetic).
* A status **without `maxStacks` does not stack**: applying it again only refreshes it. `stacksMaxed`
  fires only for a `maxStacks` above 1.
* `duration` is used when an `applyBuff` / `debuffTarget` gives none. A status with no duration at all
  never expires (a buff lasts until a rule removes or consumes it).
* `collectsAutomatically: true` — collected the moment it spawns (Ionic Traces track to you);
  `false` — it lands on the ground until a `pickup:<id>` step ([loop-format.md](loop-format.md)).
* `damageType` (summons): `kinetic|arc|solar|void|stasis|strand`.

## Elements

```yaml
elements:
  - id: spark-of-shock
    name: Spark of Shock
    kind: fragment            # keyword|super|grenade|melee|classAbility|aspect|fragment|exoticArmor|
                              # exoticWeapon|armorSetBonus|armorMod|artifactPerk|weaponPerk
    affinity: arc
    class: hunter             # optional: hunter|titan|warlock
    hash: 1727069364          # optional manifest hash, an unsigned 32-bit number (from Clarity until the manifest join exists)
    description: "Arc grenades apply Jolt on hit."   # optional
    source: compendium/2026-10-09/Arc#24             # optional, see Provenance; omitted → authored at file:line
    rules:                    # optional (default none)
      - on: { damage: { via: grenade } }
        then:
          - { debuffTarget: jolt }
        reason: "Your Arc grenades jolt everything they hit."
    passives: []              # optional (default none)
```

`id`, `name`, `kind` and `affinity` are required. Two keys belong to some kinds only (anywhere else they
are an error):

* `fragmentSlots: 2` — aspects only (`fragmentSlots is only allowed on aspects, not on a fragment`), a
  whole number ≥ 0; the build check counts fragments against it.
* `ability: { kind: grenade, charges: 1, chunkScalar: 0.75, baseCooldown: 151.5 }` — super, grenade, melee
  and classAbility elements only, and `kind` must be the element's kind. `charges` defaults to 1,
  `chunkScalar` and `baseCooldown` (game values) to `"?"`. Kept, not used by the engine (ADRs D21).

`kind: keyword` elements are active in every build, once; every other kind is equipped through its
build slot ([Build check](#build-check)).

### Provenance (`source`)

| Form | Domain |
|---|---|
| `compendium/2026-10-09/Arc#54` | `Provenance.Compendium(snapshot, tab, row)` |
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
| `"~25%"`, `"25%?"` | `Approximate(0.25)` | shown `~25%` |
| `"?"`, `"?%"` | `Unknown()` | **never 0** |
| `"300% [20%]"` | PvE part `Known(3.0)` | `[…]` is the PvP value: checked, then dropped |
| `2`, `"2"` | `Known(2)` | plain number |

Game values appear in `grantEnergy.amount` (or `full`), `convertStacksToEnergy.perStack`, `modifyDamage.change`,
`restoreHealth.amount`, `resistDamage`, a weapon-stat `change`, and `ability.chunkScalar` / `baseCooldown`.
Decimals use `.`; quote the value as the examples do (a bare `?` or `~` means something else to YAML).

Durations: `5s`, `0.5s`; `"?s"`, `"?"` or omitted → no duration (`Optional.None`): an outcome then
uses the glossary's.

### Rules

```yaml
rules:
  - on: { kill: { via: any, targetHas: [jolt] } }
    when: [ { has: amplified } ]        # optional guards
    then:
      - { spawn: ionic-trace }
    chance: true                        # optional: "occasionally / chance to" (default false)
    reason: "Final blows against jolted targets create an Ionic Trace."
```

`on` and `then` (at least one outcome) are required. `reason` is optional plain English, shown in
traces with `--why`; `doesNotStackWith` is [below](#rules-that-dont-stack).

**Triggers (`on`)** — exactly one key:

| YAML | Domain `Trigger` |
|---|---|
| `{ abilityCast: grenade }` (`grenade\|melee\|classAbility\|super`) | `AbilityCast(kind)` |
| `{ abilityCast: { ability: classAbility, airborne: true } }` — only the class ability used in the air | `AbilityCast(classAbility, Airborne: true)` |
| `{ kill: { via: <source> } }` | `KillAny(via)` |
| `{ kill: { via: <source>, tier: champion } }` (`minor\|major\|boss\|champion`) | `KillOfTier(via, tier)` |
| `{ kill: { via: <source>, targetHas: [jolt, sever] } }` | `KillDebuffed(via, statuses)` |
| `{ damage: { via: <source> } }` | `Damage(via)` |
| `{ damage: { via: <source>, targetHas: [sever] } }` | `DamageDebuffed(via, statuses)` |
| `{ damage: { via: weapon, atLeast: 3 } }` — hit at least N enemies with `via` in one action | `DamageMultiple(via, atLeast)` |
| `{ kill: { via: grenade, atLeast: 2 } }` — kill at least N enemies with `via` in one action | `KillMultiple(via, atLeast)` |
| `{ pickUp: ionic-trace }` | `PickUp(pickup)` |
| `{ buffGained: bolt-charge }` | `BuffGained(status)` |
| `{ stacksMaxed: bolt-charge }` | `StacksMaxed(status)` |

An **airborne** class ability use is an air move that spends the class ability charge (Ascension's,
played as `class:air`, [loop-format.md](loop-format.md#action-tokens)). It is still a class ability
cast, so the plain `abilityCast: classAbility` rules fire on it too (the Compendium, Arc#48: the air
move sets off the equipped class ability's effects); the airborne trigger never fires on a ground use
(ADRs D26). The map form without `airborne` (or with `airborne: false`) is the plain trigger.
`airborne: true` on another ability is an error (`abilityCast: only a classAbility can be airborne (there is no airborne grenade action)`).

`via` defaults to `any`. `targetHas` lists one or more debuffs, and the target must have all of them.
`tier` and `targetHas` together is an error (`kill cannot combine 'tier' and 'targetHas'`), and so is
`atLeast` with either of them; `damage` has no `tier`. v1's pack is always `minor` (ADRs D16), so a
`major|boss|champion` kill shows in `explain` and the graph but never fires in a run.
`atLeast` is a whole number 1..20 (`TargetCount`, ADRs D22): the player says how many enemies an
action hits ("One For All: hitting three separate targets…" → `{ damage: { via: weapon, atLeast: 3 } }`).
A kill hits too, so a kill action also fires `damage … atLeast`. Only player actions (abilities and
weapons) fire `atLeast` triggers, once per action — keyword strikes and summons hit one enemy each.

**Damage sources (`via`, `against`)**:

| YAML | Domain `DamageSource` |
|---|---|
| `any` | `AnySource` |
| `weapon` | `AnyWeapon` |
| `weapon:strand` | `WeaponOfType(Strand)` |
| `ability` | `AnyAbility` |
| `grenade` / `melee` / `classAbility` / `super` | `AbilityOf(kind)` |
| `type:arc` | `OfType(Arc)` — any weapon, ability, keyword or summon of that type |
| `keyword:bolt-charge` | `KeywordOf(status)` — keyword damage (lightning strike, jolt chain…); any glossary status |
| `summon:threadling` | `SummonOf(summon)` |

Types are `kinetic|arc|solar|void|stasis|strand`. A weapon deals its build `type`; an ability the
subclass's (kinetic on Prismatic: per-ability types aren't modelled); a keyword strike its status's
affinity; a summon its glossary `damageType`. The class ability deals no damage — it is only cast.

**Conditions (`when`)**: `{ has: <status> }` · `{ lacks: <status> }` · `{ targetHas: <status> }` — all
must hold, checked against the state when the event is processed.

**Outcomes (`then`)** — exactly one key each:

| YAML | Domain `Outcome` |
|---|---|
| `{ grantEnergy: { to: grenade, amount: "15%" } }` / `amount: full` | `GrantEnergy(to, Fraction(v) \| Full)` |
| `{ convertStacksToEnergy: { consumed: armor-charge, to: grenade, perStack: "?" } }` | `ConvertStacksToEnergy` |
| `{ applyBuff: { status: bolt-charge, stacks: 1, duration: 10s } }` / `{ applyBuff: amplified }` | `ApplyBuff` (stacks default 1) |
| `{ applyBuff: { status: slice, stacks: 1, duration: 8s, restart: true } }` (`restart` default false) | `ApplyBuff` with `Restarts` |
| `{ removeBuff: new-tricks }` | `RemoveBuff` |
| `{ debuffTarget: { status: jolt, duration: 4s } }` / `{ debuffTarget: jolt }` | `DebuffTarget` |
| `{ spawn: { pickup: orb-of-power, count: 1 } }` / `{ spawn: orb-of-power }` | `Spawn` (count default 1) |
| `{ summon: { summon: threadling, count: 1 } }` / `{ summon: threadling }` | `SpawnSummon` (count default 1) |
| `{ strikeTarget: { via: bolt-charge, hit: kill } }` (`damage\|kill`; `via` names a status) | `StrikeTarget` |
| `{ modifyDamage: { against: keyword:bolt-charge, change: "+50%" } }` | `ModifyDamage` |
| `{ restoreHealth: { amount: "?", allies: true } }` (`allies` default false) | `RestoreHealth` |
| `{ resetCooldown: melee }` | `ResetCooldown` |

Statuses in `applyBuff`/`removeBuff`/`convertStacksToEnergy.consumed`/`has`/`lacks`/`buffGained`/`stacksMaxed`/`extraStacks`
must be glossary `buff`s; in `debuffTarget`/`targetHas` glossary `debuff`s
(`'jolt' is a debuff, but this position needs a buff`); `strikeTarget.via` and `keyword:` take either.
`spawn`/`pickUp` name glossary pickups, `summon`/`summon:` glossary summons.

**`restart: true`** (`applyBuff`) replaces the active stacks instead of adding to them — for a buff that
each trigger arms afresh, so repeating the trigger doesn't build it up: every class ability use arms
Slice for the next 5 hits anew (Compendium Weapon Perks#198), so a second dodge sets Slice back to ×1
(ADRs D27). The stacks given (plus any `extraStacks`) are the new count, and the duration restarts as
usual. `explain` and the trace read `Slice ×1 (8s, restarts)` (an adding grant reads `+1 Slice (8s)`);
when the buff was already active and the restart doesn't raise its stacks, the caveat reads
`restarted (was ×2)`.

Only `applyBuff`, `removeBuff`, `debuffTarget`, `spawn`, `summon`, `strikeTarget` and the stacks
`convertStacksToEnergy` consumes change a run. The energy outcomes (`grantEnergy`, the energy of
`convertStacksToEnergy`, `resetCooldown`), `modifyDamage` and `restoreHealth` are **explanations**: shown
with their value and certainty, changing nothing — there is no energy model (ADRs D21), nor a damage
or health one.

### Rules that don't stack

**`doesNotStackWith`** — optional, a list of element ids. When the game says
two elements' grants don't stack, say it on the rule of the side that gives nothing (ADRs D23):

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
* With both equipped, the build check warns (in every build summary: `explain`, `validate`, the web…) —
  `'Tempest Strike': +1 Bolt Charge on "Kill Jolted target" doesn't stack with 'Dielectric' — with both equipped, it is wasted.` —
  `explain` annotates the bullet (`+1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike]`),
  and a loop's analysis counts it as **wasted** ([loop-format.md](loop-format.md)).
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
| `{ extraCharges: { ability: grenade, extra: 1 } }` | `ExtraCharges` (shown, not used — no energy model) |
| `{ modifyDamage: { against, change } }` | `ModifyDamage` |
| `{ resistDamage: "25%" }` | `ResistDamage` |
| `{ modifyWeaponStats: { archetypes: [fusion-rifle], changes: [ { stat: handling, change: "?" } ] } }` | `ModifyWeaponStats` (non-empty lists; archetypes and stats are kebab-case words) |

Only `extraStacks` changes a run: while its `when` holds, every `applyBuff` of that status gets `extra`
more stacks (up to `maxStacks`; the trace's caveat reads `+1 from Spark of Frequency`). The others are
shown — in `explain`, and, when they have a `when`, in the trace's state while it holds — and change nothing.

## Build file (`builds/<build>/build.yaml`)

```yaml
name: Skip Grenade Hunter
author: Plunderthabooty                      # optional
source: https://www.youtube.com/watch?v=zvd6sNS463E   # optional
catalog: authored-0123456789ab               # optional pin (FR-8)
class: hunter
subclass: arc                                # arc|solar|void|stasis|strand|prismatic
super: arc-staff
grenade: skip-grenade
melee: combination-blow
classAbility: gamblers-dodge
aspects: [tempest-strike, flow-state]
fragments: [spark-of-resistance, spark-of-frequency, spark-of-shock, spark-of-discharge]
exoticArmor: shinobus-vow                    # optional
armorSetBonuses: [luminopotent-2pc, luminopotent-4pc]
armorMods: [elemental-charge, elemental-charge, grenade-kickstart, grenade-kickstart, bomber]   # repeat = stacked copies
artifactPerks: [defibrillating-blast, flashover]
weapons:
  - { slot: kinetic, name: Festival Flight, type: strand, archetype: grenade-launcher, perks: [slice] }
stats: { weapons: 47, class: 104, grenade: 145, super: 27, melee: 79 }   # any subset; 0..200
```

* Required: `name`, `class` (`hunter|titan|warlock`), `subclass`, `super`, `grenade`, `melee`, `classAbility`.
* Optional: `author`, `source` (an absolute http(s) URL), `catalog`, `exoticArmor`, and the lists
  `aspects`, `fragments`, `armorSetBonuses`, `armorMods`, `artifactPerks`, `weapons` (default empty).
* A weapon: `slot` (`kinetic|energy|power`), `name` and `type` (`kinetic|arc|solar|void|stasis|strand`)
  are required; `archetype` (a kebab-case word) and `perks` (element ids) are optional. The name
  labels its actions ("Festival Flight (hit 5)").
* `stats`: any of `weapons`, `health`, `class`, `grenade`, `super`, `melee`, whole numbers 0..200.
* Stats, archetypes and hashes are shown in the build summary; the engine doesn't use them.

### Build check

`BuildValidation` checks the build against the catalog. Blocking issues stop it
(`Build '<name>' is invalid:` and one `✗` line each); warnings (`!`) and info (`i`) show in its summary.

| Issue | Severity |
|---|---|
| `Unknown <slot> '<id>' — no such element in the rule catalog.` | Blocking |
| `'<name>' (<Kind>) can't go in the <slot> slot.` — each slot takes its own kind; a weapon's `perks` take `weaponPerk` or `exoticWeapon`; a `keyword` element goes in no slot | Blocking |
| `'<name>' belongs to another class, not <Class>.` — the element's `class` isn't the build's | Blocking |
| `'<name>' (<Affinity>) does not fit the <Subclass> subclass.` — a super, grenade, melee, aspect or fragment of another affinity; neutral and kinetic ones fit any, and Prismatic takes any | Blocking |
| `<n> aspects equipped; at most 2.` · `'<name>' is equipped <n> times; aspects and fragments are unique.` · `<n> fragments equipped; the aspects grant <m> slots.` | Blocking |
| `Fragment slots unknown for <aspects> — fragment count not checked.` | Info |
| `'<name>' has no authored rules yet — it is inert in the trace.` | Warning |
| a rule that doesn't stack with an equipped element ([above](#rules-that-dont-stack)) | Warning |
| `Build pinned catalog <pinned>; simulating with <current>.` | Info |

## Engine semantics (what an author can rely on)

> **Being reworked (ADRs D28 — not a simulator).** The engine below still counts stacks up to
> `maxStacks` (and raises `StacksMaxed` by itself), runs a step clock that `wait` advances and lets
> timed statuses expire. D28 replaces that: stacks and durations become facts shown with the outcome,
> a status stays until a rule removes or consumes it, and the player declares thresholds such as
> "Bolt Charge at max". The inventory is in [backlog.md](backlog.md#not-a-simulator-adrs-d28); author
> rules for the trigger the source states (`stacksMaxed: bolt-charge`), never to make a count come out.

* **Player actions → events.** `UseClassAbility` emits only `AbilityCast(classAbility)` — airborne
  for `class:air` (an air move that spends the charge), which every `abilityCast: classAbility` rule
  matches too; only the `airborne: true` rules tell the two apart.
  `CastAbility(k, hit, N)` (grenade, melee, super) emits `AbilityCast(k)`, then N
  `Damaged(Ability(k, subclass damage type))` — one per enemy — then N `Killed(…)` if `hit = kill`,
  then one `TargetsHit(origin, N, hit)` for the `atLeast` triggers. `FireWeapon(slot, hit, N)` emits
  N `Damaged(Weapon(slot, type))`, N `Killed` if a kill, then `TargetsHit`. N (the target count, 1..20)
  is the player's: the engine can't know how many enemies a hit catches (ADRs D22).
  `CollectPickups(p)` emits one `PickedUp(p)` per pickup on the ground and clears them. `Wait(s)`
  (5 s by default) advances the clock and expires timed buffs/debuffs — nothing recharges.
* **Nothing is gated by energy** (ADRs D21): abilities can always be used. An action is blocked
  only when it can't happen at all — nothing of that pickup on the ground, no weapon in that slot.
* Each event **fully cascades** before the next one of the same action is emitted, so a kill
  sees the debuffs its own hit applied (grenade hit → Spark of Shock jolts → the kill counts as
  "kill jolted target"), and the second enemy's hit sees what the first one's applied.
* **Kill/damage target statuses** are a snapshot of the target's debuffs when the event is emitted.
  v1 target model (ADRs D16): "the pack in front of you", always `minor` — debuffs stay on the pack after
  a kill until they expire.
* **Statuses.** `applyBuff` adds `stacks` (plus any `extraStacks`) up to `maxStacks`, or only refreshes
  a status without `maxStacks`; with `restart: true` those stacks replace the active ones instead.
  `debuffTarget` refreshes a debuff already on the pack. The duration is the outcome's, else the
  glossary's; re-applying restarts it.
* **Derived events:** `applyBuff` → `BuffGained(status, newStacks)` when the buff wasn't active or gained
  stacks (not on a plain refresh, nor on a restart that keeps or lowers the stacks); reaching `maxStacks`
  from below → `StacksMaxed(status)`. `spawn` of a `collectsAutomatically` pickup → one `PickedUp` per
  pickup immediately, otherwise they land on the ground. `summon` → one `Damaged(Summoned(…))` per
  summon. `strikeTarget` → `Damaged(Keyword(…))` (+ `Killed` if `hit: kill`). `debuffTarget`, `removeBuff` and the explanation outcomes derive no event.
* **Guards** (`when`) are checked against the state as it is when the event is processed, before any
  outcome of that event applies.
* **Phase order** of the outcomes fired by one event: Debuff → Empower (`applyBuff`,
  `removeBuff`, `modifyDamage`) → Damage (`strikeTarget`, `summon`) → Spawn → Refund
  (energy, health, cooldowns) → cascade the derived events (depth + 1). Within a phase, outcomes keep
  the equip order (the build's slots, then keyword elements) and each rule's own order.
* **Termination:** cascade depth ≤ 5, and a rule never re-fires on an identical event up its own causal
  chain (A → B → A stops). Sibling occurrences — two orbs picked up, two traces spawned, the three
  enemies of `grenade:kill:3` — each fire.
* **Stacked mods:** an element equipped N times fires its rules once; `PerModCount` picks the
  N-th value (last value if N is larger).
* **Rules that don't stack** (`doesNotStackWith`, ADRs D23): of the rules one event fires, a rule
  that lists another firing rule's element gives way — it is reported with no outcomes and
  `NotStackedWith` = that element's name, and its outcomes take no part in the phase order.
* **Explanations, not totals** (ADRs D21): energy outcomes, `modifyDamage` and `restoreHealth` are applied
  with their certainty and a caveat (`×3 stacks` for the stacks a conversion consumed — the stacks
  *are* consumed; `amount unknown` / `value unknown`) and change no state: no gauge, no refund total.
  An `Unknown` value stays `?` (never 0) and counts in a loop report's unknown values. Chunk energy
  scalars, base cooldowns and `extraCharges` are parsed but not used.
* `chance: true` rules still fire (deterministic v1) and are marked *chance* in the trace — on every
  enemy of a multi-target action, so orb and trace counts are an upper bound.
