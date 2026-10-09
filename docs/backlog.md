# Backlog

Open items collected while building the prototype. Roadmap order lives in the README; this is
the detail behind it.

## Data

- **Compendium snapshot** (next) — the tooling is in the repo (`tools/compendium/sheet_dump.py`;
  the docker command is in the README, plain `python3` + `pip install requests beautifulsoup4`
  works too). A cloud session can run it once `docs.google.com` is added to the environment's
  allowed domains (or run it locally and hand the folder over). The raw snapshot is only needed
  while ingesting: its numbers go into `rules/` with `compendium/<date>/<tab>#<row>` provenance;
  the snapshot itself stays private (`snapshots/` is gitignored). Unlocks ability cooldowns,
  chunk energy scalars, artifact perk numbers and the status glossary — most of today's `?`.
- **Clarity ingestion slice** — Clarity is reachable (git clone); its layout is described in
  the design proposal §03. Today its numbers were copied into the rules by hand (v2.0625).
- **Bungie manifest** (later, not needed for loop design) — hashes, official names and icons.
  Worth it for a DIM-like look (item icons), importing builds from DIM links / loadouts (they
  use hashes) and noticing game patches. Needs `www.bungie.net` allowed and a free API key as an
  environment secret.

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

- **No passive recharge yet.** Abilities only get energy back from rules (orbs, Ionic Traces,
  Shinobu's Vow…); the natural cooldown recharge needs base cooldowns from the Compendium, so
  `wait` restores nothing and loops look *less* sustainable than in game.
- **One "pack" of enemies.** The target is "the enemies in front of you": a debuff on the pack
  stays until it expires, kills don't use enemies up, and there's no health or enemy count
  (ADRs D16). Good for add-clear loops, optimistic for a single boss.
- **Chance always succeeds.** "Chance to" / "rapid kills" perks always fire, marked
  *(chance)* (D15), so orb and trace counts are an upper bound.
- **Prismatic** builds would need per-ability elements; today an ability takes the subclass's element.

## Deferred review items

- A `ChargeCount` value object instead of raw `int` for charges/copies/fragment slots.
- An optional glossary `adjective` ("Frozen") instead of the suffix rule in `DescribeDebuffedAdjective`.
- Build validation: armor energy budget and exotic-weapon limit (FR-2); fragment slots are
  unknown for the current aspects, so the fragment count isn't checked.

## Web

- **First visit downloads ≈ 2.5 MB** (the .NET runtime as WebAssembly + the engine; cached
  afterwards). The `wasm-tools` workload would shrink it; it can't be installed in cloud
  sessions, but the deploy workflows on GitHub's runners could install it.
- **"Save to library" is per browser.** Saved loops live in that browser's storage (other
  devices don't see them; clearing site data deletes them). Export / share links work
  everywhere; syncing a library across devices would need a backend.

## Housekeeping

- Verify.XunitV3 33.x needs a SponsorCheck licence decision (ADRs D20).
