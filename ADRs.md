# Architecture decision records

Each ADR explains **why**; [`CONVENTIONS.md`](./CONVENTIONS.md) says **what** and wins
on any conflict. D1–D12 come from the design proposal
([`docs/design/loopsmith-design-v0.3.html`](docs/design/loopsmith-design-v0.3.html), §04);
D13+ were decided while building the CLI prototype.

| # | Decision | Why |
|---|---|---|
| D1 | Bungie manifest hash is every element's long-term identity (`ItemHash`) | Stable, complete, shared by Clarity and DIM |
| D2 | Compendium is the primary mechanics source; Clarity the cross-check | Compendium covers abilities, cooldowns, chunk scalars, artifact perks, statuses; Clarity has hashes |
| D3 | Compendium joined by name, scoped by kind + class, alias table in git | It has no hashes; unresolved names are reported, never guessed |
| D4 | Causality authored as YAML; drafts generated from "On X:" phrasing | 55% of Compendium descriptions already state the trigger; a human confirms every rule |
| D5 | Every number is a `GameValue` with `Provenance` | ≈29% of descriptions contain a "?"; unknown ≠ 0; every number traceable |
| D6 | Vertical slices as folders, guarded by architecture tests | One change = one slice; boundaries can't silently rot |
| D7 | Strict impure → pure → impure; effects as data | Pure core is deterministic, replayable, testable without infrastructure |
| D8 | Closed vocabulary as Dunet DUs; behaviour in static functions; no interfaces | Exhaustive `Match`; the compiler lists every place to update |
| D9 | Typestate: Simulation accepts only `ValidatedBuild` | "Simulate before validation" does not compile / is caught by the architecture test |
| D10 | Vogen value objects validated at the boundary | Inside the core every value is known-valid |
| D11 | Railway (ROP) for ingest and simulate; `Severity` only where the app branches | Short-circuit on first blocking error, no app-state object |
| D12 | Step-based time in v1; builds pin `CatalogVersion` | Matches how players think; patches don't silently change saved builds |

## D13 — Authored `ElementId` slugs until the manifest join exists

**Context.** The Bungie manifest (and the API key it needs) is not reachable from the
build environment yet, and abilities / artifact perks / armor set bonuses have no
Clarity hash. **Decision.** Elements are keyed by a kebab-case `ElementId`
(`shinobus-vow`); `Hash` is `Optional<ItemHash>`, filled from Clarity where known.
`CatalogPublishing` will resolve names → hashes later (D1 still holds as the target).
**Consequence.** Builds reference slugs; the manifest join adds hashes without
changing build files.

## D14 — Two extra kernel modules: `Phrasing` and `Causality`

**Context.** Explanation, trace and graph renderers all need the same English for
triggers and outcomes; Simulation and LoopGraphing need the same trigger-matching
semantics. Slices may not depend sideways. **Decision.** Put both in the kernel as
small non-Domain modules, as the conventions allow (like `Extraction`/`Documents`
in the sibling project), guarded by architecture tests. **Consequence.** "What a
trigger means" and "how we say it" have exactly one home.

## D15 — Deterministic chance

**Open question answered:** rules with "chance to / occasionally / rapidly" fire in v1
and are marked *(chance)* in every view. Probabilistic simulation would need seeds and
distributions we don't have; marking them keeps the loop honest without hiding it.

## D16 — Target model: "the pack in front of you"

**Open question answered:** one abstract target with a tier and the debuffs spread
across the pack. Debuffs stay after a kill until they expire (`wait`). This matches
how add-clear builds are explained ("everything is jolted"); single-target nuance is
a later refinement.

## D17 — Cascade termination is per causal chain

Depth ≤ 5, and a rule never re-fires on an identical event *up its own causal chain*.
A global "once per step" guard (the proposal's first idea) wrongly suppressed sibling
occurrences — two orbs picked up fired the pickup rule once.

## D18 — Unknown energy is not applied; unknown chunk scalar is assumed 1× and flagged

*Superseded by D21: ability energy is no longer simulated, so chunk scalars aren't used.*
An unknown amount stays "?" and doesn't change the state. An unknown chunk energy
scalar is assumed 1× (`Certainty.Assumed`, marked `*`), because the amount itself is
known and the scalar only rescales it — the trace says so until the Compendium
snapshot provides the real scalar.

## D19 — CLI host first

The proposal's API host comes later; shells return `Effect`s that any host executes,
so the CLI and the API share every line of logic. The CLI is the fastest way to check
the engine against real build notes.

## D20 — Verify.XunitV3 pinned to 32.0.1

33.x requires xunit.v3 4.x and gates the build on a SponsorCheck licence property —
a licensing decision for the owner. 32.0.1 predates SponsorCheck and works with
xunit.v3 3.2.2. Revisit when the owner decides.

## D21 — No ability-energy model (for now)

**Context.** Ability energy in game is mainly time: base cooldowns scaled by stats, with rule
refunds on top. The step-based engine (D12) has no real clock, most cooldowns and chunk
scalars are still `?`, and a partial model (refunds only, no recharge) made loops look
*less* sustainable than they are and blocked steps that are fine in game ("melee-first
breaks in cycle 2"). **Decision (product owner).** Don't track ability energy and never
restrict a trigger: grenade, melee, super and class ability can always be used.
`grantEnergy`, `resetCooldown` and the energy part of `convertStacksToEnergy` stay in the
rules and the traces *as explanation* ("+12% grenade energy [Bomber]") — applied with their
certainty, recorded as an `EnergyRefund`, changing no state (`convertStacksToEnergy` still
consumes its stacks). Chunk scalars, base cooldowns and `extraCharges` are parsed and kept
but not used. **Consequence.** A loop is "repeatable" when its steps can be played back to
back (only a missing pickup or an empty weapon slot breaks a cycle); the analysis shows the
**energy refunded per cycle** per ability (known amounts summed, `?` refunds counted) so
the player judges whether that covers the abilities the loop uses. Revisit with the
Compendium's cooldowns and a time model.

## D22 — Player-declared target counts

**Context.** The engine can't know how many enemies a grenade or a burst of fire catches,
yet perks depend on it ("hitting three separate targets…" — One For All, multi-kill perks).
**Decision.** The player says it: an ability or weapon action carries a `TargetCount`
(Vogen, 1..20, validated at the boundary; tokens `grenade:kill:3`, `kinetic:hit:5`, default
1). An action against N enemies emits N per-enemy `Damaged` events, then N `Killed` for a
kill — each cascading fully, so later hits see the debuffs earlier ones applied (D16's pack)
— then one `TargetsHit` event per action for the new triggers `damage/kill … atLeast: N`
(`Trigger.DamageMultiple` / `KillMultiple`, each with only its own data). **Consequence.**
Kills per cycle count every target; chance-based perks still fire on every enemy (D15,
marked *(chance)*), so counts stay an upper bound. Strikes and summons hit one enemy.
