# Skip Grenade Hunter: where the sources disagree

Every disagreement below is about what triggers what, or about the fact an outcome shows
([ADRs D1](../../ADRs.md)): a grant ("+1 Bolt Charge"), a duration ("Amplified (15s)"), an amount
("+12% grenade energy"). When a threshold is reached ("Bolt Charge at max") is the player's
declaration (D3).

Sources, in priority order:

1. **Game data**: the **Destiny Data Compendium** snapshot of 2026-10-09 (private, not in git; cited
   as `compendium/2026-10-09/<Tab>#<row>`, where the row is the record number in that tab's CSV) and
   **Clarity** 2.0625 (commit 039cfbc, 2026-10-08). When they disagree the Compendium wins (it is the
   newer snapshot) unless Clarity is clearly more specific; every such case is listed below.
2. The user's **note** (`note.txt`).
3. The creator's **video** (https://www.youtube.com/watch?v=zvd6sNS463E; its transcript is kept
   locally, not in git).

Clarity has no entries for grenades, melees, supers, armor set bonuses, artifact perks or statuses
(of the abilities it covers only class abilities, such as Gambler's Dodge). The Compendium covers
all of them, so every rule of the build has game data behind it except the build choices themselves
(super, energy weapon, stats).

Number rules: game-data numbers are used as given, and a value the source marks as approximate
(`~40%`, `4?`) stays approximate. A number only the video gives is approximate (`~`). Anything else
is `"?"`, which is never treated as 0. Some Compendium numbers can't be written in the rule format
(heals in HP, values that depend on a stat or on the stack level); they stay `"?"` with the number in
a comment (see "Still unknown" below).

No row goes against the priority order: where the video disagrees with the note (rows 2 and 16), it
reads the in-game perk text word for word, and the Compendium agrees with it.

| # | Topic | Note says | Video says | Game data says | Encoded | Why |
|---|---|---|---|---|---|---|
| 1 | Tempest Strike on jolted kills | "Kill jolted target -> amplified [flow state, tempest strike]" | "defeating jolt[ed] targets grants us stacks of bolt charge" | Compendium Arc#51 and Clarity 4194622037: a jolted kill grants x1 Bolt Charge (the Compendium adds that it doesn't stack with Dielectric's, row 26) | `tempest-strike`: jolted kill → +1 Bolt Charge, `doesNotStackWith: [dielectric]` (row 26). Amplified on a jolted kill comes only from `flow-state` | Game data outranks the note, and the video agrees |
| 2 | Luminopotent 4pc ("Shock and Clear") condition | Listed under "Amplified ->" | Reads the bonus text with no condition, and found no cooldown in testing | Compendium Armor Perks#58: a Jolt kill or a kill of a jolted enemy creates an Ionic Trace. No condition, no cooldown stated | Unconditional: any kill of a jolted target → Ionic Trace | Game data and the video agree. The note's "Amplified ->" heading groups effects together and doesn't read as a condition |
| 3 | Where Armor Charge comes from | "Orb of power -> 2x armor charge [grenade kickstart]" | Grenade Kickstart "consumes our armor charges and we gain grenade energy for each armor charged used" | Compendium Game Mechanics#94: any Orb of Power gives x1 Armor Charge while any Armor Charge mod is equipped. Armor Mods#8 (same as Clarity 4182064480): Grenade Kickstart spends all Armor Charge on a grenade for 16.2% … 45% by mods + stacks | `orb-of-power` keyword: pickup → +1 Armor Charge (unconditional: a rule can't check which mods are equipped; this build has two kinds). `grenade-kickstart`: grenade cast with Armor Charge → `convertStacksToEnergy`, which consumes Armor Charge, `perStack: "?"` | Game data. Grenade Kickstart spends Armor Charge; it doesn't create it. "2x" most likely means the two copies. The refund table is indexed by mods + stacks, not per stack, so no per-stack number exists |
| 4 | Ionic Trace energy | — | "15% ability energy to all three abilities" | Compendium Arc#6: 11.25% grenade, 11.25% melee and 13.5% class ability energy per pickup (Armor Perks#114 repeats these as the base). Clarity has no base value, only the total with Fallen Sunstar (1376049785) | `ionic-trace`: `"11.25%"` grenade and melee, `"13.5%"` class ability | Game data replaces the video's number. The trace prints 11.25% as "11.3%" (the engine shows one decimal) |
| 5 | Bolt Charge max stacks | "Max bolt charge" (no number) | "those 10 stacks of bolt charge" | Compendium Arc#5: 10 stacks are needed to discharge. Clarity only has New Tricks' "x10" | Glossary `maxStacks: 10`, shown as "up to x10"; it makes `max:bolt-charge` declarable | Game data confirms the video. Reaching x10 is the player's declaration (ADRs D3) |
| 6 | Harmonic Siphon trigger | "Kill jolted target -> … orb of power [harmonic siphon]" | — | Compendium Armor Mods#11 (Siphon): 2 kills within 3 s with a weapon of the mod's element (for Harmonic, your Super's), 1 s cooldown; orbs 2.5% / 3.75% / 4.4%. Clarity 3832366019: "within ? s" and 3.25% with 2 mods | Kill with an Arc weapon → Orb, `chance: true` | Game data. The rule can't say "matching your Super", so it is written for an Arc super (Arc Staff). Two kills within 3 s are a counter, hence chance. The Compendium settles Clarity's "? s"; its 3.75% (vs 3.25%) is preferred, in the description only |
| 7 | Spark of Discharge trace source | "Weapon kill -> ionic trace" | "collecting an ionic trace grants you another stack of bolt charge" (pickup side only) | Compendium Arc#13 and Clarity 1727069362: Arc weapon kills fill a counter (34% minor, 67% elite or Guardian, 100% miniboss and up) | Kill with an Arc weapon → Ionic Trace, `chance: true`. Strand (Festival Flight) kills don't count | Game data. The progress counter isn't shown as a stacking status, so it is a chance rule (ADRs D8), with the progress in its reason |
| 8 | Attrition Orbs | Listed with Strand Siphon under "Kill with strand weapon", as if it were a mod | — | Compendium Weapon Perks#22: a weapon trait. A magazine-based number of hits, each within 5 s of the last (3 for grenade launchers), makes the next hit drop an orb worth 0.87% super energy. Clarity 243981275: hits too, but 0.8% | Weapon perk on Festival Flight. Hit with a Strand weapon → Orb, `chance: true` | Game data on what it is and what triggers it; the Compendium's 0.87% is preferred (description only). Which weapon carries it is inferred: Festival Flight is the only Strand weapon named |
| 9 | Strand Siphon | "Kill with strand weapon -> orb of power" | — | Compendium Armor Mods#11: 2 kills within 3 s (Clarity 3926119246: "? s") | Kill with a Strand weapon → Orb, `chance: true` | Game data. Two kills within 3 s are a counter, so it is marked chance |
| 10 | Slice | "5s sever on strand weapon" | "the slice perk on it. So, it guarantees to sever targets" | Compendium Weapon Perks#198 (Clarity 923806249 agrees): after a class ability, the next 5 hits on non-severed enemies, each within 8 s of the last (9 s enhanced), sever them; a severing kill doesn't count. Sever lasts 10 s against combatants, 5 s in PvP (Strand#8) | Class ability → +1 Slice (8s): every dodge arms it. Strand weapon hit while Slice is on you → Sever + 1 Slice. The player declares the 5 hits done (`max:slice`) → Slice ends. Glossary: Slice up to x5; Sever 10 s | Game data. The note's 5 s matches neither the window nor the PvE sever, only the PvP sever. When the 5 hits are done depends on play, so it is the player's declaration (ADRs D3). "Not already severed" and "not on a killing hit" can't be expressed |
| 11 | Gambler's Dodge refund | "refills melee" | "79 melee just so when I dodged, I could get my melee back" | Compendium Class Abilities#5 and Clarity 426473317: 1% melee energy per Melee point (0.35% in PvP, 100% at 100 Melee), doubled within 15 m of an enemy, no chunk or stat scalar. Base cooldown 56 s, chunk scalar 0.8x | Class ability → melee energy `"?"`; ability profile `chunkScalar: 0.8`, `baseCooldown: 56` | At 79 Melee near an enemy that's 158%, a full refill. But a rule amount can't depend on a stat, and a build-specific number doesn't belong in the catalog |
| 12 | Flashover | "Max bolt charge -> amplified [flashover]" | "lightning bolts deal more damage" | Compendium Artifact Perks#43: reaching x10 Bolt Charge makes you Amplified for 15 s; discharges deal 50% more damage against combatants (675 → 1014). Clarity's Revolution frame (4185339856) also says 50% | Passive: +50% to Bolt Charge damage. Rule: Max Bolt Charge (`stacksMaxed`, the player's `max:bolt-charge`) → Amplified (15s) | Game data backs both the note and the video |
| 13 | Defibrillating Blast | "Max bolt charge -> jolts AND heal" | "stunning a champion grants max bolt charge and then damaging targets with bolt charge bolts jolts them and heals you" | Compendium Artifact Perks#45: a Champion stun grants x10 Bolt Charge; a discharge's splash jolts; each discharge restores ~55 HP and starts critical health regeneration | Bolt Charge strike damage → Jolt the target + heal `"?"`. It follows the discharge: the next ability hit while Bolt Charge is at max (row 23) | Game data and the video agree. The heal is in HP and `restoreHealth` amounts read as percentages, so it stays `"?"`. "Stun a Champion → max Bolt Charge" is left out because there's no stun trigger |
| 14 | Dielectric | "Kill jolted target -> orb of power [dielectric]" | "Defeating arc debuff targets grants more bolt charge … rapidly defeating those arc debuff targets spawns orbs of power and heals" | Compendium Artifact Perks#41: the Tablet of Ruin version grants x1 Bolt Charge on an Arc-debuffed kill and, unlike the Encrypted Data Disk's version (Artifact Perks#7), spawns no orbs and doesn't heal | Jolted kill → +1 Bolt Charge. No orb and no heal | Game data contradicts the note and the video: the orbs and healing belong to the Data Disk's Dielectric, and the creator runs Tablet of Ruin (he names it, and Flashover, Defibrillating Blast and Photonic Flare exist only on that artifact). "Arc debuff" is treated as Jolt only: Blind also counts, but `targetHas` requires all listed debuffs, and a second rule for Blind would fire twice on targets that have both |
| 15 | To Shreds | "Kill with strand weapon -> unravels targets AND woven mail" | "Dealing sustained damage to severed targets unravels them and defeating those severed targets creates that woven [mail] hot spot" | Compendium Artifact Perks#51: 6 weapon hits on a severed enemy unravel it. A severed enemy's death releases 3 healing pulses, 2 s apart, each restoring 15 HP and granting Woven Mail for 10 s to you and nearby allies | Weapon hit on a severed target → Unravel, `chance: true`. Kill of a severed target → Woven Mail (10s) + heal `"?"` for you and allies | Game data turns "sustained" into 6 weapon hits, so jolt chains and Threadling hits don't unravel; the 6 hits are a counter, hence chance. The pulses are simplified to one grant; 15 HP per pulse is a heal in HP, so `"?"`. On a fresh target the killing shot doesn't unravel, because the target isn't severed yet when the hit lands |
| 16 | Horde Shuttle trigger | "spawns threadling if severed" | "Damaging unraveled targets with a weapon occasionally spawns a threadling. And those threadlings can then go out and sever targets" | Compendium Artifact Perks#45: weapon damage worth 10% of an unraveled enemy's health and shields spawns a Threadling (0.5 s cooldown); Threadlings sever what they damage | Weapon hit on an unraveled target → Threadling (`chance: true`). Threadling damage → Sever | Game data confirms the video. The note's "severed" version still happens through the cascade (sever → To Shreds unravel → Threadling) |
| 17 | Photonic Flare | "Kill jolted target -> … blind if severed" | Not mentioned | Compendium Artifact Perks#47: killing a Severed or Exhausted enemy with Arc damage sets off a blinding burst (about 5 m, about 6 s cooldown) | Kill with Arc damage (`type:arc`) of a severed target → Blind | Game data: Jolt isn't needed, Arc damage is. Strand weapon kills don't blind; Bolt Charge strikes, Arc grenades and the Arc weapon do. "Exhausted" isn't in the glossary and nothing in this build applies it. The cooldown is a fact in the description |
| 18 | Luminopotent 2pc | "Amplified -> Linear, Fusion Rifles and Heat weapons increased handling, reload and vent" | Only the 4-piece is covered | Compendium Armor Perks#57, "Ionic Overclock": while Amplified, (linear) fusion rifles and heat weapons get +? Handling and +? Reload Speed | `luminopotent-2pc`, named Ionic Overclock. While Amplified: handling and reload `"?"`. No vent | Game data names the bonus and lists no vent stat, so the note's vent is dropped. The Compendium has no values either |
| 19 | Spark of Frequency condition | "Amplified -> increased bolt charge" | "You gain increased stacks of bolt charge from all sources" (no condition mentioned) | Compendium Arc#16 and Clarity 1727069361: while Amplified, (almost) all Bolt Charge sources grant an extra stack. Also a melee-hit reload buff | While Amplified, every Bolt Charge grant carries the caveat "+1 from Spark of Frequency" (`extraStacks`) | Game data (and the note agrees). The melee-hit reload buff isn't encoded |
| 20 | Elemental Charge | "Ionic trace -> armor charge" | "an escalating chance to give you an armor charge" | Compendium Armor Mods#4 and Clarity 3712696020: a progress counter. Ionic Traces give 34% / 50% / 50% with 1 / 2 / 3 mods (every 2nd trace with 2 copies) | Ionic Trace pickup → +1 Armor Charge, `chance: true` | Game data: the mechanic is a progress meter, not a chance. It isn't shown as a stacking status, so it is a chance rule (ADRs D8), and the reason states the real progress |
| 21 | New Tricks enhancement and healing | "heal allies … enhanced next skip grenade" | "bounces two additional times … releasing bonus skip seekers per bounce", "heals yourself and nearby allies" | Compendium Exotic Armors#78: on x10 Bolt Charge, ~40% grenade energy, ? HP to you and allies within ? m, and the next Skip Grenade bounces once more and releases 5 extra seekers on its 1st and 2nd bounce; ?% grenade energy per Bolt Charge stack gained. Clarity 3907299874: the same numbers, but 2 extra seekers on the first bounce | On Max Bolt Charge: New Tricks buff, heal `"?"` with `allies: true`, `"~40%"` grenade energy. The next grenade throw consumes New Tricks. `"?"` grenade energy per Bolt Charge gain | Game data; the description follows the Compendium. The enhancement itself has no causal effect, so it is only a buff. The three sources describe it three different ways |
| 22 | Impact Induction trigger | "Slide + melee -> reduced grenade cooldown" | — | Compendium Armor Mods#10: powered melee damage (Clarity 377010989: elemental melee damage); 12% / 17% / 20%; 7 s cooldown | Melee damage → grenade energy `"12% \| 17% \| 20%"` (1 copy → 12%) | Game data. Sliding isn't required; the cooldown is a fact in the description |
| 23 | Bolt Charge strike outcome | — | "four bolt charge lightning strikes on one single champion" (strikes don't always kill) | Compendium Arc#5: at x10, the next ability hit or unpowered melee hit discharges a bolt 0.5 s later, 675 damage (1014 with Flashover); it counts as generic Arc damage, not ability damage | While Bolt Charge is declared at max (`atMax`), the next ability hit consumes it and strikes: `strikeTarget` via Bolt Charge, `hit: kill`. The strike is keyword damage, not ability damage | Game data for when it strikes. The kill is engine design: the target is the pack in front of you, and a strike clears adds (ADRs D7). Champions are the exception the video shows |
| 24 | Class stat | — | "I had 104 class ability", but also "pair that with a 100 class ability" | — | `class: 104` | The stated stat line wins over the rounded remark |
| 25 | Super and energy weapon | Not mentioned | "Take whatever super you like … arc staff would probably be the play"; no energy weapon named | Compendium Arc#44 for Arc Staff itself: Tier 2 super (556 s), kills grant x1 Bolt Charge | `super: arc-staff` (with its super kill → +1 Bolt Charge rule); energy slot "Any Arc weapon" placeholder | Inferred. Harmonic Siphon (super element) and Spark of Discharge (Arc weapon kills) only work with an Arc super and an Arc weapon |
| 26 | Tempest Strike and Dielectric together | — | — | Compendium Arc#51: Tempest Strike's x1 Bolt Charge on a jolted kill doesn't stack with Dielectric's | `tempest-strike`'s jolted-kill rule has `doesNotStackWith: [dielectric]`: with Dielectric equipped it gives nothing, so a jolted kill gives Dielectric's +1 Bolt Charge (with "+1 from Spark of Frequency" while Amplified) | Game data. The build check warns that with both equipped Tempest Strike's Bolt Charge is wasted, the trace shows it as "doesn't stack with Dielectric", and a loop's analysis lists it as wasted at every step with a jolted kill (in the creator's loop, every kill once the grenade has jolted the pack) |
| 27 | Bolt Charge melee energy | — | — | Compendium Arc#5: every Bolt Charge stack gained grants 2.5% melee energy, even past x10 | `bolt-charge` rule: Bolt Charge gain → +2.5% melee energy | From game data: a fact shown on every Bolt Charge gain |
| 28 | Combination Blow | — | "combination blow times three stacks"; "my melee won't kill something, so it doesn't refund my dodge" | Compendium Arc#41: a melee kill grants 100% class ability energy, heals (100 HP, then 80 / 60 / 40 HP at x1 / x2 / x3) and adds a stack for 20 s, up to x3; melee damage +133% / +266% / +400% (PvP 23% / 51.3% / 86%); 70 Class is needed for a full dodge refund; base cooldown 58.1 s, chunk scalar 1x | Melee kill → +1 Combination Blow (20s), `"100%"` class ability energy, heal `"?"`. Damage passive `"?"` while Combination Blow is on you. Glossary: up to x3 | Game data confirms the video's x3 and dodge refund and gives the amount. The heal is in HP, and both it and the damage bonus vary with the stack level, which the format can't show yet, so they stay `"?"` ([backlog](../../docs/backlog.md), rule format gaps). The build's 104 Class is above the 70 needed |
| 29 | Amplified duration | — | — | Compendium Arc#4: 15 s (Flashover's Amplified is also 15 s, Artifact Perks#43) | Glossary `duration: 15s`, shown on the outcome: "Amplified (15s)" | Game data; no other source states it. Amplified ends when the player declares it (`end:amplified`) |
| 30 | Other status durations | "5s sever" (row 10) | — | Jolt 10 s (Arc#8), Sever 10 s (Strand#8), Blind 10 s against combatants (Arc#7), Woven Mail 10 s (Strand#7; also To Shreds), Unraveling Rounds 14 s from Unraveling Orbs (Artifact Perks#45), Combination Blow 20 s (Arc#41) | Glossary or rule durations as listed, shown on the outcomes ("Woven Mail (10s)") | Game data. A buff ends when a rule consumes or removes it (Reaper on its orb, Woven Mail on a Super cast) or when the player declares it ended (`end:woven-mail`); a debuff when the player declares it (`end:jolt`) |
| 31 | Skip Grenade hits per seeker | — | — | Compendium Arc#36: 4 seekers, each hitting twice on impact and then exploding; Exotic Armors#78: every impact and explosion counts as a Shinobu's Vow hit (Clarity: 2-3 hits per drone) | A grenade action hits each enemy once, and that hit fires Shinobu's Vow's +1 Bolt Charge and 4.2% | How many times a seeker hits is a fact in the description: every hit sets off the same rules, so one hit per enemy shows them all |
| 32 | Health from Orbs of Power | — | — | Compendium Game Mechanics#119: an orb restores 0.7 HP per Health point (70 HP at 100) | Not encoded | Depends on a stat, is in HP, and the build gives no Health stat |
| 33 | Woven Mail's own effects | "woven mail [to schreds]" (no effect named) | "that woven [mail] hot spot" | Compendium Strand#7: 45% [PvP 25%] damage resistance for 10 s; casting any Super removes it | `woven-mail` keyword: Super cast while you have Woven Mail → Woven Mail removed; while Woven Mail: `resistDamage: "45% [25%]"` | From game data. The removal shows in a loop that casts the Super (the example loops don't). Amplified's own effects (Compendium Arc#4: 15% damage resistance against combatants, mobility and handling, and the intrinsic Arc-kill counter) stay unencoded: the counter spans several actions (and Flow State gives Amplified on every jolted kill), mobility needs movement, handling an "all weapons" archetype, and the resistance sets off nothing (glossary comment) |

**Count: 33 rows.** Game data settles or corrects the note and the video in rows 1–23 and 28; rows
26, 27 and 29–33 come from the Compendium alone; rows 24 and 25 are build choices only the
video supports.

## Compendium 2026-10-09: what the rules encode

Every value of the build's rules against the snapshot. Rows cite `<Tab>#<row>` of
`compendium/2026-10-09`; for artifact perks the cited row is the description row (the perk's name
is in the row above).

| Element | Compendium says | Encoded | Source |
|---|---|---|---|
| `ionic-trace` | 11.25% grenade and melee, 13.5% class ability per pickup | `"11.25%"`, `"11.25%"`, `"13.5%"` | Arc#6 |
| `bolt-charge` (keyword) | each stack gained gives 2.5% melee energy; at x10 the next ability hit discharges; 675 damage | rule: Bolt Charge gain → `"2.5%"` melee; the discharge on the next ability hit while Bolt Charge is at max (`atMax`) | Arc#5 |
| `skip-grenade` | 0.75x, 151.5 s; 4 seekers | `0.75`, `151.5` | Arc#36 |
| `combination-blow` | 100% class energy on a melee kill; heals 100 HP, less with stacks; 1x, 58.1 s | `"100%"`; heal `"?"` (HP); `1`, `58.1` | Arc#41 |
| glossary `combination-blow` | up to x3, 20 s each | `maxStacks: 3`, `duration: 20s` | Arc#41 |
| `gamblers-dodge` | 0.8x, 56 s | `0.8`, `56` (refund stays `"?"`) | Class Abilities#5 |
| `arc-staff` | Tier 2 super, 556 s; kills grant x1 Bolt Charge | `baseCooldown: 556`; super kill → +1 Bolt Charge | Arc#44 |
| `shinobus-vow` | ~40% grenade energy at x10 | `"~40%"` | Exotic Armors#78 |
| `flashover` | Amplified for 15 s at x10; +50% discharge damage | Amplified `15s`; +50% | Artifact Perks#43 |
| `tempest-strike` | x1 Bolt Charge on a jolted kill, which doesn't stack with Dielectric's | `doesNotStackWith: [dielectric]`: gives nothing with Dielectric equipped, flagged as wasted (row 26) | Arc#51 |
| `dielectric` | this version only grants x1 Bolt Charge, no orbs or healing | one rule: jolted kill → +1 Bolt Charge | Artifact Perks#41 |
| `to-shreds` | 6 weapon hits → Unravel; severed death → 3 pulses of 15 HP + Woven Mail 10 s for you and allies | weapon hit on severed → Unravel (`chance`); kill → Woven Mail `10s` + heal `"?"` (allies) | Artifact Perks#51 |
| `unraveling-orbs` | 14 s | `duration: 14s` | Artifact Perks#45 |
| `photonic-flare` | Arc-damage kill of a severed or exhausted enemy → blinding burst | `kill via type:arc` of a severed target → Blind | Artifact Perks#47 |
| `luminopotent-2pc` | "Ionic Overclock": +? Handling, +? Reload Speed | named Ionic Overclock; handling, reload `"?"`; no vent | Armor Perks#57 |
| `harmonic-siphon`, `strand-siphon` | within 3 s; 3.75% with 2 mods | description only (orb values aren't encoded) | Armor Mods#11 |
| `attrition-orbs` | 0.87% (1.1% enhanced); 3 hits for grenade launchers | description only | Weapon Perks#22 |
| glossary `amplified` | 15 s | `duration: 15s` | Arc#4 |
| glossary `jolt` | 10 s (PvP 5 s) | `duration: 10s` | Arc#8 |
| glossary `sever` | 10 s (PvP 5 s) | `duration: 10s` | Strand#8 |
| glossary `blind` | 10 s against combatants | `duration: 10s` | Arc#7 |
| glossary `woven-mail` | 10 s, 45% damage resistance | `duration: 10s` | Strand#7 |
| `woven-mail` (keyword) | 45% [25%] damage resistance; removed by casting any Super | element: Super cast → Woven Mail removed; `resistDamage: "45% [25%]"` while active (row 33) | Strand#7 |
| `ascension` (not in this build; the creator's alternative to Flow State, in [skip-grenade-hunter-ascension](../skip-grenade-hunter-ascension/discrepancies.md)) | the same effect, plus up to 181 damage; the air move spends the class ability charge | source in the Compendium, description adds the damage; the rule fires only on the airborne use (`class:air`, ADRs D9), never on a ground dodge | Arc#48 |
| glossary `bolt-charge` | up to x10 | as the Compendium says | Arc#5 |
| glossary `armor-charge` | up to x3 | as the Compendium says | Game Mechanics#94 |
| `orb-of-power` | +1 Armor Charge per pickup (with an Armor Charge mod equipped); super energy `"?"` (depends on the source of the orb) | as the Compendium says | Game Mechanics#94 |
| glossary `slice` | 8 s window, up to x5 | as the Compendium says | Weapon Perks#198 |
| `slice` | class ability → +1 Slice (8s); Strand weapon hit while Slice → Sever + 1 Slice; Max Slice (`max:slice`) → Slice ends (row 10) | as the Compendium says | Weapon Perks#198 |
| `reaper`, glossary `reaper` | 10 s; next weapon kill → Orb | as the Compendium says | Armor Mods#14 |
| `bomber` | 12% \| 17% \| 20% grenade energy per class ability | as the Compendium says | Armor Mods#3 |
| `impact-induction` | 12% \| 17% \| 20% grenade energy, 7 s cooldown | as the Compendium says | Armor Mods#10 |
| `elemental-charge` | Ionic Traces 34% \| 50% \| 50% progress | as the Compendium says | Armor Mods#4 |
| `grenade-kickstart` | 16.2% … 45% by mods + stacks (`perStack: "?"`) | as the Compendium says | Armor Mods#8 |
| `shinobus-vow` | 4.2% [1.4%] and x1 Bolt Charge per hit; `"?"` per Bolt Charge gain; heal `"?"` | as the Compendium says | Exotic Armors#78 |
| `flashover` | +50% Bolt Charge damage | as the Compendium says | Artifact Perks#43 |
| `defibrillating-blast` | discharge → Jolt + heal (`"?"`, ~55 HP) | as the Compendium says | Artifact Perks#45 |
| `horde-shuttle` | weapon damage on unraveled → Threadling (chance); Threadlings sever | as the Compendium says | Artifact Perks#45 |
| `luminopotent-4pc` | jolted kill → Ionic Trace, unconditional | as the Compendium says | Armor Perks#58 |
| `flow-state` | jolted kill → Amplified | as the Compendium says | Arc#49 |
| `spark-of-resistance` | 25% [10%] damage resistance | as the Compendium says | Arc#23 |
| `spark-of-frequency` | +1 Bolt Charge per source while Amplified | as the Compendium says | Arc#16 |
| `spark-of-shock` | Arc grenades jolt | as the Compendium says | Arc#24 |
| `spark-of-discharge` | trace pickup → x1 Bolt Charge; Arc weapon kills → trace (34% / 67% / 100% progress) | as the Compendium says | Arc#13 |
| `jolt` (keyword) | hits on a jolted target chain lightning | as the Compendium says | Arc#8 |
| `unraveling-rounds` (keyword) | Strand weapon hits unravel | as the Compendium says | Strand#11 |
| glossary `ionic-trace` | collects itself (homes in at 6 m/s) | as the Compendium says | Arc#6 |

### Still unknown (`"?"`)

| Element | Value | Why it stays `"?"` |
|---|---|---|
| `gamblers-dodge` | melee energy | 1% per Melee point, doubled near enemies: depends on a stat |
| `combination-blow` | heal; melee damage bonus | in HP, and both vary with the stack level (100 / 80 / 60 / 40 HP; +133% / +266% / +400%) |
| `defibrillating-blast` | heal | ~55 HP: a heal in HP |
| `to-shreds` | heal | 15 HP per pulse: a heal in HP |
| `shinobus-vow` | grenade energy per Bolt Charge gain; heal and its radius | the Compendium has `?` too |
| `grenade-kickstart` | `perStack` | the table is indexed by mods + stacks, not per stack |
| `orb-of-power` | super energy | depends on what spawned the orb (0.8% … 4.4%) |
| `luminopotent-2pc` | handling, reload | the Compendium has `+?` |
| `arc-staff` | `chunkScalar` | supers have no chunk energy scalar (Game Mechanics#12) |
