# Backlog

Open items collected while building the prototype. Roadmap order lives in the README; this is
the detail behind it.

## Data

- **Compendium snapshot** (next) — run `tools/compendium/get-compendium.ps1` (Windows) or
  `.sh` locally and hand the zip to a session ([tools/compendium/README.md](../tools/compendium/README.md));
  a cloud session can also run `sheet_dump.py` itself once `docs.google.com` is in the
  environment's allowed domains. The raw snapshot is only needed
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
- Numbers: hit/kill counters and progress meters across actions (Spark of Discharge ≈ 3 kills per
  trace), amounts that scale with stats (Gambler's Dodge: 1% melee per Melee stat), a flag for
  refunds not affected by chunk energy scalars (Shinobu's Vow) — the last two matter once energy is
  simulated again. (*Within one action* counts are done: `damage/kill … atLeast: N`, see below.)
- Passives: ability regeneration and timed damage resistance (Flow State, Tempest Strike),
  an "all weapons" archetype; rule cooldowns.

## Engine model

- **No ability-energy model — a decision, not a gap** (ADRs D21). Energy is mainly time (cooldowns
  scaled by stats), which the step engine can't simulate; the earlier refunds-only model had no
  passive recharge, so `wait` restored nothing, loops looked *less* sustainable than in game and steps
  were blocked that are fine in game. Now abilities are always available, energy outcomes are
  explanations and a loop reports the **energy refunded per cycle** per ability. Revisit once the
  Compendium's base cooldowns and chunk scalars are in and there is a time model.
- **Multi-target actions — done** (ADRs D22). The player says how many enemies an action hits or kills
  (`grenade:kill:3`, `kinetic:hit:5`); each enemy cascades on its own, and `damage/kill … atLeast: N`
  triggers express "hitting three separate targets" (One For All) and multi-kill perks. The web
  designer picks the count per hit/kill pair (1–9; the format allows up to 20).
- **One "pack" of enemies.** The target is "the enemies in front of you": a debuff on the pack
  stays until it expires, kills don't use enemies up, and there's no health or enemy count
  (ADRs D16). Good for add-clear loops, optimistic for a single boss.
- **Chance always succeeds.** "Chance to" / "rapid kills" perks always fire, marked
  *(chance)* (D15) — on every enemy of a multi-target action — so orb and trace counts are an upper bound.
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
- **No saved library (for now).** The app stores nothing in the browser: a loop is kept by
  exporting its `.loop.yaml` or copying its share link, and loops imported on the Compare
  screen last until the tab closes. A library could come back later (per-browser storage, or
  a backend to sync it across devices).

## Housekeeping

- Verify.XunitV3 33.x needs a SponsorCheck licence decision (ADRs D20).
