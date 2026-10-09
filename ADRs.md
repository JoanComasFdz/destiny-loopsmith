# Architecture decision records

Each ADR explains **why**; [`CONVENTIONS.md`](./CONVENTIONS.md) says **what** and wins
on any conflict. D1–D12 come from the design proposal
([`docs/design/loopsmith-design-v0.3.html`](docs/design/loopsmith-design-v0.3.html), §04);
D13+ were decided while building the prototype (the CLI first, then the web designer).

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
| D12 | Step-based time in v1; builds pin `CatalogVersion` | Matches how players think; patches don't silently change saved builds (time: superseded by D28 — no time at all) |

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

*Superseded by D21: ability energy is no longer simulated, so chunk scalars aren't used
(`Certainty.Assumed` no longer exists).*
An unknown amount stays "?" and doesn't change the state. An unknown chunk energy
scalar is assumed 1× (`Certainty.Assumed`, marked `*`), because the amount itself is
known and the scalar only rescales it — the trace says so until the Compendium
snapshot provides the real scalar.

## D19 — CLI host first

The proposal's API host comes later; shells return `Effect`s that any host executes,
so the CLI and the API share every line of logic. The CLI is the fastest way to check
the engine against real build notes. *Since D25 the main UI is the web designer; the CLI stays
the scripting and golden-test host.*

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
certainty and caveat ("amount unknown", "×2 stacks"), changing no state
(`convertStacksToEnergy` still consumes its stacks). Chunk scalars, base cooldowns and
`extraCharges` are parsed and kept but not used. A later decision of the owner went further:
exact energy, refund and cooldown totals aren't wanted at all — the build crafter needs to see
what works together and what is wasted (D23) — so a loop no longer adds up what its rules
refund. **Consequence.** A loop is "repeatable" when its steps can be played back to
back (only a missing pickup or an empty weapon slot breaks a cycle); the analysis reports
outcomes, what fired, what was wasted and buff uptime, with no energy figures. Revisit only if
the owner asks for a time model (the Compendium's cooldowns would feed it).

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

## D23 — Rules that don't stack give way and are reported as wasted

**Context.** The Compendium (Arc#51) says Tempest Strike's x1 Bolt Charge on a jolted kill
doesn't stack with Dielectric's. v1 fired both, so the Skip Grenade loops gained one Bolt
Charge too many per jolted kill (6 maxes per cycle instead of 3). Beyond the numbers, the
build crafter needs to see wasted potential: a slot spent on a grant the game throws away.
**Decision.** A rule-level `doesNotStackWith: [<element-id>, …]`, declared on the side that
gives nothing. When a rule of a listed element fires on the same event, the declaring rule
gives way: it is reported as fired with no outcomes, naming that element
(`FiredRule.NotStackedWith`; the trace reads "doesn't stack with Dielectric [Tempest Strike]").
With both elements equipped the build check warns ("… with both equipped, it is wasted") and
`explain` annotates the bullet; a loop report lists **Wasted per cycle** per (element,
partner), counted neither as fired nor as a chance bullet, and a comparison judges fewer as
better. Parsing rejects an id that isn't an element of the rules, the rule's own element, an
empty list, and a `doesNotStackWith` that leads back to its own element, directly or through a
longer circle (none of those rules would apply). **Consequence.**
The granularity is the whole rule, not one outcome: an outcome that does stack needs a rule
of its own. Only rules matching the same event interact, and a rule gives way when any listed
element's rule matched that event, even if that rule itself gave way — so in a chain where A
lists B and B lists C, only C applies (a circle, which would apply none of them, is rejected
when parsing). Without the listed element equipped, the rule applies as usual. The loop graph shows
it too: where the rule and the one it gives way to lead from the same trigger to the same node, both
arrows go into a "Doesn't stack" node and only the partner's arrow leaves it
(`Kill Jolted target →[Tempest Strike, Dielectric] Doesn't stack →[Dielectric] Gain Bolt Charge`). The
graph is static, so it pairs only rules on the same trigger — an approximation of the same-event rule —
and the node is not a step (loop length, "N steps" and loop order ignore it).

## D24 — The designed loop is the product

**Context.** The proposal's product was a simulation API (FR-3, FR-4): a step or a sequence in, a
trace out, nothing kept. **Decision.** The loop a player designs, trigger by trigger, is
first-class data: a self-contained `*.loop.yaml` that embeds its build file's text, so it replays
anywhere that has the rule catalog, travels as a file or a share link, runs back to back into a
`LoopReport` and compares with another loop (`LoopComparison`). `Orchestration.LoopDesigning` is
the API behind both hosts (the web calls it directly, the CLI through its shells). **Consequence.**
Two slices, `LoopFiles` and `ReportComparison`; a loop designed against another catalog version
replays with an Info note, not an error. Format and metrics: [docs/loop-format.md](docs/loop-format.md).

## D25 — The web designer runs in the browser, as static files

**Context.** The designer needs the same engine as the CLI and should be shareable with a link.
**Decision.** `Loopsmith.Web` is a Blazor WebAssembly host: the C# core runs in the browser
(rules, builds and example loops are embedded), so there is no server and any static host works —
GitHub Pages, with a preview per pull request ([docs/hosting.md](docs/hosting.md)). Like the CLI
it owns no game logic. Shared loops travel in the URL fragment (`#loop=…`). The app stores nothing
in the browser (owner decision, for now): a loop is kept by exporting it or copying its link.
**Consequence.** No API host or database yet (D19); a saved library could come back later
(per-browser storage, or a backend to sync it — [docs/backlog.md](docs/backlog.md)). The first
visit downloads the .NET runtime (≈ 3 MB, then cached).

## D26 — Airborne class ability use (`class:air`)

**Context.** Ascension (Compendium Arc#48), the creator's alternative to Flow State, is an air move
that spends the class ability charge: it jolts nearby enemies, makes you Amplified and sets off the
equipped class ability's effects. v1 had no air-move trigger, so Ascension fired on every dodge,
ground ones included. **Decision.** The class ability action carries an airborne flag: token
`class:air` (`UseClassAbility(Airborne: true)`), event `AbilityCast(ClassAbility, Airborne: true)`.
It is still a class ability cast — the charge is spent — so every `abilityCast: classAbility` rule
fires on it too (the dodge's own effects per the Compendium; the class-ability mods and perks by the
same reading). The trigger `{ abilityCast: { ability: classAbility, airborne: true } }` fires only on
the airborne use; `airborne` on a grenade, melee or super is a parse error, since there is no airborne
action for them. `class:air` always parses (CLI, loop files, share links), but `play` and the web
palette offer it only when an equipped rule has an airborne trigger: without one it fires exactly what
`class` fires, and a second identical choice would only clutter the menu. **Consequence.** Ascension's
rule uses the airborne trigger, and any other airborne class-ability move is the same flag; the graph's
"Use class ability" action reaches both triggers. Airborne grenade and melee actions (Ballistic Slam)
and slide qualifiers (Tempest Strike) are not expressible yet ([docs/backlog.md](docs/backlog.md)).

## D27 — A buff armed afresh replaces its stacks (`restart`)

**Context.** Slice counts the next 5 hits after each class ability use (Compendium Weapon Perks#198),
but its dodge rule added a stack, so dodges alone stacked Slice to 5 and ended it ("Melee first"
reported "Slice maxed"). **Decision.** `applyBuff` takes `restart: true`: the stacks given replace the
active ones and the duration restarts as usual; a restart that doesn't raise the stacks derives no
`BuffGained`, and the trace reads "Slice ×1 (8s, restarts)" with the caveat "restarted (was ×N)". It is
a flag on the outcome, not on the glossary status, because the same buff is armed afresh by one rule
(the dodge) and stacked by another (each sever). **Consequence.** "Melee first" no longer maxes Slice;
the creator's loop is unchanged. A buff armed at 0 stacks is still a gap (Slice severs 4 times per
window instead of 5, [docs/backlog.md](docs/backlog.md)).

## D28 — Loopsmith explains cause and effect; it doesn't simulate quantities or time

**Context.** Combat has too many variables to calculate: how many enemies and which, where they
stand, shields, whether the player hits, whether a grenade lands — and almost everything in the
sandbox runs on time (cooldowns, durations, decay). Exact numbers are impossible, yet the engine
had started to count anyway: stacks up to a maximum (Bolt Charge reaching x10 fires New Tricks by
itself), buff durations and a clock (`wait`), buff uptime, "Bolt Charge maxed per cycle", "kills per
cycle", Slice's re-arming arithmetic (D27). **Decision (product owner).** Loopsmith is not a
simulator. It explains cause and effect: for each trigger, every outcome it fires, which element
caused it, what that unlocks next, and what doesn't work together (wasted potential, D23). Numbers
from the sources are **facts shown with the outcome** ("+12% grenade energy", "Amplified (15 s)",
"up to ×3", "reduces the cooldown by 2 s"); the engine never accumulates them or decides anything
from them. The engine never decides **when** a threshold is reached — stacks at 3 or at max, a buff
running out, energy full, a cooldown over. The **player declares** such states in the loop ("repeat
until Combination Blow ×3, then punch the big enemy"; "Combination Blow ×3 → One-Two Punch shotgun →
melee"; "Bolt Charge at max"), and the engine shows what the declared state sets off. Boolean causal
state stays (a buff is active, the target is jolted, an orb is on the ground); it ends when a rule
consumes it or the player declares it, never by time. Counts the player states about one action stay
(D22: "this grenade kills 3"). **Consequence.** The parts of the engine, the analysis and the UI that
count or time things are reworked into causal information or player declarations — the inventory is
in [docs/backlog.md](docs/backlog.md) ("Not a simulator"). As that lands, it supersedes D12's step
time, the uptime and per-cycle counts of D21/D23's reports and D27's restart arithmetic. Rule-format
gaps that are only about counting or timing (several hits per enemy, a buff armed at 0 stacks, rule
cooldowns, buff decay…) are non-goals, not gaps.
