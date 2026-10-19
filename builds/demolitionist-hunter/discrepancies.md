# Demolitionist Hunter: where the sources disagree

A test build for Demolitionist, the weapon trait whose two halves make a grenade loop: the grenade
refills the weapon, and the weapon's kills give grenade energy back. It holds only what that loop needs.

Sources, in priority order:

1. **Game data**: the **Destiny Data Compendium** snapshot of 2026-10-14 (private, not in git), cited
   as `compendium/2026-10-14/<Tab>#<row>`: Weapon Perks#62 for Demolitionist.
2. The owner's **request**: the loop Mactics plays, an Arc Hand Cannon rolling Demolitionist. There is no
   note (no `note.txt` or `note-map.md`) and no DIM link.

| # | Topic | Request says | Game data says | Encoded | Why |
|---|---|---|---|---|---|
| 1 | The rest of the build | An Arc Hand Cannon with Demolitionist; the grenade's type doesn't matter | — | An Arc Hunter with Skip Grenade, the Super `"?"`, nothing else equipped | Demolitionist reacts to any grenade, so the grenade is the subclass's own; what Mactics equips beside it isn't known, and nothing is guessed |
| 2 | The hand cannon | An Arc Hand Cannon | — | Crisis Inverted (`hash: 2888266564`), an Arc Hand Cannon that rolls Demolitionist | No source names the weapon, and a build's weapon needs a hash (its slot, damage type, archetype and ammo type are the manifest's), so Crisis Inverted stands in for it. Its archetype is what picks Demolitionist's amount |
| 3 | Grenade energy per kill | — | Weapon Perks#62: 10% (11% enhanced) per weapon kill; 20% (22%) with Fusion Rifles, Glaives, Shotguns and Sniper Rifles | `grantEnergy` 10% with `byArchetype` 20% for those four; the hand cannon gets 10% | Game data. The weapon's archetype picks the amount when the build is composed; with it unknown, both show |
| 4 | The refill | — | Weapon Perks#62: using the grenade refills the magazine, with a 3 s cooldown between refills | `{ abilityCast: grenade }` → `refillWeapon: weapon:this`, a refill and not a reload, so no on-reload perk fires (ADRs D42, Weapon Perks#250 in the 2026-10-19 snapshot); the cooldown is a fact in the description | No ammo and no time are counted (ADRs D1) |
