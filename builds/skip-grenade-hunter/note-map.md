# note.txt → engine events → expected fired sources (golden test)

Each line of `note.txt` is listed with:

- the engine event it describes;
- a golden scenario: the start state and one player action;
- the element ids whose rules should fire, at the root event or through the cascade.

The scenarios assume the build in `build.yaml`. The expected sets were derived by running the
spec's v1 semantics over the authored rules: phase order, depth ≤ 5, a rule never re-firing on an
identical event up its own causal chain, the target's debuffs snapshotted when each event is
emitted, `UseClassAbility` emitting only `AbilityCast(classAbility)`, and non-stacking statuses
that only refresh.

Conventions:

- **fired** = the element's rule matched and its guards held.
- *(chance)* = the rule is `chance: true`. It still fires in v1, but the trace marks it as chance.
- **passive** = an always-on modifier that was applied, not a rule firing.
- Keyword elements (`bolt-charge`, `jolt`, `ionic-trace`, `orb-of-power`, `unraveling-rounds`)
  are active in every build.
- "Fresh" = no buffs, target pack has no debuffs, nothing on the ground.

## Summary

| # | Note line (trigger) | Engine event | Must fire | Not reproduced |
|---|---|---|---|---|
| 0 | `# Skipp nade build` | (title, no trigger) | — | — |
| 1 | Class ability | `AbilityCast(ClassAbility)` | gamblers-dodge, slice, reaper, bomber | "5s" on the sever |
| 2 | Kill with strand weapon | `Damaged(Weapon strand)` then `Killed(Weapon strand)` target has [sever] | slice, attrition-orbs, strand-siphon, reaper, to-shreds, horde-shuttle | Unravel and Threadling need an already severed or unraveled pack |
| 3 | Arc grenade | `Damaged(Ability grenade arc)` | spark-of-shock, shinobus-vow | — |
| 4 | Slide + melee | `Damaged(Ability melee arc)` | tempest-strike, impact-induction | "slide" |
| 5 | Kill jolted target | `Killed(…)` target has [jolt] (+[sever]) | flow-state, tempest-strike, harmonic-siphon, dielectric, photonic-flare | Tempest Strike → Amplified |
| 6 | Amplified | `While has amplified` (guard) | spark-of-frequency (passive), luminopotent-2pc (passive) | 4pc is unconditional |
| 7 | Orb of power | `PickedUp(orb-of-power)` | orb-of-power, unraveling-orbs | Grenade Kickstart doesn't fire on pickup; 1 charge, not 2 |
| 8 | Weapon kill | `Killed(Weapon arc)` | spark-of-discharge | Strand kills; the truncated "+ when strand weapon with severe:" |
| 9 | Ionic trace | `PickedUp(ionic-trace)` | ionic-trace, spark-of-discharge, elemental-charge, shinobus-vow | — |
| 10 | Max bolt charge | `StacksMaxed(bolt-charge)` | bolt-charge, flashover, shinobus-vow, defibrillating-blast | — |

---

## Line 0: `# Skipp nade build`

This is the title and has no trigger.

## Line 1: "Class ability -> refills melee [gamblers doge] + 5s sever on strand weapon [Weapon perk slice] + next weapon kill produces orb of power [reaper] + reduced greaned cooldown [bomber]"

- **Event:** `AbilityCast(ClassAbility)`. Action: `UseClassAbility`, which emits only `AbilityCast`.
- **Scenario 1:** fresh start, then `UseClassAbility`.
- **Fired (depth 0):**
  - `gamblers-dodge`: melee energy `?`
  - `bomber`: grenade energy 12% (1 copy)
  - `reaper`: Reaper buff, 10 s
  - `slice`: Slice buff, 8 s, 1 stack
- **Nothing cascades.** `BuffGained(slice)` and `BuffGained(reaper)` match no rule.
- **End state:** buffs `slice: 1`, `reaper: 1`.
- **Reproduced differently:**
  - **"5s sever on strand weapon":** the dodge only arms Slice, using Clarity's 8 s window. The sever happens on the next Strand weapon hit (line 2). The 5 s isn't encoded (see discrepancies row 10).
  - **"next weapon kill produces orb":** the dodge arms Reaper. The Orb drops on that kill (line 2).
  - **"refills melee":** the amount is `?` because the refund scales with the Melee stat (discrepancies row 11).

## Line 2: "Kill with strand weapon -> sever [slice] + orb of power [attrition orbs, strand siphon] + orb of power [reaper] + unravels targets AND woven mail [to schreds] + spawns threadling if severed [horde shuttle]"

- **Event:** `Damaged(Weapon kinetic strand)`, then `Killed(Weapon kinetic strand)`. The kill sees target has [sever].
- **Scenario 2a: the first kill right after line 1.**
  - Start: buffs `slice: 1`, `reaper: 1`; fresh target.
  - Action: `FireWeapon(Kinetic, kill)`.
  - On the `Damaged` event (target snapshot `[]`):
    - `slice` → Sever, Slice stack 2
    - `attrition-orbs` *(chance)* → Orb
  - On the `Killed` event (target `[sever]`):
    - `reaper` → Orb, Reaper removed
    - `strand-siphon` *(chance)* → Orb
    - `to-shreds` → Woven Mail
  - Fired: **slice, attrition-orbs, reaper, strand-siphon, to-shreds**.
  - End state: 3 Orbs on the ground, Woven Mail, target `[sever]`.
  - Must NOT fire: `horde-shuttle`, and `to-shreds`' unravel rule. The pack wasn't severed when the hit landed.
- **Scenario 2b: a later shot, once the pack is severed and unraveled.**
  - Start: buffs `slice: 1`, `reaper: 1`; target `[sever, unravel]`.
  - Action: `FireWeapon(Kinetic, kill)`.
  - On the `Damaged` event:
    - `to-shreds` → Unravel
    - `horde-shuttle` *(chance)* → Threadling
    - `slice`
    - `attrition-orbs` *(chance)*
  - At depth 1, on `Damaged(Summoned threadling)`:
    - `horde-shuttle` → Sever
    - `to-shreds`
  - On the `Killed` event: `reaper`, `strand-siphon` *(chance)*, `to-shreds` → Woven Mail.
  - Fired: **to-shreds, horde-shuttle, slice, attrition-orbs, reaper, strand-siphon**.
- **Reproduced differently:**
  - **"unravels targets":** To Shreds unravels on damage to an *already severed* target, so a fresh target's killing shot only gets Woven Mail.
  - **"threadling if severed":** encoded per the video as "damaging *unraveled* targets with a weapon" (discrepancies row 16). It still follows from a severed pack through To Shreds.

## Line 3: "Arc grenade  -> jolt [spark of shock] + Bolt Charge [shinobu's vow] + skip grenade energy [shinobu's vow]"

- **Event:** `Damaged(Ability grenade arc)`. Action: `CastAbility(Grenade, damage)`.
- **Scenario 3:** fresh start.
  - On `AbilityCast(Grenade)`: nothing fires. There's no Armor Charge and no New Tricks.
  - On `Damaged` (depth 0):
    - `spark-of-shock` → Jolt
    - `shinobus-vow` → +1 Bolt Charge and 4.2% grenade energy
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`.
  - Must NOT fire: `jolt`. The target wasn't jolted when the hit was emitted.
  - Fired: **spark-of-shock, shinobus-vow**.
- **Fully reproduced.** "skip grenade energy" comes from both the drone hit (4.2%) and the Bolt Charge gain (`?`).

## Line 4: "Slide + melee -> jolt [tempest strike] + reduced grenade cooldown [impact induction]"

- **Event:** `Damaged(Ability melee arc)`. Action: `CastAbility(Melee, damage)`.
- **Scenario 4:** fresh start.
  - On `Damaged` (depth 0):
    - `tempest-strike` → Jolt
    - `impact-induction` → grenade energy 12%
  - Fired: **tempest-strike, impact-induction**.
- **Reproduced differently:**
  - **"Slide":** not modelled; any melee hit counts.
  - **Combination Blow:** fires only on a melee kill. With `CastAbility(Melee, kill)` it also fires, plus the line-5 cascade, because the target is jolted by the time of the kill.

## Line 5: "Kill jolted target -> amplified [flow state, tempest strike] + orb of power [harmonic siphon] + orb of power [dielectric] + blind if severed [photonic flare]"

- **Event:** `Killed(…)` where target has [jolt]. Photonic Flare also needs [sever]. Harmonic Siphon needs an Arc weapon kill.
- **Scenario 5a: everything on the line.**
  - Start: target `[jolt, sever]`.
  - Action: `FireWeapon(Energy, kill)` with the Arc weapon.
  - On `Damaged(Weapon energy arc)`:
    - `jolt` → chain strike, which leads at depth 1 to `Damaged(Keyword jolt)` → `to-shreds`
    - `to-shreds` → Unravel
  - On `Killed(Weapon energy arc)` (target `[jolt, sever, unravel]`):
    - `flow-state` → Amplified
    - `tempest-strike` → +1 Bolt Charge
    - `harmonic-siphon` *(chance)* → Orb
    - `dielectric` → +Bolt Charge
    - `dielectric` *(chance)* → Orb + heal
    - `photonic-flare` → Blind
    - `luminopotent-4pc` → Ionic Trace
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `to-shreds` → Woven Mail
  - Cascade (depth 1–2), repeated for each of the 2 traces:
    - `PickedUp(ionic-trace)` → `ionic-trace`, `spark-of-discharge`, `elemental-charge` *(chance)*
    - `BuffGained(bolt-charge)` → `shinobus-vow`
  - Passive: `spark-of-frequency`, active once Flow State has made you Amplified.
  - Fired: **flow-state, tempest-strike, harmonic-siphon, dielectric, photonic-flare, luminopotent-4pc, spark-of-discharge, to-shreds, jolt, ionic-trace, elemental-charge, shinobus-vow**, plus the passive spark-of-frequency.
- **Scenario 5b: minimal, the spec's own example.**
  - Start: fresh.
  - Action: `CastAbility(Grenade, kill)`. Spark of Shock jolts on the hit, so the kill sees `[jolt]`.
  - On `Killed(Ability grenade arc)`: **flow-state, tempest-strike, luminopotent-4pc, dielectric** (both rules).
  - Cascade: `ionic-trace`, `spark-of-discharge`, `elemental-charge`, `shinobus-vow`, and the passive `spark-of-frequency`.
  - Must NOT fire: `harmonic-siphon` (not a weapon kill), `photonic-flare` (no sever).
- **Not reproduced:** "amplified [tempest strike]". Clarity says Tempest Strike gives Bolt Charge on jolted kills; only Flow State gives Amplified (discrepancies row 1).
- **Harmonic Siphon:** fires on Arc weapon kills whether or not the target is jolted (row 6).

## Line 6: "Amplified -> increased bolt charge [spark of frequency] + Linear, Fusion Rifles and Heat weapons increased handling, reload and vent [Luminopotent Mask 2 piece bonus] + Kills wirh jolt or jolted enemies produce ionic trace [Luminopotent Mask 4 piece bonus]"

- **Event:** none. This is a guard, `While has amplified`. It describes state, not an event.
- **Scenario 6:**
  - Start: buffs `amplified`.
  - Action: `CastAbility(Grenade, damage)`.
  - Fired: `spark-of-shock`; `shinobus-vow` (Bolt Charge gain, then at depth 1 the `BuffGained(bolt-charge, 2)` energy rule).
  - **Passive applied:** `spark-of-frequency`. Bolt Charge goes 0 → **2**, not 1.
  - **Passive active:** `luminopotent-2pc` (`ModifyWeaponStats` while Amplified, all values `?`). No rule fires for it.
- **Reproduced differently:** the 4-piece is not conditional on Amplified. It fires on any jolted kill (line 5; discrepancies row 2).

## Line 7: "Orb of power -> 2x armor charge [grenade kickstart] + strand unraveling rounds [unraveling orbs]"

- **Event:** `PickedUp(orb-of-power)`. Action: `CollectPickups(orb-of-power)`.
- **Scenario 7a:**
  - Start: one Orb on the ground.
  - On `PickedUp` (depth 0):
    - `orb-of-power` → +1 Armor Charge, super energy `?`
    - `unraveling-orbs` → Unraveling Rounds
  - Fired: **orb-of-power, unraveling-orbs**.
- **Scenario 7b: where Grenade Kickstart actually fires.**
  - Start: buffs `armor-charge: 2`.
  - Action: `CastAbility(Grenade, damage)`.
  - On `AbilityCast(Grenade)`: `grenade-kickstart` → consumes Armor Charge for grenade energy (`?` per stack). Then `spark-of-shock` and `shinobus-vow` fire as in line 3.
  - Fired: **grenade-kickstart, spark-of-shock, shinobus-vow**.
- **Not reproduced as written:** Grenade Kickstart doesn't grant Armor Charge on pickup. Picking up an Orb gives 1 Armor Charge as base behaviour, and Grenade Kickstart spends them on the next grenade (discrepancies row 3).

## Line 8: "Weapon kill -> ionic trace [spark of discharge] + when strand weapon with severe: "

- **Event:** `Killed(Weapon arc)`. Clarity says Spark of Discharge counts only Arc weapon kills.
- **Scenario 8a:** fresh start, then `FireWeapon(Energy, kill)`.
  - On `Killed` (depth 0):
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `harmonic-siphon` *(chance)* → Orb
  - At depth 1, on `PickedUp(ionic-trace)`: `ionic-trace`, `spark-of-discharge`, `elemental-charge` *(chance)*.
  - At depth 2, on `BuffGained(bolt-charge)`: `shinobus-vow`.
  - Fired: **spark-of-discharge, harmonic-siphon, ionic-trace, elemental-charge, shinobus-vow**.
- **Scenario 8b (negative):** fresh start, then `FireWeapon(Kinetic, kill)` with the Strand weapon.
  - Fired: **attrition-orbs** *(chance)*, **strand-siphon** *(chance)* only.
  - `spark-of-discharge` must NOT fire.
- **Not reproduced:**
  - **Strand weapon kills making traces:** Clarity requires Arc weapon kills (row 7).
  - **"+ when strand weapon with severe:"** is cut off in the note, with nothing after the colon, so nothing is encoded for it.

## Line 9: "Ionic trace -> bolt charge [spark of discharge] + armor charge [elemental charge] + skip grenade energy [shinobu's vow]"

- **Event:** `PickedUp(ionic-trace)`. Traces collect themselves, so the test either puts one on the ground and runs `CollectPickups(ionic-trace)`, or spawns one.
- **Scenario 9:**
  - Start: one Ionic Trace on the ground.
  - On `PickedUp` (depth 0):
    - `ionic-trace` → ~15% to grenade, melee and class ability
    - `spark-of-discharge` → +1 Bolt Charge
    - `elemental-charge` *(chance)* → +1 Armor Charge
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`.
  - Fired: **ionic-trace, spark-of-discharge, elemental-charge, shinobus-vow**.
- **Fully reproduced.** Shinobu's comes through the Bolt Charge gain.

## Line 10: "Max bolt charge -> amplified [flashover] + jolts AND heal [defibrilating blast] + heal allies [shinobu's vow] + enhanced next skip grenade [shinobu's vow]"

- **Event:** `StacksMaxed(bolt-charge)`.
- **Scenario 10:**
  - Start: buffs `bolt-charge: 9`; one Ionic Trace on the ground.
  - Action: `CollectPickups(ionic-trace)`.
- **Depth 0, on `PickedUp`:** `ionic-trace`, `spark-of-discharge` (→ 10 stacks), `elemental-charge` *(chance)*.
- **Depth 1:**
  - On `BuffGained(bolt-charge, 10)`: `shinobus-vow`.
  - On `StacksMaxed(bolt-charge)`:
    - `bolt-charge` → stacks removed, strike with `hit: kill`
    - `flashover` → Amplified
    - `shinobus-vow` → New Tricks, `40%?` grenade energy, heal `?` for you and allies
- **Depth 2:**
  - On `Damaged(Keyword bolt-charge)`: `defibrillating-blast` → Jolt + heal `?`.
  - On `Killed(Keyword bolt-charge)` (target `[jolt]`, applied by Defibrillating Blast): `tempest-strike`, `flow-state`, `luminopotent-4pc`, `dielectric` (both rules).
- **Depth 3:**
  - On `BuffGained(bolt-charge)`: `shinobus-vow`. The passive `spark-of-frequency` applies because Flashover made you Amplified.
  - The new Ionic Trace's `PickedUp(ionic-trace)` is identical to the root event, so `ionic-trace`, `spark-of-discharge` and `elemental-charge` must NOT fire on it again (no re-firing up the causal chain). They already fired at depth 0.
- **Fired:** **bolt-charge, flashover, shinobus-vow, defibrillating-blast, tempest-strike, flow-state, luminopotent-4pc, dielectric, ionic-trace, spark-of-discharge, elemental-charge**, plus the passive spark-of-frequency.
- **End state:** `new-tricks`, `amplified`, Bolt Charge rebuilt to 4. The loop closes.
- **Reproduced.** The jolt and heal happen on the Bolt Charge strike's damage, one step after the max-stacks event, through the cascade (discrepancies row 13). The next throw consumes New Tricks: `CastAbility(Grenade)` while `new-tricks` → `shinobus-vow` removes it.
