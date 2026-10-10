# note.txt → engine events → expected fired sources (golden test)

Loopsmith describes cause and effect; it doesn't simulate the game ([ADRs D1](../../ADRs.md)). Each
line of `note.txt` is listed with:

- the engine event it describes;
- a golden scenario: the start state and one step, either a player action or a state the player
  declares (`max:bolt-charge`, ADRs D3);
- the element ids whose rules should fire, at the root event or through the cascade.

The scenarios assume the build in `build.yaml` and the rules of the Compendium 2026-10-09 pass (see
`discrepancies.md`). The expected sets follow the engine semantics over the authored rules
([docs/rule-format.md](../../docs/rule-format.md)): phase order, depth ≤ 5, a rule never re-firing on
an identical event up its own causal chain, the target's debuffs snapshotted when each event is
emitted, guards checked before the event's outcomes apply, `UseClassAbility` emitting only
`AbilityCast(classAbility)` (the ground dodge, `class`), `StacksMaxed` only from a declared `max:`
step, a non-stacking buff that is already active changing nothing, and a rule with
`doesNotStackWith` giving way when a listed element fires on the same event. The rules each event
fires are listed in the order the trace prints them (phase order; a rule that gives way comes last),
and each **Fired** set in the order the elements first fire. `SkipGrenadeHunterGoldenTests.cs` has
one test per scenario: it asserts elements that must fire (part of the Fired set) and some that must
not, and, for a few scenarios, more: 8b's whole fired set, the active passives in 5a and 6, Spark of
Frequency's caveat on the Bolt Charge grant in 6, Armor Charge consumed in 7b, Bolt Charge at max
with New Tricks and Amplified after 10a, and New Tricks consumed and Bolt Charge not at max after 10b.

Conventions:

- **fired** = the element's rule matched and its guards held.
- **gives way** = the rule fired but gave nothing: it doesn't stack with another element's rule on the
  same event (`doesNotStackWith`). It still counts as fired; the trace shows "doesn't stack with …".
- *(chance)* = the rule is `chance: true`: a chance in game, or a counter Loopsmith doesn't keep
  ("2 kills within 3 s"; ADRs D8). It fires, and the trace marks it.
- **passive** = an always-on modifier that was applied, not a rule firing.
- **State** is what is present: buffs by name, "(at max)" on a buff the player declared at max, the
  pack's debuffs, the pickups on the ground. A grant reads "+1 Bolt Charge" and a duration shows on
  its outcome ("Reaper (10s)"). A buff ends when a rule consumes or removes it, or when the player
  declares it ended (`end:`); a debuff when the player declares it.
- Keyword elements (`bolt-charge`, `jolt`, `ionic-trace`, `orb-of-power`, `unraveling-rounds`,
  `woven-mail`) are active in every build. Every Bolt Charge gain also fires `bolt-charge` (+2.5%
  melee energy, discrepancies row 27); the scenarios below list it where the gain happens.
- "Fresh" = no buffs, a pack with no debuffs, nothing on the ground.
- "Row N" = row N of the table in `discrepancies.md`.

## Summary

| # | Note line (trigger) | Engine event | Must fire | Not reproduced |
|---|---|---|---|---|
| 0 | `# Skipp nade build` | (title, no trigger) | — | — |
| 1 | Class ability | `AbilityCast(ClassAbility)` | gamblers-dodge, slice, reaper, bomber | "5s" on the sever |
| 2 | Kill with strand weapon | `Damaged(Weapon strand)` then `Killed(Weapon strand)` target has [sever] | slice, attrition-orbs, strand-siphon, reaper, to-shreds; horde-shuttle only on an unraveled pack (2b) | Unravel and Threadling need an already severed or unraveled pack |
| 3 | Arc grenade | `Damaged(Ability grenade arc)` | spark-of-shock, shinobus-vow | — |
| 4 | Slide + melee | `Damaged(Ability melee arc)` | tempest-strike, impact-induction | "slide" |
| 5 | Kill jolted target | `Killed(…)` target has [jolt] (+[sever]) | flow-state, tempest-strike (gives way), harmonic-siphon, dielectric, photonic-flare | Tempest Strike → Amplified (and its Bolt Charge gives way to Dielectric's); Dielectric's orb; Photonic Flare needs an Arc kill of a severed target, not a jolted one |
| 6 | Amplified | `While has amplified` (guard) | spark-of-frequency (passive), luminopotent-2pc (passive) | 4pc is unconditional; no vent |
| 7 | Orb of power | `PickedUp(orb-of-power)` | orb-of-power, unraveling-orbs | Grenade Kickstart doesn't fire on pickup; +1 Armor Charge, not 2x |
| 8 | Weapon kill | `Killed(Weapon arc)` | spark-of-discharge | Strand kills; the truncated "+ when strand weapon with severe:" |
| 9 | Ionic trace | `PickedUp(ionic-trace)` | ionic-trace, spark-of-discharge, elemental-charge, shinobus-vow, bolt-charge | — |
| 10 | Max bolt charge | `StacksMaxed(bolt-charge)` from a `max:bolt-charge` step (10a); then the next ability hit while Bolt Charge is at max (10b) | 10a: shinobus-vow, flashover; 10b: bolt-charge, defibrillating-blast, flow-state, dielectric, luminopotent-4pc, tempest-strike (gives way) | — |

---

## Line 0: `# Skipp nade build`

This is the title and has no trigger.

## Line 1: "Class ability -> refills melee [gamblers doge] + 5s sever on strand weapon [Weapon perk slice] + next weapon kill produces orb of power [reaper] + reduced greaned cooldown [bomber]"

- **Event:** `AbilityCast(ClassAbility)`. Action: `UseClassAbility` (`class`), which emits only `AbilityCast`.
- **Scenario 1:** fresh start, then `UseClassAbility`.
- **Fired (depth 0):**
  - `reaper`: Reaper (10s)
  - `slice`: +1 Slice (8s): the dodge arms Slice
  - `gamblers-dodge`: melee energy `?`
  - `bomber`: grenade energy 12% (1 copy)
- **Nothing cascades.** `BuffGained(slice)` and `BuffGained(reaper)` match no rule.
- **End state:** buffs Reaper, Slice.
- **Dodging again** fires the same four rules: +1 Slice (8s) again, since every class ability use arms the next 5 hits (Compendium, row 10), and Reaper with the caveat `already active`. Slice stays on you until the player declares its 5 hits done (`max:slice`, line 2).
- **Reproduced:** "reduced greaned cooldown [bomber]" is Bomber's 12% grenade energy (one copy; 17% / 20% with two or three).
- **Reproduced differently:**
  - **"5s sever on strand weapon":** the dodge only arms Slice, with the 8 s window on its outcome (Compendium and Clarity). The sever happens on the next Strand weapon hit (line 2) and is 10 s (Compendium; 5 s is the PvP figure). The note's 5 s isn't encoded (row 10).
  - **"next weapon kill produces orb":** the dodge arms Reaper. The Orb drops on that kill (line 2).
  - **"refills melee":** the amount is `?` because the refund scales with the Melee stat (row 11).

## Line 2: "Kill with strand weapon -> sever [slice] + orb of power [attrition orbs, strand siphon] + orb of power [reaper] + unravels targets AND woven mail [to schreds] + spawns threadling if severed [horde shuttle]"

- **Event:** `Damaged(Weapon kinetic strand)`, then `Killed(Weapon kinetic strand)`. The kill sees target has [sever].
- **Scenario 2a: the first kill right after line 1.**
  - Start: the end state of scenario 1 (buffs Reaper, Slice); fresh pack.
  - Action: `FireWeapon(Kinetic, kill)` (`kinetic:kill`).
  - On the `Damaged` event (target snapshot `[]`):
    - `slice` → Sever, +1 Slice (8s)
    - `attrition-orbs` *(chance)* → Orb
  - On the `Killed` event (target `[sever]`):
    - `reaper` → consumes Reaper, Orb
    - `to-shreds` → Woven Mail (10s) and a heal `?` for you and allies
    - `strand-siphon` *(chance)* → Orb
  - Fired: **slice, attrition-orbs, reaper, to-shreds, strand-siphon**.
  - End state: buffs Slice, Woven Mail (Reaper consumed); target `[sever]`; Orb of Power on the ground.
  - Must NOT fire: `horde-shuttle`, and `to-shreds`' unravel rule. The pack wasn't severed when the hit landed.
- **Scenario 2b: a later shot, once the pack is severed and unraveled.**
  - Start: buffs Slice, Reaper; target `[sever, unravel]`.
  - Action: `FireWeapon(Kinetic, kill)`.
  - On the `Damaged` event:
    - `to-shreds` *(chance)* → Unravel
    - `slice` → Sever, +1 Slice (8s)
    - `horde-shuttle` *(chance)* → Threadling
    - `attrition-orbs` *(chance)* → Orb
  - At depth 1, on `Damaged(Summoned threadling)`: `horde-shuttle` → Sever. (`to-shreds` doesn't
    fire here: the Compendium counts weapon hits only.)
  - On the `Killed` event: `reaper` → consumes Reaper, Orb; `to-shreds` → Woven Mail and a heal `?`; `strand-siphon` *(chance)* → Orb.
  - Fired: **to-shreds, slice, horde-shuttle, attrition-orbs, reaper, strand-siphon**.
  - End state: buffs Slice, Woven Mail; target `[sever, unravel]`; Orb of Power on the ground.
- **Slice's 5 hits:** Slice stays on you, and every Strand weapon hit severs, until the player
  declares the 5 hits done: `max:slice` ("Slice at max") raises `StacksMaxed(slice)` and `slice`
  ends Slice. The next dodge arms it again (row 10).
- **Reproduced:** "sever [slice]", "orb of power [attrition orbs, strand siphon]" (both *chance*: Attrition
  Orbs counts hits and Strand Siphon needs 2 kills, rows 8 and 9), "orb of power [reaper]" and "woven mail [to schreds]".
- **Reproduced differently:**
  - **"unravels targets":** To Shreds unravels after 6 weapon hits on an *already severed* target (Compendium; marked chance, row 15), so a fresh target's killing shot only gets Woven Mail.
  - **"threadling if severed":** encoded per the Compendium, which agrees with the video: "damaging *unraveled* targets with a weapon" (row 16). It still follows from a severed pack through To Shreds.

## Line 3: "Arc grenade  -> jolt [spark of shock] + Bolt Charge [shinobu's vow] + skip grenade energy [shinobu's vow]"

- **Event:** `Damaged(Ability grenade arc)`. Action: `CastAbility(Grenade, damage)` (`grenade`).
- **Scenario 3:** fresh start.
  - On `AbilityCast(Grenade)`: nothing fires. There's no Armor Charge and no New Tricks.
  - On `Damaged` (depth 0):
    - `spark-of-shock` → Jolt
    - `shinobus-vow` → +1 Bolt Charge and 4.2% grenade energy
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`; `bolt-charge` → +2.5% melee energy.
  - Must NOT fire: `jolt`. The target wasn't jolted when the hit was emitted. `bolt-charge`'s discharge doesn't fire either: Bolt Charge isn't at max.
  - Fired: **spark-of-shock, shinobus-vow, bolt-charge**.
  - End state: buffs Bolt Charge; target `[jolt]`.
- **Fully reproduced.** "skip grenade energy" comes from both the grenade hit (4.2%) and the Bolt Charge gain (`?`).
  How many times each seeker hits is a fact of the grenade (row 31); every hit fires the same rules.

## Line 4: "Slide + melee -> jolt [tempest strike] + reduced grenade cooldown [impact induction]"

- **Event:** `Damaged(Ability melee arc)`. Action: `CastAbility(Melee, damage)` (`melee`).
- **Scenario 4:** fresh start.
  - On `Damaged` (depth 0):
    - `tempest-strike` → Jolt
    - `impact-induction` → grenade energy 12% (row 22)
  - Fired: **tempest-strike, impact-induction**.
  - End state: no buffs; target `[jolt]`.
- **Reproduced differently:**
  - **"Slide":** not modelled; any melee hit counts.
  - **Combination Blow:** fires only on a melee kill (+1 Combination Blow (20s), 100% class ability energy, a heal `?`; row 28). With `CastAbility(Melee, kill)` it also fires, plus the line-5 cascade, because the target is jolted by the time of the kill: `flow-state`, `dielectric`, `luminopotent-4pc`, `tempest-strike` (gives way), then the Ionic Trace and Bolt Charge cascade.

## Line 5: "Kill jolted target -> amplified [flow state, tempest strike] + orb of power [harmonic siphon] + orb of power [dielectric] + blind if severed [photonic flare]"

- **Event:** `Killed(…)` where target has [jolt]. Photonic Flare needs [sever] and Arc damage. Harmonic Siphon needs an Arc weapon kill.
- **Scenario 5a: everything on the line.**
  - Start: target `[jolt, sever]`.
  - Action: `FireWeapon(Energy, kill)` (`energy:kill`) with the Arc weapon.
  - On `Damaged(Weapon energy arc)`:
    - `to-shreds` *(chance)* → Unravel
    - `jolt` → chain strike (its `Damaged(Keyword jolt)` at depth 1 fires nothing: To Shreds counts weapon hits only)
  - On `Killed(Weapon energy arc)` (target `[jolt, sever, unravel]`):
    - `photonic-flare` → Blind (an Arc kill of a severed target)
    - `flow-state` → Amplified
    - `dielectric` → +1 Bolt Charge, with the caveat "+1 from Spark of Frequency" because Flow State's Amplified lands first (no orb: the Tablet of Ruin version doesn't make one)
    - `to-shreds` → Woven Mail (10s) and a heal `?`
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `luminopotent-4pc` → Ionic Trace
    - `harmonic-siphon` *(chance)* → Orb
    - `tempest-strike` gives way to `dielectric`: no Bolt Charge (they don't stack, row 26)
  - Cascade:
    - depth 1, on `BuffGained(bolt-charge)` (Dielectric's): `shinobus-vow`, `bolt-charge`;
    - then, for each of the two traces: depth 1, `PickedUp(ionic-trace)` → `spark-of-discharge`,
      `elemental-charge` *(chance)*, `ionic-trace`; depth 2, `BuffGained(bolt-charge)` → `shinobus-vow`, `bolt-charge`.
  - Passive: `spark-of-frequency`, active once Flow State has made you Amplified (every later Bolt Charge grant carries "+1 from Spark of Frequency").
  - Fired: **to-shreds, jolt, photonic-flare, flow-state, dielectric, spark-of-discharge, luminopotent-4pc, harmonic-siphon, tempest-strike, shinobus-vow, bolt-charge, elemental-charge, ionic-trace**, plus the passive spark-of-frequency.
  - End state: buffs Amplified, Bolt Charge, Woven Mail, Armor Charge; target `[jolt, sever, unravel, blind]`; Orb of Power on the ground.
- **Scenario 5b: minimal.**
  - Start: fresh.
  - Action: `CastAbility(Grenade, kill)` (`grenade:kill`). Spark of Shock jolts on the hit, so the kill sees `[jolt]`.
  - On `Damaged(Ability grenade arc)`: `spark-of-shock`, `shinobus-vow`, then `shinobus-vow` and `bolt-charge` on the Bolt Charge gain (as in line 3).
  - On `Killed(Ability grenade arc)`: **flow-state, dielectric, luminopotent-4pc, tempest-strike**
    (`tempest-strike` gives way to `dielectric`).
  - Cascade: `shinobus-vow` and `bolt-charge` on Dielectric's gain; the trace's `spark-of-discharge`, `elemental-charge`, `ionic-trace`; `shinobus-vow` and `bolt-charge` again on that gain; and the passive `spark-of-frequency`.
  - End state: buffs Bolt Charge, Amplified, Armor Charge; target `[jolt]`.
  - Must NOT fire: `harmonic-siphon` (not a weapon kill), `photonic-flare` (no sever).
- **Not reproduced:**
  - "amplified [tempest strike]". The Compendium and Clarity say Tempest Strike gives Bolt Charge on jolted kills; only Flow State gives Amplified (row 1).
  - "orb of power [dielectric]". The Tablet of Ruin's Dielectric makes no orbs (row 14).
  - "blind if severed [photonic flare]" is reproduced only for Arc kills: the Compendium's trigger is an Arc kill of a severed target, jolted or not (row 17).
- **Harmonic Siphon:** fires on Arc weapon kills whether or not the target is jolted (row 6).
- **Tempest Strike's Bolt Charge** doesn't stack with Dielectric's (Compendium, row 26): its rule says
  `doesNotStackWith: [dielectric]`, so it gives way, the build check flags it as wasted, and a loop's
  analysis lists it as wasted at the step.

## Line 6: "Amplified -> increased bolt charge [spark of frequency] + Linear, Fusion Rifles and Heat weapons increased handling, reload and vent [Luminopotent Mask 2 piece bonus] + Kills wirh jolt or jolted enemies produce ionic trace [Luminopotent Mask 4 piece bonus]"

- **Event:** none. This is a guard, `While has amplified`. It describes state, not an event.
- **Scenario 6:**
  - Start: buffs Amplified.
  - Action: `CastAbility(Grenade, damage)`.
  - Fired: `spark-of-shock`; `shinobus-vow` (+1 Bolt Charge, then at depth 1 the energy rule on the `BuffGained(bolt-charge)`); `bolt-charge` (+2.5% melee energy).
  - **Passive applied:** `spark-of-frequency` (row 19). Shinobu's grant reads "+1 Bolt Charge" with the caveat "+1 from Spark of Frequency".
  - **Passive active:** `luminopotent-2pc`, Ionic Overclock (`ModifyWeaponStats` while Amplified: handling and reload `?`; no vent, see row 18). No rule fires for it.
  - End state: buffs Amplified, Bolt Charge; target `[jolt]`.
- **Reproduced differently:** the 4-piece is not conditional on Amplified. It fires on any jolted kill (line 5; row 2).

## Line 7: "Orb of power -> 2x armor charge [grenade kickstart] + strand unraveling rounds [unraveling orbs]"

- **Event:** `PickedUp(orb-of-power)`. Action: `CollectPickups(orb-of-power)` (`pickup:orb-of-power`), which picks up one Orb.
- **Scenario 7a:**
  - Start: an Orb of Power on the ground.
  - On `PickedUp` (depth 0):
    - `unraveling-orbs` → Unraveling Rounds (14s)
    - `orb-of-power` → +1 Armor Charge (the Compendium: with any Armor Charge mod equipped), super energy `?`
  - Fired: **unraveling-orbs, orb-of-power**.
  - End state: buffs Unraveling Rounds, Armor Charge; nothing on the ground.
  - While Unraveling Rounds is on you, Strand weapon hits unravel the target (the `unraveling-rounds` keyword), which in turn lets Horde Shuttle fire on the hits after.
- **Scenario 7b: where Grenade Kickstart actually fires.**
  - Start: buffs Armor Charge.
  - Action: `CastAbility(Grenade, damage)`.
  - On `AbilityCast(Grenade)`: `grenade-kickstart` → spends Armor Charge → +?% grenade energy per stack (it consumes Armor Charge). Then `spark-of-shock`, `shinobus-vow` and `bolt-charge` fire as in line 3.
  - Fired: **grenade-kickstart, spark-of-shock, shinobus-vow, bolt-charge**.
  - End state: buffs Bolt Charge; Armor Charge not present; target `[jolt]`.
- **Not reproduced as written:** Grenade Kickstart doesn't grant Armor Charge on pickup. Picking up an Orb gives +1 Armor Charge while any Armor Charge mod is equipped (Compendium), and Grenade Kickstart consumes it on the next grenade (row 3).

## Line 8: "Weapon kill -> ionic trace [spark of discharge] + when strand weapon with severe: "

- **Event:** `Killed(Weapon arc)`. The Compendium and Clarity say Spark of Discharge counts only Arc weapon kills.
- **Scenario 8a:** fresh start, then `FireWeapon(Energy, kill)`.
  - On `Killed` (depth 0):
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `harmonic-siphon` *(chance)* → Orb
  - At depth 1, on `PickedUp(ionic-trace)`: `spark-of-discharge`, `elemental-charge` *(chance)*, `ionic-trace`.
  - At depth 2, on `BuffGained(bolt-charge)`: `shinobus-vow`, `bolt-charge`.
  - Fired: **spark-of-discharge, harmonic-siphon, elemental-charge, ionic-trace, shinobus-vow, bolt-charge**.
  - End state: buffs Bolt Charge, Armor Charge; Orb of Power on the ground.
- **Scenario 8b (negative):** fresh start, then `FireWeapon(Kinetic, kill)` with the Strand weapon.
  - Fired: **attrition-orbs** *(chance)* on the hit, **strand-siphon** *(chance)* on the kill, and nothing else.
  - `spark-of-discharge` must NOT fire.
- **Not reproduced:**
  - **Strand weapon kills making traces:** the Compendium and Clarity require Arc weapon kills (row 7).
  - **"+ when strand weapon with severe:"** is cut off in the note, with nothing after the colon, so nothing is encoded for it.

## Line 9: "Ionic trace -> bolt charge [spark of discharge] + armor charge [elemental charge] + skip grenade energy [shinobu's vow]"

- **Event:** `PickedUp(ionic-trace)`. Traces collect themselves, so the test either puts one on the ground and runs `CollectPickups(ionic-trace)`, or spawns one.
- **Scenario 9:**
  - Start: an Ionic Trace on the ground.
  - On `PickedUp` (depth 0):
    - `spark-of-discharge` → +1 Bolt Charge
    - `elemental-charge` *(chance)* → +1 Armor Charge (a progress meter in game: every 2nd trace with 2 copies, row 20)
    - `ionic-trace` → 11.25% grenade and melee energy, 13.5% class ability energy (the trace prints 11.25% rounded to "11.3%", row 4)
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`; `bolt-charge` → +2.5% melee energy.
  - Fired: **spark-of-discharge, elemental-charge, ionic-trace, shinobus-vow, bolt-charge**.
  - End state: buffs Bolt Charge, Armor Charge.
- **Fully reproduced.** Shinobu's comes through the Bolt Charge gain.

## Line 10: "Max bolt charge -> amplified [flashover] + jolts AND heal [defibrilating blast] + heal allies [shinobu's vow] + enhanced next skip grenade [shinobu's vow]"

- **Event:** `StacksMaxed(bolt-charge)`, raised by a declared step: the player says Bolt Charge is at
  its max, x10 (`max:bolt-charge`, "Bolt Charge at max"; row 5, ADRs D3). The discharge comes on the
  next ability hit while Bolt Charge is at max (row 23).
- **Scenario 10a: the declaration.**
  - Start: buffs Bolt Charge; fresh pack.
  - Action: `max:bolt-charge`.
  - On `StacksMaxed(bolt-charge)` (depth 0):
    - `shinobus-vow` → New Tricks, `~40%` grenade energy, heal `?` for you and allies (row 21)
    - `flashover` → Amplified (15s) (row 12)
  - **Nothing cascades.** `BuffGained(new-tricks)` and `BuffGained(amplified)` match no rule, and the discharge comes on the next ability hit (10b).
  - Passives: `spark-of-frequency` and `luminopotent-2pc`, active once Flashover has made you Amplified.
  - Fired: **shinobus-vow, flashover**, plus the passives.
  - End state: buffs Bolt Charge (at max), New Tricks, Amplified.
  - Must NOT fire: `bolt-charge`, `defibrillating-blast`.
  - Without Bolt Charge the step is blocked: `Bolt Charge isn't active — nothing to declare at max.`
- **Scenario 10b: the next skip grenade.**
  - Start: the end state of 10a (buffs Bolt Charge (at max), New Tricks, Amplified); fresh pack.
  - Action: `CastAbility(Grenade, kill)` (`grenade:kill`).
  - On `AbilityCast(Grenade)`: `shinobus-vow` → consumes New Tricks (the enhanced grenade is thrown).
  - On `Damaged(Ability grenade arc)` (target `[]`; Bolt Charge at max):
    - `spark-of-shock` → Jolt
    - `shinobus-vow` → +1 Bolt Charge (caveat "+1 from Spark of Frequency") and 4.2% grenade energy
    - `bolt-charge` → consumes Bolt Charge, Bolt Charge strike (kills)
  - Depth 1:
    - On `BuffGained(bolt-charge)` (Shinobu's): `shinobus-vow` → grenade energy `?`; `bolt-charge` → +2.5% melee energy.
    - On `Damaged(Keyword bolt-charge)`: `defibrillating-blast` → Jolt + heal `?` (~55 HP per the Compendium, row 13).
    - On `Killed(Keyword bolt-charge)` (target `[jolt]`): `flow-state` (Amplified, `already active`), `dielectric` (+1 Bolt Charge, "+1 from Spark of Frequency"), `luminopotent-4pc` (Ionic Trace), `tempest-strike` (gives way to `dielectric`).
  - Depth 2: `shinobus-vow` and `bolt-charge` on Dielectric's gain; the trace's `PickedUp(ionic-trace)` → `spark-of-discharge`, `elemental-charge` *(chance)*, `ionic-trace`. Depth 3: `shinobus-vow` and `bolt-charge` on that gain.
  - On `Killed(Ability grenade arc)` (target `[jolt]`): `flow-state`, `dielectric`, `luminopotent-4pc`, `tempest-strike` (gives way), with the same cascade (as in 5b).
  - The discharge consumes Bolt Charge with its declaration, so it fires once: the Bolt Charge that Dielectric and the traces give after the strike is not at max.
  - Fired: **shinobus-vow, spark-of-shock, bolt-charge, defibrillating-blast, flow-state, dielectric, luminopotent-4pc, tempest-strike, spark-of-discharge, elemental-charge, ionic-trace**, plus the passives spark-of-frequency and luminopotent-2pc.
  - End state: buffs Amplified, Bolt Charge (not at max), Armor Charge; New Tricks consumed; target `[jolt]`.
  - Must NOT fire: `harmonic-siphon` (not a weapon kill), `photonic-flare` (no sever).
- **Reproduced.** "amplified [flashover]", "heal allies [shinobu's vow]" and "enhanced next skip
  grenade [shinobu's vow]" (New Tricks, consumed by the next throw) fire on the declaration; "jolts AND
  heal [defibrilating blast]" fires on the discharge, which comes on the next ability hit at x10, as in
  game (Compendium Arc#5, row 23). The strike's kill gives Bolt Charge again (Dielectric, Shock and
  Clear's trace), so the next pass can declare it at max again: the loop closes.

## The scenario and the example loops

`scenario.txt` is one engagement as the video plays it; the `Scenario_trace` golden test snapshots its
trace (`dotnet run --project src/Loopsmith.Cli -- trace builds/skip-grenade-hunter/build.yaml --scenario builds/skip-grenade-hunter/scenario.txt --state`).
Its steps, with the note lines they play:

1. `class`: line 1 (Reaper and Slice armed, Bomber).
2. `grenade:kill`: lines 3 and 5b (the pack jolted, Bolt Charge, Amplified, an Ionic Trace).
3. `kinetic:kill`: line 2a on a jolted pack, so the kill is also line 5's jolted kill.
4. `pickup:orb-of-power`: line 7a (Armor Charge, Unraveling Rounds).
5. `melee:kill`: line 4, with Combination Blow on the kill.
6. `max:bolt-charge`: line 10a (Shinobu's Vow; Flashover's Amplified is already active, from #2).
7. `grenade:kill`: lines 7b and 10b (New Tricks and Armor Charge consumed; the hit discharges Bolt Charge).
8. `energy:kill`: line 8a (Spark of Discharge, Harmonic Siphon).

The example loops declare Bolt Charge at max the same way, right before a skip grenade, so the throw
spends New Tricks and its hit discharges Bolt Charge:

- **Infinite skip grenades** (the creator's loop): `class`, `grenade:kill`, `kinetic:kill`, `energy:kill`, `pickup:orb-of-power`, `max:bolt-charge`, `grenade:kill`.
- **Melee first**: `melee:kill`, `max:bolt-charge`, `grenade:kill`, `energy:kill`, `pickup:orb-of-power`, `class`.
- **Helicopter skip grenades** ([the Ascension variant](../skip-grenade-hunter-ascension/discrepancies.md)): Infinite skip grenades with `class:air` at #1.

## What the build does that the note doesn't mention

These elements and rules are in the build and fire (or apply) in the engine, but no line of the note
covers them:

- **`arc-staff`:** a Super kill → +1 Bolt Charge (`super:kill`; row 25). No scenario, scenario step or example loop uses the Super.
- **`skip-grenade`:** no rules of its own, so the build check warns that it is inert; its interactions come from Shinobu's Vow and the fragments.
- **`unraveling-rounds` (keyword):** while Unraveling Rounds is on you (line 7), Strand weapon hits unravel the target. In the creator's loop it fires on the Festival Flight shot (#3) of the repeating pass: the Orb picked up at #5 of the pass before gave it.
- **`jolt` (keyword):** a weapon or ability hit on a jolted target chains lightning (`strikeTarget` via Jolt, damage only). Seen in scenario 5a.
- **`woven-mail` (keyword):** casting the Super while Woven Mail is on you removes it (row 33). No scenario or example loop casts the Super.
- **Passives:** `spark-of-resistance` (25% damage resistance, always on), `woven-mail` (45% damage resistance while Woven Mail is on you, from the first To Shreds kill), `shinobus-vow` (+1 grenade charge, a fact shown, ADRs D5), `flashover` (+50% Bolt Charge damage), `combination-blow` (melee damage `?` while Combination Blow is on you, row 28).
- **The build check** also notes that the aspects' fragment slots are unknown, so the fragment count isn't checked.
