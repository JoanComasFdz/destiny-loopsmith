# Backlog

Open items collected while building the prototype. Roadmap order lives in the README; this is
the detail behind it.

## Data

- **Compendium snapshot** — the 2026-10-09 snapshot was ingested **by hand** for the Skip Grenade
  build's rules (`compendium/2026-10-09/<tab>#<row>` provenance; what changed is listed in
  [builds/skip-grenade-hunter/discrepancies.md](../builds/skip-grenade-hunter/discrepancies.md)).
  Next: a parser slice instead of hand copying. To refresh, run `tools/compendium/get-compendium.ps1`
  (Windows) or `.sh` locally and hand the zip to a session
  ([tools/compendium/README.md](../tools/compendium/README.md)); a cloud session can also run
  `sheet_dump.py` itself once `docs.google.com` is in the environment's allowed domains. The raw
  snapshot is only needed while ingesting; it stays private (`snapshots/` is gitignored).
- **Clarity ingestion slice** — Clarity is reachable (git clone); its layout is described in
  the design proposal §03. Today its numbers were copied into the rules by hand (v2.0625).
- **Bungie manifest** (later, not needed for loop design) — hashes, official names and icons.
  Worth it for a DIM-like look (item icons), importing builds from DIM links / loadouts (they
  use hashes) and noticing game patches. The `BUNGIE_API_KEY` environment secret exists;
  `www.bungie.net` is reachable from a session but answered a 500 error page (2026-10-09, no key),
  so check manifest access with the key in a new session first.

## Rule format gaps (what the Skip Grenade build couldn't express)

- Triggers: champion stun (Defibrillating Blast's "stun → max Bolt Charge"); slide / airborne
  qualifiers (Tempest Strike is modelled as any melee); an air move (Ascension is modelled as a
  class ability cast); blocking / reflecting attacks (Arc Staff's guard grants Bolt Charge per
  reflected attack).
- Damage sources: a specific ability or an ability's element (Skip Grenade vs any grenade, "Arc
  grenades"), "this weapon" for weapon perks, "matches your super's element" (Harmonic Siphon).
- Conditions: "target lacks a debuff"; "target has any of" (Dielectric counts Jolt only, Photonic
  Flare Sever only); a guard on a stack count (Bolt Charge discharges on the next ability hit *at*
  x10, modelled as discharging on reaching x10); "while a kind of mod is equipped" (an Orb of Power
  gives Armor Charge only with an Armor Charge mod equipped); "while in your super" (Arc Staff's
  damage resistance); enemies nearby (Spark of Resistance's "3+ enemies" is treated as always
  true); "not on a killing hit" (Slice: the hit's event comes before the kill's).
- Numbers: hit/kill counters and progress meters across actions (Spark of Discharge ≈ 3 kills per
  trace, To Shreds' 6 weapon hits, Amplified's intrinsic kill counter), amounts that scale with
  stats (Gambler's Dodge: 1% melee per Melee stat; Combination Blow's class refund needs 70 Class
  to fill a dodge), a flag for refunds not affected by chunk energy scalars (Shinobu's Vow) — the
  last two matter only if ability energy is ever simulated (D21). (*Within one action* counts are
  done: `damage/kill … atLeast: N`, see below.)
- Numbers: **several hits on one enemy in one action** — a Skip Grenade seeker hits up to 3 times,
  v1 emits one hit per enemy, so Shinobu's Vow's per-hit grant is undercounted; **a pickup's value by
  what spawned it** (an orb's super energy depends on its source, so it stays `?`).
- Numbers: **health in HP** — `restoreHealth` amounts read as percentages, so HP heals stay `?`
  (Combination Blow 100 → 40 HP, Defibrillating Blast ~55 HP, To Shreds 15 HP per pulse, orbs 0.7 HP
  per Health point); **energy per stack gained** — a gain of 2 stacks grants once (Bolt Charge's 2.5%
  melee per stack misses Spark of Frequency's extra stack); **values by stack count** (Combination
  Blow +133% / +266% / +400% melee damage); a buff armed at 0 stacks (Slice severs 5 times, v1 4).
- Passives: ability regeneration and timed damage resistance (Flow State, Tempest Strike),
  an "all weapons" archetype; rule cooldowns; buff decay (Armor Charge loses stacks over time).

## Engine model

- **No ability-energy model — a decision, not a gap** (ADRs D21). Energy is mainly time (cooldowns
  scaled by stats), which the step engine can't simulate; the earlier refunds-only model had no
  passive recharge, so `wait` restored nothing, loops looked *less* sustainable than in game and steps
  were blocked that are fine in game. Now abilities are always available and energy outcomes are
  explanations only — no refund totals either: the owner doesn't want exact energy, refund or
  cooldown maths; the analysis shows what works together and what is wasted. Revisit only if a time
  model is wanted (the Compendium's base cooldowns and chunk scalars would feed it).
- **Rules that don't stack — done** (ADRs D23). `doesNotStackWith: [<element>]` on a rule makes it give
  nothing when a listed element's rule fires on the same event (Tempest Strike's Bolt Charge on a
  jolted kill gives way to Dielectric's, Compendium Arc#51). The build check warns, `explain` annotates
  the bullet and a loop report counts it under **Wasted per cycle** (the creator's loop: 7×, and 3
  Bolt Charge maxes per cycle instead of the 6 v1 showed when both fired). Granularity is the whole
  rule, not one outcome.
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

- **First visit downloads ≈ 3 MB** (the .NET runtime as WebAssembly + the engine: 2.7 MB brotli,
  3.2 MB gzip in a Release publish; cached afterwards). The `wasm-tools` workload would shrink it;
  it can't be installed in cloud sessions, but the deploy workflows on GitHub's runners could
  install it.
- **No saved library (for now).** The app stores nothing in the browser: a loop is kept by
  exporting its `.loop.yaml` or copying its share link, and loops imported on the Compare
  screen last until the tab closes. A library could come back later (per-browser storage, or
  a backend to sync it across devices).

## Housekeeping

- Verify.XunitV3 33.x needs a SponsorCheck licence decision (ADRs D20).
