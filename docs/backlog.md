# Backlog

Open items collected while building the prototype. Roadmap order lives in the README; this is
the detail behind it.

## Not a simulator (ADRs D28)

The rework D28 asks for, **not started**: an inventory of everything in the code, data and docs that
counts or times, and what replaces it. Collected by four read-only reviews (engine, analysis/UI,
graph/wording, rule format/data) on 2026-10-09. Nothing here is implemented; the owner decisions at the
end come first.

**The two mechanisms that break the principle.**
1. *Stack arithmetic decides causality.* `OutcomeApplication.ApplyBuff` adds stacks, caps them at
   `maxStacks` and raises `StacksMaxed` by itself. That one event drives Bolt Charge's lightning strike,
   Shinobu's Vow (New Tricks), Flashover and Slice's end, and produces "Bolt Charge maxed 3 per cycle".
2. *A clock.* `wait`, `GameState.Clock`, `ActiveStatus.Remaining` and `AgeStatuses` count time and expire
   buffs and debuffs.

Everything else is small, or is already "facts shown". No rule-YAML key has to go except `restart`; the
user's note is already written in declared states ("Amplified ->", "Max bolt charge ->").

### Game state and the engine (`Domain`, `Simulation`)
- **Buffs are present, plus what the player declared**, never counted: `ActiveBuff(Status,
  Optional<DeclaredStacks>)`, where `DeclaredStacks` is `AtMax` or `Exactly(n)`. Debuffs are present on the
  pack (`ImmutableArray<StatusId>`), pickups are on the ground or not. No `Stacks`, no `Remaining`.
- **No automatic maximum.** `ApplyBuff` makes the buff present (a gain keeps a declared `AtMax`; it
  clears a declared `Exactly(n)`); `BuffGained` fires on every grant of a stacking buff and when a
  non-stacking one becomes active; **`StacksMaxed` comes only from the player's declaration.**
- **No time.** Remove `PlayerAction.Wait`, `GameState.Clock`, `AgeStatuses` and the `wait` token. A status
  ends when a rule removes or consumes it, or when the player declares it ended. Durations stay facts on
  the outcome bullet ("Amplified (15s)").
- **`GameEvent.BuffGained(Status)`** without its stack count; the cascade key becomes `gain:<status>` (D17
  still terminates: a coarser key means fewer re-fires).
- **Remove `restart`** (D27): it only existed for stack arithmetic.
- **Caveats become facts.** "×2 stacks", "restarted (was ×N)" and "refreshed" go; `extraStacks` stays as
  the caveat "+1 from Spark of Frequency" and adds nothing. `convertStacksToEnergy` consumes the buff if it
  is present ("was not active" otherwise); `perStack` reads "per stack".
- **Pickups:** `pickup:<id>[:N]` — the player says how many they collect (a `PickupCount`, like D22); the
  ground no longer piles orbs up across actions (3 orbs → 3 Armor Charge gains today).
- **Simplify:** `ResolvedValue` is never read for its value — replace it with `ReadCertainty`; keep one
  `IsStacking` (`KeywordResolution.CanReachMaxStacks` duplicates `DomainPhrasing.IsStacking`).
- **Guard:** an architecture test that `GameState` holds no `Seconds`/`decimal` member and no integer but
  `Step` and the declared counts, so a counter or clock can't come back unnoticed.

### States the player declares (replaces `wait` and the automatic max)

| Token | Reads | Does |
|---|---|---|
| `max:<status>` | "Bolt Charge at max" | the buff is present and declared at max → `StacksMaxed` cascades (New Tricks, Flashover…) |
| `end:<status>` | "Amplified ends" / "Jolt ends" | removes the buff, or the debuff from the pack |
| `stacks:<status>:<n>` (later) | "Combination Blow ×3" | a declared count below the max, for "repeat until ×3, then punch the big enemy" |
| a target tier (later) | "a big enemy" | lets `tier` triggers fire (today the pack is always minor, so they never do) |

- Only a status that stacks (`maxStacks` ≥ 2) can be declared at max; `stacksMaxed`, `max:` and the new
  condition check it when parsing (today `stacksMaxed: amplified` parses and silently never fires).
- A new condition `{ atMax: <status> }` lets Bolt Charge discharge on **the next ability hit at x10**, as
  the Compendium says (Arc#5), instead of on reaching it (fixes discrepancy row 23):
  `on: { damage: { via: ability } }`, `when: [ { atMax: bolt-charge } ]`.
- The web palette's "Time" group becomes **"States you declare"**, offered only when an equipped rule
  reacts to the state (as with `class:air`, D26); the designer's "new" badge marks a declaration that just
  became possible. Later: `applyBuff … stacks: max` (Defibrillating Blast's champion stun) and a "repeat
  until <state>" marker on a step.

### Loop analysis and comparison (`LoopReport`, `ReportComparison`, hosts)
- **Does it close?** instead of "N of 10 cycles": play the loop from a fresh spawn, then again from where
  it ended, until a cycle starts in a state already seen (with yes/no state this settles in about two
  cycles, and it is a proof, not a sample). Verdict: *Closes*, *Closes once started* (the first cycle lacks
  something the repeating one has), *Breaks when repeated at #k*, *Breaks at #k*. Remove `--cycles`,
  `MaxCycles`, `DefaultMaxCycles` and "10+".
- **Remove the counts:** Kills, `<Pickup> spawned`, `<Status> maxed`, buff uptime ("5/6 steps"), "Fired per
  cycle 22×" and its bars, "Wasted 7×", the totals of `?` values and *(chance)* bullets.
- **Report instead** (text and web show the same sections):
  1. the verdict, with the reason and the step;
  2. first cycle vs the repeating one (steps whose firings differ);
  3. each step gets what it needs — a pickup ← who dropped it, a consumed buff ← who armed it, a guard ← who
     set it, what carries over from the previous cycle;
  4. **You declare** — each declared state → what it set off;
  5. **Elements** — take part (with their steps) · only give way · never fire (with their trigger, or
     "waits for a state you declare") · passives;
  6. **Wasted** — each (element, partner) pair with its steps, flagged when the element gives nothing else;
  7. **Buffs and debuffs** — gained at, ended at (by whom), carried over, needed by;
  8. **Pickups** — dropped, collected, left on the ground;
  9. *(chance)* links and unknown values (`?`) as distinct lists, not totals.
- **Comparison rows:** Closes (ordinal), Wasted (fewer pairs), Never fire (fewer), then not judged: steps,
  declared states, elements and buffs only in one loop, chance links, unknowns.
- Steps are numbered by their place in the design (#1–#6 in the repeating cycle too, not #7–#12).
- Optional: record rules whose trigger matched but whose guard failed ("Grenade Kickstart — grenade
  thrown at #2 without Armor Charge"); "closes without chance links: yes/no".

### Wording, graph and hosts
- **Trace and state:** "↳ Bolt Charge ×5" → "↳ Bolt Charge gained"; state chips and the state block show
  names only ("Reaper · Slice · Bolt Charge"), plus the declared states; "3× Orb of Power" → "Orb of Power";
  "Slice ×1 (8s, restarts)" → "Slice (8s)". One phrasing home in kernel `Phrasing` (the web duplicates
  `TraceRenderer`'s today).
- **Graph:** a grant to a capped buff links only to "Gain X"; one *declared* edge `Gain X → Max X`
  ("you say: at max", dotted) replaces today's arrow per grant straight to "Max X", which implied "this +1
  is the one that maxes it". Regenerate `builds/*/loop-graph.md`. "refunds grenade energy" → "gives
  grenade energy back".
- **CLI:** drop `--cycles` and `wait` from the help; the `--caveats` example ("×3 stacks"); "ability energy
  isn't simulated" → the D28 wording; rename `simulate` → `trace` (keep `simulate` as an alias).
- **Web:** rebuild `AnalysisView` on the sections above (no meters); `StatePanel` gets a Declared row;
  copy in `Compare` ("for up to N cycles"), `MainLayout` ("Nothing can be simulated…") and the caveats
  toggle's title.
- `BuildValidation`: "simulating with" → "using rules". Optional, last: rename the `Simulation` slice.

### Rules, builds and docs
- **Rules:** drop Slice's `restart`; reword the counting comments in `glossary.yaml`,
  `keywords/arc.yaml`, `weapons/perks.yaml`, `mods/armor.yaml` and `hunter/arc.yaml`.
- **Loops:** `max:bolt-charge` before the grenade that discharges in `infinite-skip-grenades` and
  `helicopter-skip-grenades` (fix their notes); `melee-first` is the designer's choice. `scenario.txt`:
  the token header and the declaration for note line 10.
- **Prose:** both builds' `discrepancies.md` (rows that count or time), `note-map.md` (end states become
  yes/no; lines 1, 3, 6, 7b and 10), the goldens.
- **Docs:** `rule-format.md` (engine semantics, `restart`, the `maxStacks`/`duration` wording),
  `loop-format.md` (tokens, analysis, comparison, CLI), README (the trace sample, legend and command table).

### Owner decisions before starting
1. Old `wait` steps in share links and files: read and dropped with an Info note (recommended), or an
   error? Old `--cycles`: "unknown option" (recommended)?
2. Declaring `max:`/`end:` on a status that isn't active: a note (recommended — it covers sources the rules
   don't model, like a champion stun) or a blocked step?
3. "Fired ×N" / "Wasted ×N": replace by the steps where it happens (recommended), or keep the counts?
4. A loop whose first cycle is blocked but which repeats fine: "Closes once started" (recommended)?
5. May a comparison judge structural counts — fewer wasted pairs, fewer elements that never fire
   (recommended: yes, always shown with the names)?
6. Rename `simulate` → `trace` (recommended), and later the `Simulation` slice?

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

Sorted by ADRs D28: only gaps in cause and effect are gaps; counting and timing are non-goals.

- **Triggers:** champion stun (Defibrillating Blast's "stun → max Bolt Charge"); a slide qualifier
  (Tempest Strike is modelled as any melee); airborne grenade and melee actions (Ballistic Slam) —
  only the class ability has an airborne use (`class:air`, ADRs D26); blocking / reflecting attacks
  (Arc Staff's guard grants Bolt Charge per reflected attack).
- **Damage sources:** a specific ability or an ability's element (Skip Grenade vs any grenade, "Arc
  grenades"), "this weapon" for weapon perks, "matches your super's element" (Harmonic Siphon).
- **Conditions:** "target lacks a debuff"; "target has any of" (Dielectric counts Jolt only, Photonic
  Flare Sever only); a declared state (`{ atMax: bolt-charge }`, D28 — Bolt Charge discharges on the next
  ability hit *at* x10, modelled as discharging on reaching x10); "while a kind of mod is equipped" (an Orb
  of Power gives Armor Charge only with an Armor Charge mod equipped); "while in your super" (Arc Staff's
  damage resistance). (*Within one action* counts are done: `damage/kill … atLeast: N`, D22.)
- **Facts the format can't show yet** — shown, never applied or counted as unknown, so most of this
  build's `?` would become readable: health in HP (`restoreHealth` reads as a percentage, so Combination
  Blow 100 → 40 HP, Defibrillating Blast ~55 HP, To Shreds 15 HP per pulse stay `?`); values by stack
  count (Combination Blow +133% / +266% / +400% melee damage); amounts that scale with a stat (Gambler's
  Dodge: 1% melee per Melee point); an orb's energy by what spawned it; ability regeneration and damage
  resistance (Flow State, Tempest Strike, Amplified's extras); an "all weapons" archetype. One "described
  fact" value and passive would cover them. Also: the ability profiles (`charges`, `chunkScalar`,
  `baseCooldown`) are parsed but shown nowhere — show them in `explain`, or drop them.
- **Non-goals (D28)** — about counting or timing, so never modelled: hit/kill counters and progress meters
  across actions (Spark of Discharge's kills per trace, To Shreds' 6 weapon hits, Amplified's intrinsic kill
  counter — they stay *(chance)*, or the player declares the result); several hits on one enemy in one
  action (a Skip Grenade seeker hits up to 3 times); a buff armed at 0 stacks (Slice severs 5 times, v1 4);
  "not on a killing hit" (Slice; it only changed the count); energy per stack gained; refunds exempt from
  chunk scalars; rule cooldowns (Impact Induction, Photonic Flare); buff decay (Armor Charge); "enemies
  nearby" (Spark of Resistance's "3+ enemies" is treated as always true — positional; at most a declared
  state).

## Engine model

- **No ability-energy model — a decision, not a gap** (ADRs D21). Energy is mainly time (cooldowns
  scaled by stats), which the step engine can't simulate; the earlier refunds-only model had no
  passive recharge, so `wait` restored nothing, loops looked *less* sustainable than in game and steps
  were blocked that are fine in game. Now abilities are always available and energy outcomes are
  explanations only — no refund totals either: the owner doesn't want exact energy, refund or
  cooldown maths; the analysis shows what works together and what is wasted. D28 extends this to
  everything: no stack counting and no time either (see "Not a simulator" above).
- **Rules that don't stack — done** (ADRs D23). `doesNotStackWith: [<element>]` on a rule makes it give
  nothing when a listed element's rule fires on the same event (Tempest Strike's Bolt Charge on a
  jolted kill gives way to Dielectric's, Compendium Arc#51). The build check warns, `explain` annotates
  the bullet and a loop report counts it under **Wasted per cycle** (D28: to become a list of the
  pairs and the steps where they happen). Granularity is the whole
  rule, not one outcome. The loop graph routes both arrows through a "Doesn't stack" node, but only
  for rules on the **same trigger**: rules whose triggers differ yet match one event (a `kill via any`
  rule and a `kill via type:arc` one, on an Arc kill) each keep a direct arrow.
- **Multi-target actions — done** (ADRs D22). The player says how many enemies an action hits or kills
  (`grenade:kill:3`, `kinetic:hit:5`); each enemy cascades on its own, and `damage/kill … atLeast: N`
  triggers express "hitting three separate targets" (One For All) and multi-kill perks. The web
  designer picks the count per hit/kill pair (1–9; the format allows up to 20).
- **One "pack" of enemies.** The target is "the enemies in front of you": a debuff on the pack
  stays until it expires (D28: until a rule removes it or the player says it ended), kills don't use
  enemies up, and there's no health or enemy count (ADRs D16). Good for add-clear loops, optimistic for
  a single boss.
- **Chance always succeeds.** "Chance to" / "rapid kills" perks always fire, marked
  *(chance)* (D15) — on every enemy of a multi-target action. Today's orb and trace counts are an upper
  bound; D28 replaces the counts with the list of chance links.
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
