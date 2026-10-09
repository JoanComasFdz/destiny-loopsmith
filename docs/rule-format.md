# Rule & build file format (v1)

Causality is authored, not scraped: every build element is a YAML entry with **rules**
(`on` trigger → `then` outcomes) and **passives** (always-on modifiers). The YAML is a
boundary format — `RuleParsing` / `BuildParsing` turn it into the Domain types in
`src/Loopsmith.Core/Domain/` and nothing else ever sees the DTOs.

All ids are kebab-case slugs (`shinobus-vow`, `bolt-charge`). Keys are camelCase.
Unknown keys, unknown ids and unknown statuses are **parse errors** — all errors of a file
set are reported together, each with `file:line`.

## Files

```
rules/
  glossary.yaml          statuses (buffs/debuffs), pickups, summons — the keyword vocabulary
  keywords/*.yaml        kind: keyword elements — rules active in every build (Bolt Charge, Ionic Trace…)
  hunter/arc.yaml        class + subclass elements (abilities, aspects, fragments)
  exotics/armor.yaml
  armor-sets/*.yaml
  mods/armor.yaml
  artifact/<season>.yaml
  weapons/perks.yaml
builds/<build>/build.yaml
```

Every `rules/**/*.yaml` except `glossary.yaml` has the shape `elements: [ … ]`.
The catalog version is `authored-<first 12 hex chars of SHA-256 over the sorted (path, text) pairs>`.

## Glossary (`rules/glossary.yaml`)

```yaml
statuses:
  - { id: bolt-charge, name: Bolt Charge, kind: buff,   affinity: arc, maxStacks: 10 }
  - { id: jolt,        name: Jolt,        kind: debuff, affinity: arc, duration: 4s }
pickups:
  - { id: ionic-trace,  name: Ionic Trace,  affinity: arc,     collectsAutomatically: true }
  - { id: orb-of-power, name: Orb of Power, affinity: neutral, collectsAutomatically: false }
summons:
  - { id: threadling, name: Threadling, damageType: strand }
```

`kind`: `buff` (on the player) · `debuff` (on the target). `maxStacks`, `duration` optional.
A status **without `maxStacks` does not stack**: applying it again only refreshes it.
`affinity`: `neutral|kinetic|arc|solar|void|stasis|strand|prismatic`.

## Elements

```yaml
elements:
  - id: spark-of-shock
    name: Spark of Shock
    kind: fragment            # keyword|super|grenade|melee|classAbility|aspect|fragment|exoticArmor|
                              # exoticWeapon|armorSetBonus|armorMod|artifactPerk|weaponPerk
    affinity: arc
    class: hunter             # optional: hunter|titan|warlock
    hash: 1727069364          # optional manifest hash (from Clarity until the manifest join exists)
    fragmentSlots: 2          # optional, aspects only
    ability: { kind: grenade, charges: 1, chunkScalar: "?", baseCooldown: "?" }   # optional, abilities only (kept, not used by the engine — ADRs D21)
    description: "Your Arc grenades jolt targets."
    source: clarity/1727069364@2.0625      # see Provenance; omitted → authored at file:line
    rules: [ … ]
    passives: [ … ]
```

### Provenance (`source`)

| Form | Domain |
|---|---|
| `compendium/2026-10-09/Arc#54` | `Provenance.Compendium(snapshot, tab, row)` |
| `clarity/1727069364@2.0625` | `Provenance.Clarity(hash, version)` |
| `{ creator: <url>, quote: "<what they said>" }` | `Provenance.CreatorClaim(url, quote)` |
| omitted | `Provenance.Authored(file, line)` |

### Numbers (`GameValue`)

| Text | Domain | Note |
|---|---|---|
| `"15%"` | `Known(0.15)` | percent → fraction |
| `"+50%"` | `Known(0.5)` | sign optional |
| `"12% \| 17% \| 20%"` | `PerModCount([0.12, 0.17, 0.20])` | value by number of copies equipped |
| `"~25%"`, `"25%?"` | `Approximate(0.25)` | |
| `"?"`, `"?%"` | `Unknown()` | **never 0** |
| `"300% [20%]"` | PvE part `Known(3.0)` | `[…]` is the PvP value |
| `2`, `"2"` | `Known(2)` | plain number |

Durations: `5s`, `0.5s`; `"?s"` or omitted → no duration (`Optional.None`).

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

**Triggers (`on`)** — exactly one key:

| YAML | Domain `Trigger` |
|---|---|
| `{ abilityCast: grenade }` (`grenade\|melee\|classAbility\|super`) | `AbilityCast(kind)` |
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

`tier` and `targetHas` together is an error (not representable), and so is `atLeast` with either of
them. `atLeast` is a whole number 1..20 (`TargetCount`, ADRs D22): the player says how many enemies an
action hits ("One For All: hitting three separate targets…" → `{ damage: { via: weapon, atLeast: 3 } }`).
A kill hits too, so a kill action also fires `damage … atLeast`. `via` defaults to `any`.

**Damage sources (`via`, `against`)**:

| YAML | Domain `DamageSource` |
|---|---|
| `any` | `AnySource` |
| `weapon` | `AnyWeapon` |
| `weapon:strand` | `WeaponOfType(Strand)` |
| `ability` | `AnyAbility` |
| `grenade` / `melee` / `classAbility` / `super` | `AbilityOf(kind)` |
| `type:arc` | `OfType(Arc)` — any weapon, ability, keyword or summon of that type |
| `keyword:bolt-charge` | `KeywordOf(status)` — keyword damage (lightning strike, jolt chain…) |
| `summon:threadling` | `SummonOf(summon)` |

**Conditions (`when`)**: `{ has: <status> }` · `{ lacks: <status> }` · `{ targetHas: <status> }`.

**Outcomes (`then`)** — exactly one key each. The energy outcomes (`grantEnergy`, `convertStacksToEnergy`,
`resetCooldown`) are **explanations**: ability energy isn't simulated (ADRs D21), so they show in the trace
with their certainty and caveats, but change no state and aren't added up anywhere.

| YAML | Domain `Outcome` |
|---|---|
| `{ grantEnergy: { to: grenade, amount: "15%" } }` / `amount: full` | `GrantEnergy(to, Fraction(v) \| Full)` |
| `{ convertStacksToEnergy: { consumed: armor-charge, to: grenade, perStack: "?" } }` | `ConvertStacksToEnergy` |
| `{ applyBuff: { status: bolt-charge, stacks: 1, duration: 10s } }` / `{ applyBuff: amplified }` | `ApplyBuff` (stacks default 1) |
| `{ removeBuff: new-tricks }` | `RemoveBuff` |
| `{ debuffTarget: { status: jolt, duration: 4s } }` / `{ debuffTarget: jolt }` | `DebuffTarget` |
| `{ spawn: { pickup: orb-of-power, count: 1 } }` / `{ spawn: orb-of-power }` | `Spawn` (count default 1) |
| `{ summon: { summon: threadling, count: 1 } }` / `{ summon: threadling }` | `SpawnSummon` |
| `{ strikeTarget: { via: bolt-charge, hit: kill } }` (`damage\|kill`) | `StrikeTarget` |
| `{ modifyDamage: { against: keyword:bolt-charge, change: "+50%" } }` | `ModifyDamage` |
| `{ restoreHealth: { amount: "?", allies: true } }` (`allies` default false) | `RestoreHealth` |
| `{ resetCooldown: melee }` | `ResetCooldown` |

Statuses in `applyBuff`/`removeBuff`/`has`/`lacks` must be glossary `buff`s; in
`debuffTarget`/`targetHas` glossary `debuff`s; `spawn`/`pickUp` glossary pickups; `summon` glossary summons.

**Rules that don't stack (`doesNotStackWith`)** — optional, a list of element ids. When the game says
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
* With both equipped, the build check warns (`explain`, `validate`, the web) —
  `'Tempest Strike': +1 Bolt Charge on "Kill Jolted target" doesn't stack with 'Dielectric' — with both equipped, it is wasted.` —
  `explain` annotates the bullet (`+1 Bolt Charge (doesn't stack with Dielectric) [Tempest Strike]`),
  and a loop's analysis counts it as **wasted** ([loop-format.md](loop-format.md)).
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

| YAML `effect` | Domain `Passive` |
|---|---|
| `{ extraStacks: { status, extra } }` | `ExtraStacks` |
| `{ extraCharges: { ability: grenade, extra: 1 } }` | `ExtraCharges` (shown, not used — no energy model) |
| `{ modifyDamage: { against, change } }` | `ModifyDamage` |
| `{ resistDamage: "25%" }` | `ResistDamage` |
| `{ modifyWeaponStats: { archetypes: [fusion-rifle], changes: [ { stat: handling, change: "?" } ] } }` | `ModifyWeaponStats` |

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

## Engine semantics (what an author can rely on)

* **Player actions → events.** `UseClassAbility` emits only `AbilityCast(classAbility)`.
  `CastAbility(k, hit, N)` (grenade, melee, super) emits `AbilityCast(k)`, then N
  `Damaged(Ability(k, subclass damage type))` — one per enemy — then N `Killed(…)` if `hit = kill`,
  then one `TargetsHit(origin, N, hit)` for the `atLeast` triggers. `FireWeapon(slot, hit, N)` emits
  N `Damaged(Weapon(slot, type))`, N `Killed` if a kill, then `TargetsHit`. N (the target count, 1..20)
  is the player's: the engine can't know how many enemies a hit catches (ADRs D22).
  `CollectPickups(p)` emits one `PickedUp(p)` per pickup on the ground. `Wait(s)` advances
  the clock and expires timed buffs/debuffs — nothing recharges.
* **Nothing is gated by energy** (ADRs D21): abilities can always be used. An action is blocked
  only when it can't happen at all — nothing of that pickup on the ground, no weapon in that slot.
* Each event **fully cascades** before the next one of the same action is emitted, so a kill
  sees the debuffs its own hit applied (grenade hit → Spark of Shock jolts → the kill counts as
  "kill jolted target"), and the second enemy's hit sees what the first one's applied.
* **Kill/damage target statuses** are a snapshot of the target's debuffs when the event is emitted.
  v1 target model: "the pack in front of you" — debuffs stay on the pack after a kill until they expire.
* **Derived events:** `applyBuff` → `BuffGained(status, newStacks)`; reaching `maxStacks` →
  `StacksMaxed(status)`. `spawn` of a `collectsAutomatically` pickup → `PickedUp` immediately,
  otherwise it lands on the ground. `summon` → `Damaged(Summoned(…))`. `strikeTarget` →
  `Damaged(Keyword(…))` (+ `Killed` if `hit: kill`). `debuffTarget` only changes the target.
* **Guards** (`when`) are checked against the state as it is when the event is processed.
* **Phase order** of the outcomes fired by one event: Debuff → Empower (`applyBuff`,
  `removeBuff`, `modifyDamage`) → Damage (`strikeTarget`, `summon`) → Spawn → Refund
  (energy, health, cooldowns) → cascade the derived events (depth + 1).
* **Termination:** cascade depth ≤ 5, and a rule never re-fires on an identical event up its own causal
  chain (A → B → A stops). Sibling occurrences — two orbs picked up, two traces spawned, the three
  enemies of `grenade:kill:3` — each fire.
* **Stacked mods:** an element equipped N times fires its rules once; `PerModCount` picks the
  N-th value (last value if N is larger).
* **Rules that don't stack** (`doesNotStackWith`, ADRs D23): of the rules one event fires, a rule
  that lists another firing rule's element gives way — it is reported with no outcomes and
  `NotStackedWith` = that element's name, and its outcomes take no part in the phase order.
* **Energy is explained, not simulated** (ADRs D21): `grantEnergy` / `convertStacksToEnergy` /
  `resetCooldown` are applied with their certainty and a caveat (`×3 stacks` for the stacks a
  conversion consumed — the stacks *are* consumed; `amount unknown`) and change no state: no gauge,
  no refund total. An `Unknown` amount stays `?` (never 0) and counts in a loop report's unknown
  values. Chunk energy scalars, base cooldowns and `extraCharges` are parsed but not used.
* `chance: true` rules still fire (deterministic v1) and are marked *chance* in the trace — on every
  enemy of a multi-target action, so orb and trace counts are an upper bound.
