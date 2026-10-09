# note.txt → engine events → expected fired sources (golden test)

Each line of `note.txt` is listed with:

- the engine event it describes;
- a golden scenario: the start state and one player action;
- the element ids whose rules should fire, at the root event or through the cascade.

The scenarios assume the build in `build.yaml` and the rules as of the Compendium 2026-10-09 pass
(see `discrepancies.md`; catalog `authored-061e68a8ce4e`). The expected sets follow the spec's v1
semantics over the authored rules: phase order, depth ≤ 5, a rule never re-firing on an
identical event up its own causal chain, the target's debuffs snapshotted when each event is
emitted, `UseClassAbility` emitting only `AbilityCast(classAbility)`, non-stacking statuses
that only refresh, and a rule with `doesNotStackWith` giving way when a listed element fires on the
same event. Every scenario below was checked against the engine's real output: the rules each event
fires are listed in the order the trace prints them (phase order; a rule that gives way comes last),
and each **Fired** set in the order the elements first fire. `SkipGrenadeHunterGoldenTests.cs` has one
test per scenario: it asserts elements that must fire (part of the Fired set) and some that must not,
and, for a few scenarios, more: 8b's whole fired set, the active passives in 5a and 6, Bolt Charge ×2 in 6,
Armor Charge used up in 7b, New Tricks and Amplified at the end of 10.

Conventions:

- **fired** = the element's rule matched and its guards held.
- **gives way** = the rule fired but gave nothing: it doesn't stack with another element's rule on the
  same event (`doesNotStackWith`). It still counts as fired; the trace shows "doesn't stack with …".
- *(chance)* = the rule is `chance: true`. It still fires in v1, but the trace marks it as chance.
- **passive** = an always-on modifier that was applied, not a rule firing.
- Keyword elements (`bolt-charge`, `jolt`, `ionic-trace`, `orb-of-power`, `unraveling-rounds`)
  are active in every build. Every Bolt Charge gain also fires `bolt-charge` (+2.5% melee energy,
  discrepancies row 27); the scenarios below list it where the gain happens.
- "Fresh" = no buffs, target pack has no debuffs, nothing on the ground.
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
| 7 | Orb of power | `PickedUp(orb-of-power)` | orb-of-power, unraveling-orbs | Grenade Kickstart doesn't fire on pickup; 1 charge, not 2 |
| 8 | Weapon kill | `Killed(Weapon arc)` | spark-of-discharge | Strand kills; the truncated "+ when strand weapon with severe:" |
| 9 | Ionic trace | `PickedUp(ionic-trace)` | ionic-trace, spark-of-discharge, elemental-charge, shinobus-vow, bolt-charge | — |
| 10 | Max bolt charge | `StacksMaxed(bolt-charge)` | bolt-charge, flashover, shinobus-vow, defibrillating-blast | — |

---

## Line 0: `# Skipp nade build`

This is the title and has no trigger.

## Line 1: "Class ability -> refills melee [gamblers doge] + 5s sever on strand weapon [Weapon perk slice] + next weapon kill produces orb of power [reaper] + reduced greaned cooldown [bomber]"

- **Event:** `AbilityCast(ClassAbility)`. Action: `UseClassAbility`, which emits only `AbilityCast`.
- **Scenario 1:** fresh start, then `UseClassAbility`.
- **Fired (depth 0):**
  - `reaper`: Reaper buff, 10 s
  - `slice`: Slice buff, 8 s, 1 stack
  - `gamblers-dodge`: melee energy `?`
  - `bomber`: grenade energy 12% (1 copy)
- **Nothing cascades.** `BuffGained(slice)` and `BuffGained(reaper)` match no rule.
- **End state:** buffs `reaper` (10 s), `slice: 1` (8 s).
- **Reproduced:** "reduced greaned cooldown [bomber]" is Bomber's 12% grenade energy (one copy; 17% / 20% with two or three).
- **Reproduced differently:**
  - **"5s sever on strand weapon":** the dodge only arms Slice, using the 8 s window (Compendium and Clarity). The sever happens on the next Strand weapon hit (line 2) and lasts 10 s (Compendium; 5 s is the PvP figure). The note's 5 s isn't encoded (row 10).
  - **"next weapon kill produces orb":** the dodge arms Reaper. The Orb drops on that kill (line 2).
  - **"refills melee":** the amount is `?` because the refund scales with the Melee stat (row 11).

## Line 2: "Kill with strand weapon -> sever [slice] + orb of power [attrition orbs, strand siphon] + orb of power [reaper] + unravels targets AND woven mail [to schreds] + spawns threadling if severed [horde shuttle]"

- **Event:** `Damaged(Weapon kinetic strand)`, then `Killed(Weapon kinetic strand)`. The kill sees target has [sever].
- **Scenario 2a: the first kill right after line 1.**
  - Start: the end state of scenario 1 (buffs `reaper`, `slice: 1`); fresh target.
  - Action: `FireWeapon(Kinetic, kill)`.
  - On the `Damaged` event (target snapshot `[]`):
    - `slice` → Sever, Slice stack 2
    - `attrition-orbs` *(chance)* → Orb
  - On the `Killed` event (target `[sever]`):
    - `reaper` → Orb, Reaper removed
    - `to-shreds` → Woven Mail (10 s) and a heal `?` for you and allies
    - `strand-siphon` *(chance)* → Orb
  - Fired: **slice, attrition-orbs, reaper, to-shreds, strand-siphon**.
  - End state: buffs `slice: 2`, Woven Mail; target `[sever]`; 3 Orbs on the ground.
  - Must NOT fire: `horde-shuttle`, and `to-shreds`' unravel rule. The pack wasn't severed when the hit landed.
- **Scenario 2b: a later shot, once the pack is severed and unraveled.**
  - Start: buffs `slice: 1`, `reaper: 1`; target `[sever, unravel]`.
  - Action: `FireWeapon(Kinetic, kill)`.
  - On the `Damaged` event:
    - `to-shreds` *(chance)* → Unravel
    - `slice` → Sever, Slice stack 2
    - `horde-shuttle` *(chance)* → Threadling
    - `attrition-orbs` *(chance)* → Orb
  - At depth 1, on `Damaged(Summoned threadling)`: `horde-shuttle` → Sever. (`to-shreds` doesn't
    fire here: the Compendium counts weapon hits only.)
  - On the `Killed` event: `reaper` → Orb, `to-shreds` → Woven Mail and a heal `?`, `strand-siphon` *(chance)* → Orb.
  - Fired: **to-shreds, slice, horde-shuttle, attrition-orbs, reaper, strand-siphon**.
  - End state: buffs `slice: 2`, Woven Mail; target `[sever, unravel]`; 3 Orbs on the ground.
- **Reproduced:** "sever [slice]", "orb of power [attrition orbs, strand siphon]" (both *chance*: Attrition
  Orbs counts hits and Strand Siphon needs 2 kills, rows 8 and 9), "orb of power [reaper]" and "woven mail [to schreds]".
- **Reproduced differently:**
  - **"unravels targets":** To Shreds unravels after 6 weapon hits on an *already severed* target (Compendium; marked chance, row 15), so a fresh target's killing shot only gets Woven Mail.
  - **"threadling if severed":** encoded per the Compendium, which agrees with the video: "damaging *unraveled* targets with a weapon" (row 16). It still follows from a severed pack through To Shreds.

## Line 3: "Arc grenade  -> jolt [spark of shock] + Bolt Charge [shinobu's vow] + skip grenade energy [shinobu's vow]"

- **Event:** `Damaged(Ability grenade arc)`. Action: `CastAbility(Grenade, damage)`.
- **Scenario 3:** fresh start.
  - On `AbilityCast(Grenade)`: nothing fires. There's no Armor Charge and no New Tricks.
  - On `Damaged` (depth 0):
    - `spark-of-shock` → Jolt
    - `shinobus-vow` → +1 Bolt Charge and 4.2% grenade energy
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`; `bolt-charge` → +2.5% melee energy.
  - Must NOT fire: `jolt`. The target wasn't jolted when the hit was emitted.
  - Fired: **spark-of-shock, shinobus-vow, bolt-charge**.
  - End state: buffs `bolt-charge: 1`; target `[jolt]`.
- **Fully reproduced.** "skip grenade energy" comes from both the grenade hit (4.2%) and the Bolt Charge gain (`?`).
  v1 emits one grenade hit per enemy, so per grenade Shinobu's 4.2% and Bolt Charge are undercounted (each
  seeker hits up to three times, row 31).

## Line 4: "Slide + melee -> jolt [tempest strike] + reduced grenade cooldown [impact induction]"

- **Event:** `Damaged(Ability melee arc)`. Action: `CastAbility(Melee, damage)`.
- **Scenario 4:** fresh start.
  - On `Damaged` (depth 0):
    - `tempest-strike` → Jolt
    - `impact-induction` → grenade energy 12% (row 22)
  - Fired: **tempest-strike, impact-induction**.
  - End state: no buffs; target `[jolt]`.
- **Reproduced differently:**
  - **"Slide":** not modelled; any melee hit counts.
  - **Combination Blow:** fires only on a melee kill (+1 stack, +100% class ability energy, a heal `?`; row 28). With `CastAbility(Melee, kill)` it also fires, plus the line-5 cascade, because the target is jolted by the time of the kill: `flow-state`, `dielectric`, `luminopotent-4pc`, `tempest-strike` (gives way), then the Ionic Trace and Bolt Charge cascade.

## Line 5: "Kill jolted target -> amplified [flow state, tempest strike] + orb of power [harmonic siphon] + orb of power [dielectric] + blind if severed [photonic flare]"

- **Event:** `Killed(…)` where target has [jolt]. Photonic Flare needs [sever] and Arc damage. Harmonic Siphon needs an Arc weapon kill.
- **Scenario 5a: everything on the line.**
  - Start: target `[jolt, sever]`.
  - Action: `FireWeapon(Energy, kill)` with the Arc weapon.
  - On `Damaged(Weapon energy arc)`:
    - `to-shreds` *(chance)* → Unravel
    - `jolt` → chain strike (its `Damaged(Keyword jolt)` at depth 1 fires nothing: To Shreds counts weapon hits only)
  - On `Killed(Weapon energy arc)` (target `[jolt, sever, unravel]`):
    - `photonic-flare` → Blind (an Arc kill of a severed target)
    - `flow-state` → Amplified
    - `dielectric` → +1 Bolt Charge, +1 more from Spark of Frequency because Flow State's Amplified lands first (no orb: the Tablet of Ruin version doesn't make one)
    - `to-shreds` → Woven Mail (10 s) and a heal `?`
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `luminopotent-4pc` → Ionic Trace
    - `harmonic-siphon` *(chance)* → Orb
    - `tempest-strike` gives way to `dielectric`: no Bolt Charge (they don't stack, row 26)
  - Cascade:
    - depth 1, on `BuffGained(bolt-charge, 2)` (Dielectric's): `shinobus-vow`, `bolt-charge`;
    - then, for each of the 2 traces: depth 1, `PickedUp(ionic-trace)` → `spark-of-discharge`,
      `elemental-charge` *(chance)*, `ionic-trace`; depth 2, `BuffGained(bolt-charge)` → `shinobus-vow`, `bolt-charge`.
  - Passive: `spark-of-frequency`, active once Flow State has made you Amplified (every gain is +2).
  - Fired: **to-shreds, jolt, photonic-flare, flow-state, dielectric, spark-of-discharge, luminopotent-4pc, harmonic-siphon, tempest-strike, shinobus-vow, bolt-charge, elemental-charge, ionic-trace**, plus the passive spark-of-frequency.
  - End state: buffs Amplified, `bolt-charge: 6`, Woven Mail, `armor-charge: 2`; target `[jolt, sever, unravel, blind]`; 1 Orb on the ground.
- **Scenario 5b: minimal, the spec's own example.**
  - Start: fresh.
  - Action: `CastAbility(Grenade, kill)`. Spark of Shock jolts on the hit, so the kill sees `[jolt]`.
  - On `Damaged(Ability grenade arc)`: `spark-of-shock`, `shinobus-vow`, then `shinobus-vow` and `bolt-charge` on the Bolt Charge gain (as in line 3).
  - On `Killed(Ability grenade arc)`: **flow-state, dielectric, luminopotent-4pc, tempest-strike**
    (`tempest-strike` gives way to `dielectric`).
  - Cascade: `shinobus-vow` and `bolt-charge` on Dielectric's gain; the trace's `spark-of-discharge`, `elemental-charge`, `ionic-trace`; `shinobus-vow` and `bolt-charge` again on that gain; and the passive `spark-of-frequency`.
  - End state: buffs `bolt-charge: 5`, Amplified, `armor-charge: 1`; target `[jolt]`.
  - Must NOT fire: `harmonic-siphon` (not a weapon kill), `photonic-flare` (no sever).
- **Not reproduced:**
  - "amplified [tempest strike]". The Compendium and Clarity say Tempest Strike gives Bolt Charge on jolted kills; only Flow State gives Amplified (row 1).
  - "orb of power [dielectric]". The Tablet of Ruin's Dielectric makes no orbs (row 14).
  - "blind if severed [photonic flare]" is reproduced only for Arc kills: the Compendium's trigger is an Arc kill of a severed target, jolted or not (row 17).
- **Harmonic Siphon:** fires on Arc weapon kills whether or not the target is jolted (row 6).
- **Tempest Strike's Bolt Charge** doesn't stack with Dielectric's (Compendium, row 26): its rule says
  `doesNotStackWith: [dielectric]`, so it gives way, and the build check flags it as wasted.

## Line 6: "Amplified -> increased bolt charge [spark of frequency] + Linear, Fusion Rifles and Heat weapons increased handling, reload and vent [Luminopotent Mask 2 piece bonus] + Kills wirh jolt or jolted enemies produce ionic trace [Luminopotent Mask 4 piece bonus]"

- **Event:** none. This is a guard, `While has amplified`. It describes state, not an event.
- **Scenario 6:**
  - Start: buffs `amplified`.
  - Action: `CastAbility(Grenade, damage)`.
  - Fired: `spark-of-shock`; `shinobus-vow` (Bolt Charge gain, then at depth 1 the `BuffGained(bolt-charge, 2)` energy rule); `bolt-charge` (+2.5% melee energy, once for the 2-stack gain).
  - **Passive applied:** `spark-of-frequency` (row 19). Bolt Charge goes 0 → **2**, not 1.
  - **Passive active:** `luminopotent-2pc`, Ionic Overclock (`ModifyWeaponStats` while Amplified: handling and reload `?`; no vent, see row 18). No rule fires for it.
  - End state: buffs Amplified, `bolt-charge: 2`; target `[jolt]`.
- **Reproduced differently:** the 4-piece is not conditional on Amplified. It fires on any jolted kill (line 5; row 2).

## Line 7: "Orb of power -> 2x armor charge [grenade kickstart] + strand unraveling rounds [unraveling orbs]"

- **Event:** `PickedUp(orb-of-power)`. Action: `CollectPickups(orb-of-power)`.
- **Scenario 7a:**
  - Start: one Orb on the ground.
  - On `PickedUp` (depth 0):
    - `unraveling-orbs` → Unraveling Rounds (14 s)
    - `orb-of-power` → +1 Armor Charge (the Compendium: with any Armor Charge mod equipped), super energy `?`
  - Fired: **unraveling-orbs, orb-of-power**.
  - End state: buffs Unraveling Rounds (14 s), `armor-charge: 1`.
  - Unraveling Rounds then makes the next Strand weapon hits unravel (the `unraveling-rounds` keyword), which in turn lets Horde Shuttle fire on later hits.
- **Scenario 7b: where Grenade Kickstart actually fires.**
  - Start: buffs `armor-charge: 2`.
  - Action: `CastAbility(Grenade, damage)`.
  - On `AbilityCast(Grenade)`: `grenade-kickstart` → consumes Armor Charge for grenade energy (`?` per stack, ×2 stacks). Then `spark-of-shock`, `shinobus-vow` and `bolt-charge` fire as in line 3.
  - Fired: **grenade-kickstart, spark-of-shock, shinobus-vow, bolt-charge**.
  - End state: Armor Charge 2 → 0; `bolt-charge: 1`; target `[jolt]`.
- **Not reproduced as written:** Grenade Kickstart doesn't grant Armor Charge on pickup. Picking up an Orb gives 1 Armor Charge while any Armor Charge mod is equipped (Compendium), and Grenade Kickstart spends them on the next grenade (row 3).

## Line 8: "Weapon kill -> ionic trace [spark of discharge] + when strand weapon with severe: "

- **Event:** `Killed(Weapon arc)`. The Compendium and Clarity say Spark of Discharge counts only Arc weapon kills.
- **Scenario 8a:** fresh start, then `FireWeapon(Energy, kill)`.
  - On `Killed` (depth 0):
    - `spark-of-discharge` *(chance)* → Ionic Trace
    - `harmonic-siphon` *(chance)* → Orb
  - At depth 1, on `PickedUp(ionic-trace)`: `spark-of-discharge`, `elemental-charge` *(chance)*, `ionic-trace`.
  - At depth 2, on `BuffGained(bolt-charge)`: `shinobus-vow`, `bolt-charge`.
  - Fired: **spark-of-discharge, harmonic-siphon, elemental-charge, ionic-trace, shinobus-vow, bolt-charge**.
  - End state: buffs `bolt-charge: 1`, `armor-charge: 1`; 1 Orb on the ground.
- **Scenario 8b (negative):** fresh start, then `FireWeapon(Kinetic, kill)` with the Strand weapon.
  - Fired: **attrition-orbs** *(chance)* on the hit, **strand-siphon** *(chance)* on the kill, and nothing else.
  - `spark-of-discharge` must NOT fire.
- **Not reproduced:**
  - **Strand weapon kills making traces:** the Compendium and Clarity require Arc weapon kills (row 7).
  - **"+ when strand weapon with severe:"** is cut off in the note, with nothing after the colon, so nothing is encoded for it.

## Line 9: "Ionic trace -> bolt charge [spark of discharge] + armor charge [elemental charge] + skip grenade energy [shinobu's vow]"

- **Event:** `PickedUp(ionic-trace)`. Traces collect themselves, so the test either puts one on the ground and runs `CollectPickups(ionic-trace)`, or spawns one.
- **Scenario 9:**
  - Start: one Ionic Trace on the ground.
  - On `PickedUp` (depth 0):
    - `spark-of-discharge` → +1 Bolt Charge
    - `elemental-charge` *(chance)* → +1 Armor Charge (a progress meter in game: every 2nd trace with 2 copies, row 20)
    - `ionic-trace` → 11.25% grenade and melee energy, 13.5% class ability energy (the trace prints 11.25% rounded to "11.3%", row 4)
  - At depth 1, on `BuffGained(bolt-charge)`: `shinobus-vow` → grenade energy `?`; `bolt-charge` → +2.5% melee energy.
  - Fired: **spark-of-discharge, elemental-charge, ionic-trace, shinobus-vow, bolt-charge**.
  - End state: buffs `bolt-charge: 1`, `armor-charge: 1`.
- **Fully reproduced.** Shinobu's comes through the Bolt Charge gain.

## Line 10: "Max bolt charge -> amplified [flashover] + jolts AND heal [defibrilating blast] + heal allies [shinobu's vow] + enhanced next skip grenade [shinobu's vow]"

- **Event:** `StacksMaxed(bolt-charge)` (10 stacks, row 5).
- **Scenario 10:**
  - Start: buffs `bolt-charge: 9`; one Ionic Trace on the ground.
  - Action: `CollectPickups(ionic-trace)`.
- **Depth 0, on `PickedUp`:** `spark-of-discharge` (→ 10 stacks), `elemental-charge` *(chance)*, `ionic-trace`.
- **Depth 1:**
  - On `BuffGained(bolt-charge, 10)`: `shinobus-vow`, `bolt-charge` (+2.5% melee energy).
  - On `StacksMaxed(bolt-charge)`:
    - `shinobus-vow` → New Tricks, `~40%` grenade energy, heal `?` for you and allies (row 21)
    - `flashover` → Amplified (15 s) (row 12)
    - `bolt-charge` → stacks removed, strike with `hit: kill`
- **Depth 2:**
  - On `Damaged(Keyword bolt-charge)`: `defibrillating-blast` → Jolt + heal `?` (~55 HP per the Compendium).
  - On `Killed(Keyword bolt-charge)` (target `[jolt]`, applied by Defibrillating Blast): `flow-state` (Amplified, refreshed), `dielectric`, `luminopotent-4pc`, `tempest-strike` (gives way to `dielectric`).
- **Depth 3:**
  - On `BuffGained(bolt-charge, 2)`: `shinobus-vow`, `bolt-charge`. The passive `spark-of-frequency` applies because Flashover made you Amplified.
  - The new Ionic Trace's `PickedUp(ionic-trace)` is identical to the root event, so `spark-of-discharge`, `elemental-charge` and `ionic-trace` must NOT fire on it again (no re-firing up the causal chain). They already fired at depth 0.
- **Fired:** **spark-of-discharge, elemental-charge, ionic-trace, shinobus-vow, bolt-charge, flashover, defibrillating-blast, flow-state, dielectric, luminopotent-4pc, tempest-strike**, plus the passive spark-of-frequency.
- **End state:** buffs `armor-charge: 1`, `new-tricks`, Amplified, Bolt Charge rebuilt to 2 (Dielectric's stack, doubled by Spark of Frequency); target `[jolt]`. The loop closes.
- **Reproduced.** The jolt and heal happen on the Bolt Charge strike's damage, one step after the max-stacks event, through the cascade (row 13). In game the discharge waits for your next ability hit at x10 (row 23). The next throw consumes New Tricks: `CastAbility(Grenade)` while `new-tricks` → `shinobus-vow` removes it.

## What the build does that the note doesn't mention

These elements and rules are in the build and fire (or apply) in the engine, but no line of the note
covers them:

- **`arc-staff`:** a Super kill → +1 Bolt Charge (`super:kill`; row 25). No scenario, scenario step or example loop uses the Super.
- **`skip-grenade`:** no rules of its own, so the build check warns that it is inert; its interactions come from Shinobu's Vow and the fragments.
- **`unraveling-rounds` (keyword):** while Unraveling Rounds is up (line 7), Strand weapon hits unravel the target. In the creator's loop it fires from cycle 2 on, on the Festival Flight shot.
- **`jolt` (keyword):** a weapon or ability hit on a jolted target chains lightning (`strikeTarget` via Jolt, damage only). Seen in scenario 5a.
- **`woven-mail` (keyword):** casting the Super while Woven Mail is up removes it (row 33). No scenario or example loop casts the Super.
- **Passives:** `spark-of-resistance` (25% damage resistance, always on), `woven-mail` (45% damage resistance while Woven Mail is up, i.e. after every To Shreds kill), `shinobus-vow` (+1 grenade charge, shown but not used: no energy model), `flashover` (+50% Bolt Charge damage), `combination-blow` (melee damage `?` while it has stacks, row 28).
- **The build check** also notes that the aspects' fragment slots are unknown, so the fragment count isn't checked.
