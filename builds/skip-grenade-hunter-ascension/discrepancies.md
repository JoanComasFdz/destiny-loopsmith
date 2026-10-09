# Skip Grenade Hunter (Ascension): where the sources disagree

The creator's own variant of [skip-grenade-hunter](../skip-grenade-hunter/): the same build with
Ascension instead of Flow State. Everything the two builds share — every other element, its sources
and its disagreements — is in the parent's [discrepancies.md](../skip-grenade-hunter/discrepancies.md);
this file lists only what the swap changes.

Sources, in priority order:

1. **Game data**: the **Destiny Data Compendium** snapshot of 2026-10-09 (private, not in git), cited
   as `compendium/2026-10-09/<Tab>#<row>`: Arc#48 for Ascension, Arc#49 for Flow State.
2. The user's **note**: there is none for this variant (no `note.txt` or `note-map.md`); the parent's
   note and note map cover everything the builds share.
3. The creator's **video** (https://www.youtube.com/watch?v=zvd6sNS463E; its transcript is kept
   locally, not in git): the quotes in `build.yaml`'s header.

| # | Topic | Note says | Video says | Game data says | Encoded | Why |
|---|---|---|---|---|---|---|
| 1 | Second aspect | — | "Your second aspect is up to you. You either take flow state or ascension." | Compendium Arc#49: Flow State makes a jolted kill grant Amplified. Arc#48: Ascension is an air move (below) | `aspects: [tempest-strike, ascension]`; nothing else changes. Jolted kills no longer give Amplified: it comes from the air move and from Flashover (max Bolt Charge) | The creator's alternative. In the "Helicopter skip grenades" loop Amplified is still up after every step, and the loop equals the parent's "Infinite skip grenades" on every metric (`compare`) |
| 2 | The air move | — | "It's just another source of jolt." | Compendium Arc#48: while airborne with a class ability charge, the air move spends the charge on an Arc Staff twirl: up to 181 damage and Jolt to enemies within 10 m, Amplified for you and allies within 10 m, and the dodge's effects trigger | `ascension`: `{ abilityCast: { ability: classAbility, airborne: true } }` → Jolt target + Amplified, played as `class:air`. It is a class ability cast, so Gambler's Dodge's rule fires on it too (ADRs D26) | Game data. The airborne trigger keeps a ground dodge (`class`) from firing it. The pack is jolted before the grenade lands, so the grenade's hit already chains lightning (the `jolt` keyword). The damage isn't encoded (no damage model); the allies and the 10 m radius aren't modelled (one player, one pack) |
| 3 | Class ability mods and perks on the air move | — | — | Compendium Arc#48 says the dodge's own effects trigger; it doesn't say whether the mods and perks that react to a class ability use count the air move (Bomber, Armor Mods#3; Reaper, Armor Mods#14; Slice, Weapon Perks#198) | They fire on it: `class:air` fires every `abilityCast: classAbility` rule, so Bomber's grenade energy, Reaper and Slice ×1 (restarting) come with each air move | **Assumption**, not game data: the air move spends the class ability charge, which is what those mods and perks react to. If they don't count it, the helicopter loop loses Reaper's orb and Slice's severs |
| 4 | Ascension's damage and Class | — | — | Compendium Arc#48: the damage scales with Class above 100, up to +65% at 200 | Not encoded | There is no damage model, and an amount can't depend on a stat ([backlog](../../docs/backlog.md), rule format gaps). The build's 104 Class is barely above 100 |
| 5 | What Flow State gave up | — | Prefers Flow State because the dodge comes back sooner when a melee doesn't kill (paraphrased) | Compendium Arc#49: while Amplified, Flow State makes the class ability regenerate much faster (and resists damage during the dodge) | Not encoded, in either build | Ability energy isn't simulated (ADRs D21): abilities are always available, so the trade-off the creator weighs — faster dodges against an extra source of Jolt and Amplified — doesn't show in a loop report |

**Count: 5 rows.** Rows 1 and 2 are the swap itself (game data and the video agree); row 3 is an
assumption to confirm; rows 4 and 5 are what the engine can't show.
