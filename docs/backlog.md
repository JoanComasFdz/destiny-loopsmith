# Backlog

Open items collected while building the prototype. Roadmap order lives in the README; this is
the detail behind it.

## Data (needs the owner)

- **Compendium snapshot** — run `tools/compendium/sheet_dump.py` locally (README) and hand the
  dated folder to a session; it stays private (`snapshots/` is gitignored). Unlocks ability
  cooldowns, chunk energy scalars, artifact perk numbers and the status glossary — most of
  today's `?` values.
- **Bungie manifest** — needs `www.bungie.net` allowed in the cloud environment and an API key
  as an environment secret. Unlocks hashes, names and icons (ADRs D1/D13).
- **Clarity ingestion slice** — Clarity is reachable (git clone); its layout is described in
  the design proposal §03. Today its numbers were copied into the rules by hand (v2.0625).

## Rule format gaps (what the Skip Grenade build couldn't express)

- Triggers: champion stun (Defibrillating Blast's "stun → max Bolt Charge"); slide / airborne
  qualifiers (Tempest Strike is modelled as any melee).
- Damage sources: a specific ability (Skip Grenade vs any grenade), "this weapon" for weapon
  perks, "matches your super's element" (Harmonic Siphon).
- Conditions: "target lacks a debuff"; "target has any of" (Dielectric counts Jolt only).
- Numbers: hit/kill counters and progress meters (Spark of Discharge ≈ 3 kills per trace),
  amounts that scale with stats (Gambler's Dodge: 1% melee per Melee stat), a flag for
  refunds not affected by chunk energy scalars (Shinobu's Vow).
- Passives: ability regeneration and timed damage resistance (Flow State, Tempest Strike),
  an "all weapons" archetype; rule cooldowns.

## Engine model

- Base cooldowns are unknown, so `wait` regenerates nothing until the Compendium lands.
- Target model is "the pack in front of you" (ADRs D16); single-target fights aren't modelled.
- Chance rules always fire, marked *(chance)* (D15).
- Prismatic: per-ability damage types aren't modelled (subclass → one damage type).

## Deferred review items

- A `ChargeCount` value object instead of raw `int` for charges/copies/fragment slots.
- An optional glossary `adjective` ("Frozen") instead of the suffix rule in `DescribeDebuffedAdjective`.
- Build validation: armor energy budget and exotic-weapon limit (FR-2); fragment slots are
  unknown for the current aspects, so the fragment count isn't checked.

## Web

- First load ≈ 2.5 MB (brotli); the `wasm-tools` workload (not installable in cloud sessions
  today) would shrink `dotnet.native.wasm`.
- Saved loops live in the browser's localStorage; an API host would make them shareable
  across devices (the share link already works without one).

## Housekeeping

- Verify.XunitV3 33.x needs a SponsorCheck licence decision (ADRs D20).
