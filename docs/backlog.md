# Backlog

Open items. What Loopsmith is and how it behaves is in [requirements.md](requirements.md),
[ADRs.md](../ADRs.md), [rule-format.md](rule-format.md) and [loop-format.md](loop-format.md); this
list is what the code doesn't do yet, what the format can't express yet, and what is still to decide.

## Bring the code in line with the design (ADRs D1–D3)

The engine, the loop analysis and the hosts still count and time in places the design doesn't. Two
mechanisms are at the core of it: `OutcomeApplication.ApplyBuff` adds stacks up to `maxStacks` and
raises `StacksMaxed` by itself (driving Shinobu's Vow, Flashover, the Bolt Charge strike and Slice's
end), and a `wait` step (`PlayerAction.Wait`, `GameState.Clock`, `ActiveStatus.Remaining`,
`AgeStatuses`) times statuses out — it has no place in the design and goes entirely. The work, contract
first (Domain types, then the slices):

- **State** (`Domain/GameState.cs`): buffs present plus "declared at max" (`ActiveBuff(Status,
  bool AtMax)` or an optional declaration), debuffs and ground pickups present or not; no `Stacks`,
  `Remaining` or `Clock`. An architecture test that `GameState` holds no `Seconds`/`decimal` member and
  no integer but `Step`, so a counter or clock can't come back.
- **Declared states** (D3): `PlayerAction.Declare` with `max:<status>` and `end:<status>` (tokens,
  labels and blocking messages as in [loop-format.md](loop-format.md#action-tokens)); `StacksMaxed`
  only from `max:`; the `atMax` condition and its parse check.
- **Outcomes**: `ApplyBuff` makes the buff present and raises `BuffGained(status)` (no count; cascade
  key `gain:<status>`); remove `restart`; `convertStacksToEnergy` consumes the buff; `extraStacks` only
  adds its caveat; caveats "×N stacks", "restarted (was ×N)" and "refreshed" go; one `pickup` step picks
  up one pickup. `ResolvedValue` → `ReadCertainty` (its value is never read); one `IsStacking`
  (`KeywordResolution.CanReachMaxStacks` duplicates `DomainPhrasing.IsStacking`).
- **Loop analysis** (D2): first pass, then repeated to the repeating pass; verdict "Repeats / Breaks at
  #k"; per step what it needs and from which step, what it sets off, what is wasted. Remove `MaxCycles`,
  `--cycles`, `CompletedCycles`, the `Sources`/`Outcomes`/`Uptime` tallies, the wasted, unknown and
  chance totals. `FiredRule` needs its rule's guards to say where a need came from.
- **Comparison** (D2): the two orders trigger by trigger; remove the metric rows, ✓ marks and tally.
- **Phrasing and hosts**: "Bolt Charge gained" event lines; state chips by name with "(at max)"; one
  phrasing home in kernel `Phrasing` (the web duplicates `TraceRenderer`'s state text); the CLI's
  `simulate` → `trace` (alias kept) and its help; the web palette's "States you declare" group, the
  Analysis view and Compare page rebuilt on the new report, the State panel's "at max"; the build
  check's Info wording (`…; using <current>.`).
- **Loop graph**: grants to a stacking buff lead to "Gain X", plus one declared edge `Gain X → Max X`;
  "gives grenade energy back"; regenerate `builds/*/loop-graph.md`.
- **Rules and build data**: the Bolt Charge discharge rule on `atMax` (Compendium Arc#5); drop Slice's
  `restart`; reword the counting and "v1" comments in `rules/glossary.yaml`, `keywords/arc.yaml`,
  `keywords/orb-of-power.yaml`, `weapons/perks.yaml`, `mods/armor.yaml`, `exotics/armor.yaml`,
  `artifact/current.yaml` and `hunter/arc.yaml` (its header still says there are no airborne qualifiers);
  the Helicopter loop's description ("keep it up"); `max:bolt-charge` steps in the example
  loops and `scenario.txt` as [note-map.md](../builds/skip-grenade-hunter/note-map.md) describes; the
  goldens; `LoopFileParsingTests.SpecExampleYaml`, which copies loop-format.md's example.
- Later: rename the `Simulation` slice (it plays steps; it simulates nothing).

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
  look, importing builds from DIM links / loadouts (they use hashes) and noticing game patches. The
  `BUNGIE_API_KEY` environment secret exists; `www.bungie.net` is reachable from a session but answered
  a 500 error page (2026-10-09, no key), so check manifest access with the key in a new session first.

## Rule format gaps (what the Skip Grenade build couldn't express)

Only gaps in cause and effect are gaps (ADRs D1).

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
  target's tier (D7: the pack is `minor`, so `kill … tier:` rules never fire in a loop — "punch the big
  enemy"); "repeat until <state>" as a marker on a step.
- **Facts the format can't show yet** (shown, never applied — most of this build's `?` would become
  readable): health in HP (`restoreHealth` reads as a percentage, so Combination Blow 100 → 40 HP,
  Defibrillating Blast ~55 HP, To Shreds 15 HP per pulse stay `?`); values by declared level (Combination
  Blow +133% / +266% / +400% melee damage); amounts that scale with a stat (Gambler's Dodge: 1% melee per
  Melee point); an orb's energy by what spawned it; ability regeneration and damage resistance (Flow
  State, Tempest Strike, Amplified's extras); an "all weapons" archetype. One "described fact" value and
  passive would cover them. The ability profiles (`charges`, `chunkScalar`, `baseCooldown`) are recorded
  but shown nowhere: show them in `explain`.
- **Not gaps (D1)** — counting and timing, which the format never models: hit/kill counters and progress
  meters across actions (Spark of Discharge's kills per trace, To Shreds' 6 weapon hits, Amplified's
  intrinsic kill counter — *(chance)*, or the player declares the result); several hits on one enemy in
  one action; how many hits Slice lasts; energy per stack; refunds and chunk scalars; rule cooldowns
  (Impact Induction, Photonic Flare); buff decay (Armor Charge); "enemies nearby" (Spark of Resistance's
  "3+ enemies" holds always).

## Engine

- **Prismatic** builds need per-ability elements; an ability takes the subclass's element.
- **One pack** (D7) is optimistic for a single boss; single-target nuance would be a refinement.

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

## Owner items

- Verify.XunitV3 33.x needs a SponsorCheck licence decision (D26).
- Clarity's partnerships page: check it now that the site is public (README, Licensing).
- Confirm the Ascension assumption: Bomber, Reaper and Slice fire on its air move (it is a class ability
  cast — [discrepancies](../builds/skip-grenade-hunter-ascension/discrepancies.md)).
- Delete the merged remote branches `data/compendium-2026-10-09`, `docs/session-notes` and
  `fix/host-conventions` (a session can't).
