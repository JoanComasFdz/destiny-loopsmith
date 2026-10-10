# Architecture decision records

Each ADR explains **why**; [`CONVENTIONS.md`](./CONVENTIONS.md) says **what** and wins on any conflict.
The requirements they serve are in [docs/requirements.md](docs/requirements.md). The list is the
current set of decisions, in four groups: what the product is (D1–D9), sources and rules (D10–D15),
architecture (D16–D23), hosts and tooling (D24–D26). D1 is the founding one: every other decision
follows from it.

| # | Decision | Why |
|---|---|---|
| D1 | **Loopsmith describes cause and effect; it doesn't simulate the game** | Combat has too many variables and runs on time; exact numbers are impossible, and the build crafter needs to know what triggers what |
| D2 | The designed loop — an ordered list of triggers — is the product | A build is played as a loop; the order of its triggers is what the player designs, saves, shares and compares |
| D3 | The player declares thresholds and endings (`max:`, `end:`) | The engine can't know when Bolt Charge reaches x10 or a buff ends; the player can |
| D4 | The player declares how many enemies an action hits | The engine can't know how many enemies a grenade catches; perks depend on it |
| D5 | Abilities are always available; energy outcomes are facts | Energy is time; tracking it would be a simulation |
| D6 | Rules that don't stack give way and show as wasted | Wasted potential is what the build crafter most needs to see |
| D7 | One target: the pack in front of you, until the player declares a new one | Matches how add-clear builds are explained ("everything is jolted"); only play decides when the next group comes |
| D8 | Chance rules always fire, marked *(chance)* | No probabilities without a simulation; the mark keeps the loop honest |
| D9 | An airborne class ability use (`class:air`) | Air moves (Ascension) spend the class ability charge but only some rules react to them |
| D10 | Causality is authored as YAML; a human confirms every rule | 55% of Compendium descriptions state the trigger, so drafts can be generated, never trusted |
| D11 | The Compendium is the primary mechanics source; Clarity the cross-check | The Compendium covers abilities, artifact perks and statuses; Clarity has hashes |
| D12 | The Compendium is joined by name, scoped by kind + class, alias table in git | It has no hashes; unresolved names are reported, never guessed |
| D13 | Every number is a `GameValue` with `Provenance`; unknown stays `?` | ≈29% of descriptions contain a "?"; unknown ≠ 0; every number traceable |
| D14 | Authored slugs as ids, the manifest hash as the long-term identity | The manifest isn't reachable yet; slugs keep build files stable when hashes arrive |
| D15 | Builds and loops pin a catalog version | A rules change never silently changes a saved build or loop |
| D16 | Vertical slices as folders, guarded by architecture tests | One change = one slice; boundaries can't silently rot |
| D17 | Strict impure → pure → impure; effects as data | The pure core is deterministic, replayable, testable without infrastructure |
| D18 | Closed vocabulary as Dunet unions; behaviour in static functions; no interfaces | Exhaustive `Match`; the compiler lists every place to update |
| D19 | Typestate: the engine accepts only a `ValidatedBuild` | Playing an unvalidated build doesn't compile |
| D20 | Vogen value objects validated at the boundary | Inside the core every value is known-valid |
| D21 | Railway (ROP) for parsing, validating and playing; `Severity` only where the app branches | Short-circuit on the first blocking error, no app-state object |
| D22 | Two kernel modules besides `Domain`: `Phrasing` and `Causality` | "How we say it" and "what a trigger means" each have one home |
| D23 | Cascade termination is per causal chain | A global "once per step" guard would suppress sibling events |
| D24 | Hosts own no logic; the CLI is the scripting and golden-test host | Both hosts share every line of logic through `Orchestration` |
| D25 | The web designer runs in the browser, as static files | No server: any static host works, and a loop travels in its link |
| D26 | Verify.XunitV3 pinned to 32.0.1 | 33.x needs a licence decision |

## What the product is

### D1 — Loopsmith describes cause and effect; it doesn't simulate the game

**Context.** Build guides explain a build as triggers and their effects: "dodge → Reaper → the
next grenade kill drops an orb". Players re-derive this by hand; no tool shows what happens when a
build is played. Combat itself can't be calculated: how many enemies and which, where they stand,
shields, whether the player hits, whether a grenade lands — and almost everything in the sandbox
runs on time (cooldowns, durations, decay). **Decision (product owner).** Loopsmith describes
**cause and effect** and never simulates. For each trigger it shows every outcome that fires, which
element caused it, what that unlocks next, and what doesn't work together (D6). It never answers
how many, how often, how long or when.

* **Numbers are facts shown with their outcome** ("+12% grenade energy", "Amplified (15s)",
  "up to x10", "refills melee"). They are never added up or compared to
  decide what happens.
* **State is what is present**: a buff on you, a debuff on the pack, a pickup on the ground — and
  what the player declared (D3). Nothing in it is counted: no stacks, energy or health.
* **The player declares what only play can decide**: that a threshold is reached ("Bolt Charge at
  max", "Combination Blow at max"), that a buff has ended (D3), how many enemies one action hits (D4).
  The engine shows what the declared state sets off.
* A loop is judged by its **order of triggers** (D2): whether each step gets what it needs from the
  steps before it, and whether the order can be repeated.

**Consequence.** Abilities are always available (D5); a status ends when
a rule consumes or removes it, or when the player declares it ended. A source's "after 6 hits" or "for 10 s" is a fact in the
description, never a mechanism. No damage or health model either: damage and healing outcomes are
facts too.

### D2 — The designed loop is the product

**Context.** A player builds a loop by choosing triggers one after another ("dodge, grenade,
dodge, shoot with Slice") and then repeats them to keep the build up. **Decision.** The loop is
first-class data: an ordered list of steps (triggers and declared states, D3), saved as a
self-contained `*.loop.yaml` that embeds its build file's text, so it replays anywhere that has the
rule catalog and travels as a file or a share link. Its analysis follows that order: played from a
fresh spawn and then repeated from where it ended, it says whether the order **repeats** or where it
**breaks** (a step that can't happen: nothing to pick up, an empty weapon slot, a declared state
that doesn't hold), and for each step what it needs and which earlier step provided it, what it sets
off and what is wasted there. Comparing two loops compares their orders — "dodge → grenade → pick up
orb" against "grenade → dodge → pick up orb" — trigger by trigger: what each sets off in its place
and why it differs. `Orchestration.LoopDesigning` is the API behind both hosts. **Consequence.**
Two slices, `LoopFiles` and `ReportComparison`; the analysis and the comparison report, step by
step, what each step needs and sets off. A loop designed against another catalog version replays with an Info note
(D15). Format, analysis and comparison: [docs/loop-format.md](docs/loop-format.md).

### D3 — The player declares thresholds and endings

**Context.** Many rules react to a threshold: Shinobu's Vow and Flashover at x10 Bolt Charge,
Slice after its 5 hits, Combination Blow at x3. When it is reached depends on play (D1). The game
shows these counts as a stacking status (`maxStacks` in the glossary), so the player can see and
declare them; a counter the game doesn't show (To Shreds' 6 weapon hits) is a *(chance)* rule (D8).
**Decision.** A loop step can declare a state:

* `max:<status>` — the buff is at its maximum ("Bolt Charge at max"). It raises `StacksMaxed`, so the
  `stacksMaxed` rules fire, and the buff stays declared at max (the `atMax` condition reads it) until a
  rule consumes or removes it, or an `end:`.
* `end:<status>` — the buff on you or the debuff on the pack has ended ("Amplified ends").
* `pack:new` — the next enemies are a new pack, with none of the old pack's debuffs ("New pack", D7).

A declaration that doesn't hold is a **blocked step**, like a pickup that isn't on the ground:
`max:` needs an active buff that stacks (`maxStacks` in the glossary) and isn't declared at max yet,
`end:` an active status; `pack:new` always holds. Nothing else reaches a maximum: an `applyBuff` makes the buff present and
shows its grant ("+1 Bolt Charge"). A status ends only through a rule's `removeBuff` or consumption,
or an `end:`. **Consequence.** `stacksMaxed` fires only on a declaration;
a rule that reacts to the next hit at the maximum uses the `atMax` condition (Bolt Charge discharges
on the next ability hit at x10). The cap and the duration are facts the player reads when deciding
to declare. Format: [docs/loop-format.md](docs/loop-format.md#action-tokens)
and [docs/rule-format.md](docs/rule-format.md).

### D4 — The player declares how many enemies an action hits

**Context.** The engine can't know how many enemies a grenade or a burst of fire catches, yet perks
depend on it ("hitting three separate targets…" — One For All, multi-kill perks). **Decision.** An
ability or weapon action carries a `TargetCount` (Vogen, 1..20, validated at the boundary; tokens
`grenade:kill:3`, `kinetic:hit:5`, default 1). An action against N enemies emits N per-enemy
`Damaged` events, then N `Killed` for a kill — each cascading fully, so later hits see the debuffs
earlier ones applied (D7) — then one `TargetsHit` event per action for the triggers
`damage/kill … atLeast: N` (`Trigger.DamageMultiple` / `KillMultiple`). **Consequence.** The count is
a statement about one action, never a total. Strikes and summons hit one enemy. Chance rules fire on
every enemy (D8).

### D5 — Abilities are always available; energy outcomes are facts

**Context.** Ability energy is mainly time: base cooldowns scaled by stats, with refunds on top.
Tracking it is a simulation (D1). **Decision (product owner).** Grenade, melee, super and class
ability can always be used; no step is blocked for lack of energy. `grantEnergy`, `resetCooldown`
and the energy of `convertStacksToEnergy` are shown with their value and certainty ("+12% grenade
energy [Bomber]") and change nothing. **Consequence.** The cause-and-effect graph still finds
loops that give energy back ("ability loop — gives grenade energy back"), as causality, never as an
amount. The Compendium's base cooldowns, chunk scalars and `extraCharges` are recorded in the rules
as facts.

### D6 — Rules that don't stack give way and show as wasted

**Context.** The Compendium (Arc#51) says Tempest Strike's x1 Bolt Charge on a jolted kill doesn't
stack with Dielectric's. The build crafter needs to see this: a slot spent on a grant the game throws
away is wasted potential. **Decision.** A rule-level `doesNotStackWith: [<element-id>, …]`, declared
on the side that gives nothing. When a rule of a listed element fires on the same event, the
declaring rule gives way: it is reported as fired with no outcomes, naming that element
(`FiredRule.NotStackedWith`; the trace reads "doesn't stack with Dielectric [Tempest Strike]"). With
both elements equipped the build check warns ("… with both equipped, it is wasted"), `explain`
annotates the bullet, a loop's analysis shows it at each step where it happens, and the loop graph
routes both arrows through a "Doesn't stack" node. Parsing rejects an id that isn't an element of
the rules, the rule's own element, an empty list, and a `doesNotStackWith` that leads back to its
own element, directly or through a longer circle (none of those rules would apply).
**Consequence.** The granularity is the whole rule: an outcome that does stack needs a rule of its
own. Only rules matching the same event interact, and a rule gives way when any listed element's rule
matched that event, even if that rule itself gave way. The graph is static, so it pairs only rules on
the same trigger — an approximation of the same-event rule — and its node is not a step.

### D7 — One target: the pack in front of you, until the player declares a new one

**Decision.** One abstract target, the enemies in front of you, with a tier and the debuffs
spread across it. A debuff stays on the pack until the player declares it ended (D3); after a kill
the pack is still in front of you. When the next group of enemies comes is play, so the player
declares it: a `pack:new` step (D3) puts a new pack in front of you, with none of the old one's
debuffs; the buffs on you and the pickups on the ground stay. A loop that meets a new group each pass
starts with `pack:new`; a loop against one boss leaves it out. **Consequence.** It matches how add-clear
builds are explained ("everything is jolted"), and a debuff the next group doesn't have isn't
credited to the next pass's first hit. The tier is
`minor`: a kill rule for a higher tier shows in `explain` and the graph but doesn't fire in a loop
until the player can declare the tier ([docs/backlog.md](docs/backlog.md)).

### D8 — Chance rules always fire, marked *(chance)*

**Decision.** Rules with "chance to / occasionally / rapidly" fire and are marked *(chance)* in
every view, and so are rules that depend on progress the game doesn't show as a stacking status
("after 6 weapon hits"; D3).
Probabilities would need a simulation (D1); marking them keeps the loop honest without hiding it.

### D9 — An airborne class ability use (`class:air`)

**Context.** Ascension (Compendium Arc#48) is an air move that spends the class ability charge: it
jolts nearby enemies, makes you Amplified and sets off the equipped class ability's effects.
**Decision.** The class ability action carries an airborne flag: token `class:air`
(`UseClassAbility(Airborne: true)`), event `AbilityCast(ClassAbility, Airborne: true)`. It is still a
class ability cast — the charge is spent — so every `abilityCast: classAbility` rule fires on it too.
The trigger `{ abilityCast: { ability: classAbility, airborne: true } }` fires only on the airborne
use; `airborne` on a grenade, melee or super is a parse error. `class:air` always parses, but `play`
and the web palette offer it only when an equipped rule has an airborne trigger: without one it fires
exactly what `class` fires. **Consequence.** Any other airborne class-ability move is the same flag.
Airborne grenade and melee actions and slide qualifiers are open ([docs/backlog.md](docs/backlog.md)).

## Sources and rules

### D10 — Causality is authored as YAML

**Decision.** Every build element is an element with rules (`on` trigger → `then` outcomes, plus
always-on passives), authored in `rules/**/*.yaml` with provenance. Edges between elements are never
stored: they appear when an outcome matches another rule's trigger. Drafts generated from the
Compendium's "On X:" phrasing will be offered for a human to confirm, never loaded as rules.
**Consequence.** A new element is one YAML entry, never code (CONVENTIONS.md). Format:
[docs/rule-format.md](docs/rule-format.md).

### D11 — The Compendium is the primary mechanics source; Clarity the cross-check

**Decision.** Game data comes first: the Destiny Data Compendium (abilities, aspects, fragments,
artifact perks, statuses, mods) is primary, Clarity (hash-keyed descriptions) the cross-check; the
user's notes come next and a creator's claim last. Every disagreement is recorded in the build's
`discrepancies.md`. **Consequence.** Snapshots stay private (licensing); rules cite rows as
`compendium/<date>/<Tab>#<row>` and paraphrase.

### D12 — The Compendium is joined by name, scoped by kind and class

**Decision.** The Compendium has no hashes, so its entries are joined to the manifest by name,
scoped by kind and class, with an alias table in git. Unresolved names are reported, never guessed.

### D13 — Every number is a `GameValue` with `Provenance`; unknown stays `?`

**Decision.** A number in the rules is `Known`, `PerModCount` (value by copies equipped),
`Approximate` (`~`) or `Unknown` (`?`); every element keeps where its numbers came from
(`Provenance`, its `source:`: Compendium row, Clarity hash, creator claim, or the rule file's line). **Consequence.** An unknown is shown as "?" and
never treated as 0; since numbers are facts (D1), "?" only means the source doesn't say.

### D14 — Authored slugs as ids, the manifest hash as the long-term identity

**Context.** The Bungie manifest (and its API key) isn't reachable yet, and abilities, artifact perks
and armor set bonuses have no Clarity hash. **Decision.** Elements are keyed by a kebab-case
`ElementId` (`shinobus-vow`); `Hash` is `Optional<ItemHash>`, filled from Clarity where known. The
manifest hash is the long-term identity (stable, shared by Clarity and DIM). **Consequence.** Builds
reference slugs; the manifest join adds hashes without changing build files.

### D15 — Builds and loops pin a catalog version

**Decision.** The catalog version is a hash of the rule files (`authored-<12 hex>`). A build may pin
it (`catalog:`), and a loop records the version it was designed against. Replaying with another
version is allowed and reported as Info. **Consequence.** A rules change never silently changes what
a saved build or loop means.

## Architecture

### D16 — Vertical slices as folders, guarded by architecture tests

One project, slices are folders; feature slices depend only on the kernel, and only `Orchestration`
sees every slice. Architecture tests enforce the boundaries in CI, so one change touches one slice
and boundaries can't silently rot.

### D17 — Strict impure → pure → impure; effects as data

Read first, compute, write last. Side effects are described as data (`Effect`) and executed by the
host. The pure core is deterministic — same build, state and step give the same output — replayable
and testable without infrastructure (golden tests from real build notes).

### D18 — Closed vocabulary as Dunet unions; behaviour in static functions

Triggers, outcomes, conditions, actions and events are Dunet unions with exactly their own data;
behaviour is static functions that pattern-match. No interfaces. The compiler lists every `Match`
to update when a case is added.

### D19 — Typestate: the engine accepts only a `ValidatedBuild`

Only `BuildComposition` constructs a `ValidatedBuild`, and the engine takes nothing else, so
playing an unvalidated build doesn't compile (and the architecture test catches a stray constructor).

### D20 — Vogen value objects validated at the boundary

Ids and quantities (`ElementId`, `StatusId`, `TargetCount`, `StackCount`, `Seconds`…) are Vogen
value objects, validated where text becomes data (the parsing slices); inside the core every value is
known-valid.

### D21 — Railway for parsing, validating and playing; `Severity` only where the app branches

Parsing, validation and playing return `Result` and short-circuit on the first blocking error, with
no app-state object. `Severity` (`Blocking | Warning | Info`) exists because the app branches on it;
IO whose only outcome is "tell the user" is `Result<_, string>`.

### D22 — Two kernel modules besides `Domain`: `Phrasing` and `Causality`

**Context.** Explanation, trace and graph renderers and the web designer need the same English for
triggers and outcomes; the engine and the loop graph need the same trigger-matching semantics; slices
may not depend sideways. **Decision.** Both live in the kernel as small non-`Domain` modules, guarded
by architecture tests. **Consequence.** "What a trigger means" and "how we say it" each have exactly
one home.

### D23 — Cascade termination is per causal chain

Cascade depth ≤ 5, and a rule never re-fires on an identical event *up its own causal chain*. A
global "once per step" guard would suppress sibling events (two orbs picked up would fire the
pickup rule once).

## Hosts and tooling

### D24 — Hosts own no logic; the CLI is the scripting and golden-test host

Shells in `Orchestration` return `Effect`s that any host executes, so every host shares every line
of logic. The web designer (D25) is the main UI; the CLI (`loopsmith`) is for scripting, quick checks
of the engine against real build notes, and the golden tests, which snapshot the text it prints.

### D25 — The web designer runs in the browser, as static files

**Decision.** `Loopsmith.Web` is a Blazor WebAssembly host: the C# core runs in the browser (rules,
builds and example loops are embedded), so there is no server and any static host works — GitHub
Pages, with a preview per pull request ([docs/hosting.md](docs/hosting.md)). Shared loops travel in
the URL fragment (`#loop=…`). The app stores nothing in the browser (owner decision): a loop
is kept by exporting it or copying its link. **Consequence.** No API or database; a saved library is
open ([docs/backlog.md](docs/backlog.md)). The first visit downloads the .NET runtime (≈ 3 MB, then
cached).

### D26 — Verify.XunitV3 pinned to 32.0.1

33.x requires xunit.v3 4.x and gates the build on a SponsorCheck licence property — a licensing
decision for the owner. 32.0.1 predates SponsorCheck and works with xunit.v3 3.2.2.
