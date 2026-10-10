# Backlog

Open items. What Loopsmith is and how it behaves is in [requirements.md](requirements.md),
[ADRs.md](../ADRs.md), [rule-format.md](rule-format.md) and [loop-format.md](loop-format.md); this
list is what the code doesn't do yet, what the format can't express yet, and what is still to decide.

## Data

- **Compendium parser** — the 2026-10-09 snapshot was ingested by hand into the Skip Grenade build's
  rules (`compendium/2026-10-09/<tab>#<row>` provenance; source disagreements in
  [builds/skip-grenade-hunter/discrepancies.md](../builds/skip-grenade-hunter/discrepancies.md)). Next:
  a parser slice (a registry of per-tab parsers) instead of hand copying, and the coverage report
  (requirements FR-11). To refresh, run `tools/compendium/get-compendium.ps1` (Windows) or `.sh` locally
  and hand the zip to a session ([tools/compendium/README.md](../tools/compendium/README.md)); a cloud
  session can run `sheet_dump.py` itself once `docs.google.com` is in the environment's allowed domains.
  The raw snapshot stays private (`snapshots/` is gitignored).
- **Clarity ingestion slice** — Clarity is reachable (git clone): `descriptions/clarity.json` maps a
  hash to its name, type and descriptions. Its numbers are in the rules by hand (v2.0625).
- **Bungie manifest** (FR-13; not needed for loop design) — hashes, official names and icons: a DIM-like
  look, noticing game patches, and the rest of a DIM link (FR-14): names for the left-out hashes, weapons
  (slot, name, damage type), the hashes of supers, grenades and melees (so they stop being `?`), and the
  Stasis, Strand and Prismatic subclass items (only the nine Light ones are known, from DIM's source).
  The `BUNGIE_API_KEY` environment secret exists; `www.bungie.net` answered a 500 error page to a
  session (2026-10-09/10, no key). The manifest index itself needs no key (it answered from outside the
  session on 2026-10-10); single definitions (`/Manifest/DestinyInventoryItemDefinition/<hash>/`) do, so
  check with the key in a new session. In the browser, a per-hash lookup with a Bungie key registered
  for the site's origin would avoid downloading the whole item table (tens of MB).

## Rule format gaps (what the Skip Grenade build couldn't express)

- **Triggers:** champion stun (Defibrillating Blast's "stun → max Bolt Charge", with an outcome that
  puts a buff at max); a slide qualifier (Tempest Strike is modelled as any melee); airborne grenade and
  melee actions (Ballistic Slam) — only the class ability has an airborne use (`class:air`, D9);
  blocking / reflecting attacks (Arc Staff's guard grants Bolt Charge per reflected attack).
- **Damage sources:** a specific ability or an ability's element (Skip Grenade vs any grenade, "Arc
  grenades"), "this weapon" for weapon perks, "matches your super's element" (Harmonic Siphon).
- **Conditions:** "target lacks a debuff"; "target has any of" (Dielectric counts Jolt only, Photonic
  Flare Sever only); "while a kind of mod is equipped" (an Orb of Power gives Armor Charge only with an
  Armor Charge mod equipped); "while in your super" (Arc Staff's damage resistance).
- **Declared states:** a level below the max (`stacks:<status>:<n>`, for "Combination Blow ×2"); the
  target's tier (D7: the pack is `minor`, so a kill rule for a higher tier never fires in a loop — "punch
  the big enemy"); "repeat until <state>" as a marker on a step.
- **Facts the format can't show yet** (shown, never applied — most of this build's `?` would become
  readable): health in HP (`restoreHealth` reads as a percentage, so Combination Blow 100 → 40 HP,
  Defibrillating Blast ~55 HP, To Shreds 15 HP per pulse stay `?`); values by declared level (Combination
  Blow +133% / +266% / +400% melee damage); amounts that scale with a stat (Gambler's Dodge: 1% melee per
  Melee point); an orb's energy by what spawned it; ability regeneration and damage resistance (Flow
  State, Tempest Strike, Amplified's extras); an "all weapons" archetype. One "described fact" value and
  passive would cover them. The ability profiles (`charges`, `chunkScalar`, `baseCooldown`) are recorded
  but shown nowhere: show them in `explain`.
- **Written as declarations, *(chance)* or facts** (D3, D8), so they need no new format: progress the
  game doesn't show as a stacking status (Spark of Discharge's kills per trace, To Shreds' 6 weapon hits,
  Amplified's intrinsic kill counter) is *(chance)*; Slice's 5 hits end with `max:slice`; energy per
  stack, refunds, chunk scalars and rule cooldowns (Impact Induction, Photonic Flare) are facts; Spark of
  Resistance's "3+ enemies" always holds.

## Engine

- **Prismatic** builds need per-ability elements; an ability takes the subclass's element.
- **One pack** (D7) is optimistic for a single boss; single-target nuance would be a refinement.
- **Rename the `Simulation` slice**: it plays steps and analyses a loop's order; it simulates nothing (D1).

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
- **No saved library.** The app stores nothing in the browser (D25): a loop is kept by exporting its
  `.loop.yaml` or copying its share link, and loops imported on the Compare screen last until the tab
  closes. A library would need per-browser storage, or a backend to sync it across devices.
- **A build picker** over the catalog instead of build files (FR-1).
- **A preview card for a pasted link**: Home draws a DIM build card for the shares it ships with; a pasted
  link that carries its loadout could show the same card before "Start designing".
- **DIM links without the manifest** (FR-14) open only Arc, Solar and Void builds and leave every weapon
  out, so a DIM build has no weapon triggers yet; a "pick the weapons" step after the import could fill
  the gap before the manifest join. The designer has no way to edit a build either: changing it means
  a new DIM link or build file.

## Owner items

- **Register Loopsmith with DIM's API** so dim.gg links open (a Bungie API key for the site's origin,
  then DIM's `new_app`; [hosting.md](hosting.md#dim-links)), and put the key in
  `src/Loopsmith.Web/wwwroot/appsettings.json`.
- The DIM logo on Home is DIM's (MIT-licensed repository, credited in the file and the README): fine
  to show next to "paste a DIM link", or ask DIM.
- Verify.XunitV3 33.x needs a SponsorCheck licence decision (D26).
- Clarity's partnerships page: check it (the site is public; README, Licensing).
- Confirm the Ascension assumption: Bomber, Reaper and Slice fire on its air move (it is a class ability
  cast — [discrepancies](../builds/skip-grenade-hunter-ascension/discrepancies.md)).
- Delete the merged remote branches `data/compendium-2026-10-09`, `docs/session-notes` and
  `fix/host-conventions` (a session can't).
