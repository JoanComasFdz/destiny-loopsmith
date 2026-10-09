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
    ability: { kind: grenade, charges: 1, chunkScalar: "?", baseCooldown: "?" }   # optional, abilities only
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
| `{ pickUp: ionic-trace }` | `PickUp(pickup)` |
| `{ buffGained: bolt-charge }` | `BuffGained(status)` |
| `{ stacksMaxed: bolt-charge }` | `StacksMaxed(status)` |

`tier` and `targetHas` together is an error (not representable). `via` defaults to `any`.

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

**Outcomes (`then`)** — exactly one key each:

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
| `{ extraCharges: { ability: grenade, extra: 1 } }` | `ExtraCharges` |
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

* **Player actions → events.** `CastAbility(k, hit)` emits `AbilityCast(k)`, then
  `Damaged(Ability(k, subclass damage type))`, then `Killed(…)` if `hit = kill`.
  `FireWeapon(slot, hit)` emits `Damaged(Weapon(slot, type))` then `Killed` if a kill.
  `CollectPickups(p)` emits one `PickedUp(p)` per pickup on the ground. `Wait(s)` advances
  the clock and expires timed buffs/debuffs.
* Each event **fully cascades** before the next one of the same action is emitted, so a kill
  sees the debuffs its own hit applied (grenade hit → Spark of Shock jolts → the kill counts as
  "kill jolted target").
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
* **Termination:** cascade depth ≤ 5; each (rule, event) pair fires at most once per step.
* **Stacked mods:** an element equipped N times fires its rules once; `PerModCount` picks the
  N-th value (last value if N is larger).
* **Energy:** `Fraction(v)` adds `v × chunk scalar` charges (unknown scalar → 1× *assumed*, flagged);
  an `Unknown` amount is shown as `?` and not applied. `full` tops the gauge up to its max charges.
* `chance: true` rules still fire (deterministic v1) and are marked *chance* in the trace.
