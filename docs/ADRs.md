# Architecture decision records

Each ADR explains **why**; [`docs/CODING_RULES.md`](CODING_RULES.md) says **what** and wins on any conflict.
The requirements they serve are in [docs/requirements.md](requirements.md). The list is the
current set of decisions, in four groups: what the product is (D1–D9), sources and rules (D10–D15),
architecture (D16–D23), hosts and tooling (D24–D26), starting from a DIM link (D27), the build's stats (D28), the
step's damage resistance (D29), armor mods in their sockets (D30), the artifact (D32), the count of Armor Charge
(D33), what a hit step stands for (D34), the raid and activity mods left out of the sockets (D35), what the player
says about a step besides its hit and count (D36), a reload (D37) and the web designer as the only host (D38). D1 is the
founding one: every other decision follows from it.

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
| D9 | Secondary ability uses (`melee:slide`, `class:air`, `grenade:hold`) | An aspect makes a slide, an air move or a hold a different trigger from the ability's press, but every rule on the ability still fires for it |
| D10 | Causality is authored as YAML; a human confirms every rule | 55% of Compendium descriptions state the trigger, so drafts can be generated, never trusted |
| D11 | The Compendium is the primary mechanics source; Clarity the cross-check | The Compendium covers abilities, artifact perks and statuses; Clarity has hashes |
| D12 | The Compendium is joined by name, scoped by kind + class, alias table in git | It has no hashes; unresolved names are reported, never guessed |
| D13 | Every number is a `GameValue` with `Provenance`; unknown stays `?` | ≈29% of descriptions contain a "?"; unknown ≠ 0; every number traceable |
| D14 | Authored slugs as ids, the manifest hash as the long-term identity; a generated excerpt, and the full manifest in the browser for the rest | Slugs keep build files stable; the excerpt makes the bundled builds work offline |
| D15 | Builds and loops pin a catalog version | A rules change never silently changes a saved build or loop |
| D16 | Vertical slices as folders, guarded by architecture tests | One change = one slice; boundaries can't silently rot |
| D17 | Strict impure → pure → impure; effects as data | The pure core is deterministic, replayable, testable without infrastructure |
| D18 | Closed vocabulary as Dunet unions; behaviour in static functions; no interfaces | Exhaustive `Match`; the compiler lists every place to update |
| D19 | Typestate: the engine accepts only a `ValidatedBuild` | Playing an unvalidated build doesn't compile |
| D20 | Vogen value objects validated at the boundary | Inside the core every value is known-valid |
| D21 | Railway (ROP) for parsing, validating and playing; `Severity` only where the app branches | Short-circuit on the first blocking error, no app-state object |
| D22 | Two kernel modules besides `Domain`: `Phrasing` and `Causality` | "How we say it" and "what a trigger means" each have one home |
| D23 | Cascade termination is per causal chain | A global "once per step" guard would suppress sibling events |
| D24 | ~~Hosts own no logic; the CLI is the scripting and golden-test host~~ Superseded by D38 | Both hosts shared every line of logic through `Orchestration` |
| D25 | The web designer runs in the browser, as static files | No server: any static host works, and a loop travels in its link; only Bungie's manifest is saved in the browser |
| D26 | Verify.XunitV3 pinned to 32.0.1 | 33.x needs a licence decision |
| D27 | A DIM link starts a build of what the catalog knows by hash; the rest is left out, listed | A player's build already lives in DIM; nothing is guessed, and what's missing stays visible |
| D29 | The step's state shows its damage resistance, its sources multiplied, PvE and PvP | Builds are judged by how much damage they shrug off; the game stacks resistance by a known rule, so the total is a fact, never a decision |
| D30 | Armor mods are picked in the pieces' sockets; the energy budget holds when its capacity is known | A build's mods are part of its loop and the game decides what fits; the sockets and costs are Bungie's, the capacity the player's copy's |
| D32 | A build names its artifact and the perk in each of its slots (Artifacts 2.0) | Since Monument of Triumph one of seven artifacts is equipped with up to seven perks, one per slot; the tiers and slots are Bungie's, read from the artifact's sockets |
| D33 | Armor Charge is counted: sure stacks and chance stacks, and the count decides | Gaining, keeping and spending Armor Charge is a pillar of build crafting; most of its gains and spends are known steps of the loop, so the loop can say how many it surely holds |
| D35 | Raid, activity and seasonal armor mods aren't supported: a DIM link's are kept but ignored, and no picker offers them | They act on an encounter's mechanics (a relic, a plate, an enemy race), which a loop of triggers doesn't hold, or no longer work at all; what a loadout had stays visible |
| D36 | The player says when a weapon step breaks a shield and when an ability step spends its last charge | Shield Break Charge and the Kickstarts react to them; play decides both, and neither is counted |
| D37 | A reload is a step the player declares, and a rule can reload a weapon; no ammo is counted | Demolitionist's loop needs a reload and the perks that react to one; a magazine is time and ammo, which only play decides |
| D38 | The web designer is the only host; the CLI and the loop graph are removed | Nobody used the CLI: the web app, recorded on every PR, is how Loopsmith is iterated on; what the CLI checked is a Core test |

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
  what the player declared (D3). Nothing in it is counted: no stacks, energy or health — but a counted buff's
  stacks (Armor Charge, D33).
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
(D15). Format, analysis and comparison: [docs/loop-format.md](loop-format.md).

### D3 — The player declares thresholds and endings

**Context.** Many rules react to a threshold: Shinobu's Vow and Flashover at x10 Bolt Charge,
Slice after its 5 hits, Combination Blow at x3. When it is reached depends on play (D1). The game
shows these counts as a stacking status (`maxStacks` in the glossary), so the player can see and
declare them; a counter the game doesn't show is a *(chance)* rule (D8), except a count of hits or a share of the
enemy's health (To Shreds' 6 weapon hits), which a hit step stands for (D34).
**Decision.** A loop step can declare a state:

* `max:<status>` — the buff is at its maximum ("Bolt Charge at max"). It raises `StacksMaxed`, so the
  `stacksMaxed` rules fire, and the buff stays declared at max (the `atMax` condition reads it) until a
  rule consumes or removes it, or an `end:`.
* `end:<status>` — the buff on you or the debuff on the pack has ended ("Amplified ends").
* `pack:new` — the next enemies are a new pack, with none of the old pack's debuffs ("New pack", D7).
* `health:critical` — you reached critical health ("Critical health"): no damage you take is modelled, so the player
  says when the shields broke. It raises `CriticalHealth`, so the `criticalHealth` rules fire (Emergency
  Reinforcement), and leaves nothing in the state.

A declaration that doesn't hold is a **blocked step**, like a pickup that isn't on the ground:
`max:` needs an active buff that stacks (`maxStacks` in the glossary) and isn't declared at max yet,
`end:` an active status; `pack:new` and `health:critical` always hold. Nothing else reaches a maximum: an `applyBuff` makes the buff present and
shows its grant ("+1 Bolt Charge") — but a counted buff, whose sure stacks reaching the cap put it at max (D33). A status ends only through a rule's `removeBuff` or consumption,
or an `end:`. **Consequence.** `stacksMaxed` fires only on a declaration;
a rule that reacts to the next hit at the maximum uses the `atMax` condition (Bolt Charge discharges
on the next ability hit at x10). A rule that needs a count below the cap ("x3 or more Armor Charge": Emergency
Reinforcement) fires for sure at the declared max and as a *(chance)* below it (the `belowMax` condition, D8). The cap
and the duration are facts the player reads when deciding to declare. Format: [docs/loop-format.md](loop-format.md#action-tokens)
and [docs/rule-format.md](rule-format.md).

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
energy [Bomber]") and change nothing. **Consequence.** A loop's analysis still shows where a step
gives energy back, as causality, never as an amount. The Compendium's base cooldowns, chunk scalars and `extraCharges` are recorded in the rules
as facts.

### D6 — Rules that don't stack give way and show as wasted

**Context.** The Compendium (Arc#51) says Tempest Strike's x1 Bolt Charge on a jolted kill doesn't
stack with Dielectric's. The build crafter needs to see this: a slot spent on a grant the game throws
away is wasted potential. **Decision.** A rule-level `doesNotStackWith: [<element-id>, …]`, declared
on the side that gives nothing. When a rule of a listed element fires on the same event, the
declaring rule gives way: it is reported as fired with no outcomes, naming that element
(`FiredRule.NotStackedWith`; the trace reads "doesn't stack with Dielectric [Tempest Strike]"). With
both elements equipped the build check notes it ("… doesn't stack with 'Dielectric'.", info: a crafter may keep
both for what else each gives), the build's explanation
annotates the bullet, and a loop's analysis shows it at each step where it happens. Parsing rejects an id that isn't an element of
the rules, the rule's own element, an empty list, and a `doesNotStackWith` that leads back to its
own element, directly or through a longer circle (none of those rules would apply).
**Consequence.** The granularity is the whole rule: an outcome that does stack needs a rule of its
own. Only rules matching the same event interact, and a rule gives way when any listed element's rule
matched that event, even if that rule itself gave way.

### D7 — One target: the pack in front of you, until the player declares a new one

**Decision.** One abstract target, the enemies in front of you, with a tier and the debuffs
spread across it. A debuff stays on the pack until the player declares it ended (D3); after a kill
the pack is still in front of you. When the next group of enemies comes is play, so the player
declares it: a `pack:new` step (D3) puts a new pack in front of you, with none of the old one's
debuffs; the buffs on you and the pickups on the ground stay. A loop that meets a new group each pass
starts with `pack:new`; a loop against one boss leaves it out. **Consequence.** It matches how add-clear
builds are explained ("everything is jolted"), and a debuff the next group doesn't have isn't
credited to the next pass's first hit. The tier is
`minor`: a kill rule for a higher tier shows in the build's explanation but doesn't fire in a loop
until the player can declare the tier ([docs/backlog.md](backlog.md)).

### D8 — Chance rules always fire, marked *(chance)*

**Decision.** Rules with "chance to / occasionally / rapidly" fire and are marked *(chance)* in
every view, and so are rules that depend on progress the game doesn't show as a stacking status
("after 6 weapon hits"; D3).
Probabilities would need a simulation (D1); marking them keeps the loop honest without hiding it.

### D9 — Secondary ability uses (`melee:slide`, `class:air`, `grenade:hold`)

**Context.** Some aspects give an ability a second use with its own effects. Tempest Strike (Compendium
Arc#54): the melee while sliding is an uppercut that jolts; a plain press is a basic melee. Ascension
(Arc#48): the air move key while airborne spends the class ability charge on a twirl that jolts and
Amplifies, and sets off the equipped class ability's effects. Chaos Accelerant, Bleak Watcher, Mindspun
Invocation and Unbreakable (Void, Stasis, Strand, Prismatic): the grenade held.
**Decision.** An ability use carries an input (`AbilityInput`: press, slide, air, hold): tokens
`melee:slide[:hit|kill[:N]]`, `melee:air`, `grenade:hold`, `class:air`; events
`AbilityCast(kind, input)` and `Ability(kind, type, input)` origins. An ability takes only the inputs the
game has (`AbilityInputs`: a melee slides or is airborne, the class ability has its air move, a grenade
is held; a Super has none) — any other is a parse error, in a token and in a rule. A secondary is still
that ability, so every rule on the ability fires for it too (a sliding melee is a melee for Combination
Blow); only a rule that names the input tells them apart: `abilityCast: { ability: classAbility, input:
air }` and `via: melee:slide`. Every token parses anywhere, but the palette and the HUD offer a
secondary only when an equipped rule names it — without one it fires exactly what the press fires — and
the HUD shows it below its ability's square, with the icon of the element that names it.
**Consequence.** A new secondary from an aspect is an input on an existing ability, not a new action.
Air moves that spend another ability's energy (Weavewalk's melee energy) are open
([docs/backlog.md](backlog.md)).

## Sources and rules

### D10 — Causality is authored as YAML

**Decision.** Every build element is an element with rules (`on` trigger → `then` outcomes, plus
always-on passives), authored in `rules/**/*.yaml` with provenance. Edges between elements are never
stored: they appear when an outcome matches another rule's trigger. Drafts generated from the
Compendium's "On X:" phrasing will be offered for a human to confirm, never loaded as rules.
**Consequence.** A new element is one YAML entry, never code (docs/CODING_RULES.md). Format:
[docs/rule-format.md](rule-format.md).

### D11 — The Compendium is the primary mechanics source; Clarity the cross-check

**Decision.** Game data comes first: the Destiny Data Compendium (abilities, aspects, fragments,
artifact perks, statuses, mods) is primary, Clarity (hash-keyed descriptions) the cross-check; the
user's notes come next and a creator's claim last. Every disagreement is recorded in the build's
`discrepancies.md`. **Consequence.** Snapshots stay private (licensing); rules cite rows as
`compendium/<date>/<Tab>#<row>` and paraphrase.

### D12 — The Compendium is joined by name, scoped by kind and class

**Decision.** The Compendium has no hashes, so its entries are joined to the manifest by name,
scoped by kind and class, with an alias table in git (`tools/compendium/aliases.yaml`: a tab's prefixes, suffixes and
dropped words, an element's entry name, and numbers a row says in words, each with its reason). Unresolved names are
reported, never guessed: the Compendium check (FR-11) names every element its cited row doesn't name.

### D13 — Every number is a `GameValue` with `Provenance`; unknown stays `?`

**Decision.** A number in the rules is `Known`, `PerModCount` (value by copies equipped),
`Approximate` (`~`) or `Unknown` (`?`); every element keeps where its numbers came from
(`Provenance`, its `source:`: Compendium row, Clarity hash, creator claim, or the rule file's line). **Consequence.** An unknown is shown as "?" and
never treated as 0; since numbers are facts (D1), "?" only means the source doesn't say.

### D14 — Authored slugs as ids, the manifest hash as the long-term identity

**Context.** The manifest's API needs a key, and abilities, artifact perks and armor set bonuses have no
Clarity hash; the manifest's component files are public (bungie.net serves them to anyone, ~3 MB compressed
for the item table). **Decision.** Elements are keyed by a kebab-case
`ElementId` (`shinobus-vow`); `Hashes` holds its manifest hash (plus the copies of the same item with the
same text, such as a mod's second copy), filled from Clarity where known, empty otherwise. The manifest
hash is the long-term identity (stable, shared by Clarity and DIM). What the manifest says about the
hashes Loopsmith names — official name, type, description, icon, rarity, slot, damage type, a weapon's trait
columns and the perks each rolls with, intrinsic perks, an exotic's catalyst, armor sets and their perks — is a
generated excerpt in
the rules (`rules/manifest.yaml`, `tools/manifest/`), so the app shows the bundled builds the way DIM does
without downloading the manifest. For a hash outside it (a pasted DIM link's other items, a login's, an item
picked from every item of the game), the web app reads Bungie's full manifest in the browser, the way DIM does:
when the app opens, the component files download from bungie.net (public, cross-origin) and the page indexes their
entries by hash and keeps them for as long as the tab is open; the browser saves them, gzipped, with the listing of
every weapon and armor piece made from them (D25), and a later visit reads that copy instead while the manifest index
names its version; `ManifestJoining` (pure) reads the entries it needs, the same way the extractor does, into
an excerpt added to the catalog's. **Consequence.** Builds reference slugs; the manifest join adds
hashes without changing build files; a DIM loadout is read by hash (D27), and a weapon's roll is picked by
hash among its columns' perks (a build's `roll`). Opening the app the first time (and after Bungie publishes a new
version) costs a ~9 MB download and some seconds behind a startup screen; a later visit checks the version and reads
the saved copy (~9 MB gzipped on disk, a few seconds); after that a link, a login or a pick reads from the files the
tab holds. The Core tests know only the excerpt.

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

Read first, compute, write last. The pure core returns what the host shows or saves, and the host performs
the side effect (the CLI's `Effect` union went with the CLI, D38). The pure core is deterministic — same build, state and step give the same output — replayable
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

**Context.** Explanation and trace renderers and the web designer need the same English for
triggers and outcomes; every slice that reads a rule's trigger needs the same trigger-matching semantics; slices
may not depend sideways. **Decision.** Both live in the kernel as small non-`Domain` modules, guarded
by architecture tests. **Consequence.** "What a trigger means" and "how we say it" each have exactly
one home.

### D23 — Cascade termination is per causal chain

Cascade depth ≤ 5, and a rule never re-fires on an identical event *up its own causal chain*. A
global "once per step" guard would suppress sibling events (two orbs picked up would fire the
pickup rule once).

## Hosts and tooling

### D24 — Hosts own no logic; the CLI is the scripting and golden-test host

**Superseded by [D38](#d38--the-web-designer-is-the-only-host-the-cli-and-the-loop-graph-are-removed).** Kept as it was decided:

Shells in `Orchestration` return `Effect`s that any host executes, so every host shares every line
of logic. The web designer (D25) is the main UI; the CLI (`loopsmith`) is for scripting, quick checks
of the engine against real build notes, and the golden tests, which snapshot the text it prints.

### D25 — The web designer runs in the browser, as static files

**Decision.** `Loopsmith.Web` is a Blazor WebAssembly host: the C# core runs in the browser (rules,
builds and example loops are embedded), so there is no server and any static host works — GitHub
Pages, with a preview per pull request ([docs/hosting.md](hosting.md)). Shared loops travel in
the URL fragment (`#loop=…`). The app stores nothing in the browser but Bungie's manifest (owner
decisions): a loop is kept by exporting it or copying its link. Bungie's manifest (D14) and the listing of every weapon
and armor piece made from it are saved in the browser's IndexedDB, as DIM saves its copy: the files gzipped, keyed by the
manifest version (their URLs), the listing by that version and the Loopsmith code that made it, each in place of the
one saved before. At startup the app asks the manifest index for the current version and reads the saved copy when it
is that version (or the index doesn't answer); a browser that can't save downloads at every visit, as before.
**Consequence.** No API or database; a saved library is open ([docs/backlog.md](backlog.md)). The first visit downloads the .NET runtime (≈ 3 MB, then
cached).

### D26 — Verify.XunitV3 pinned to 32.0.1

33.x requires xunit.v3 4.x and gates the build on a SponsorCheck licence property — a licensing
decision for the owner. 32.0.1 predates SponsorCheck and works with xunit.v3 3.2.2.

## Starting from a DIM link

### D27 — A DIM link starts a build of what the catalog knows by hash; the rest is left out

**Context.** Players keep their builds in DIM, and DIM shares a loadout as a link: a dim.gg share
(the loadout is on DIM's servers, which hand it to any caller) or a link to DIM's
Loadouts page carrying the loadout as JSON (D2ArmorPicker, guardian.report). A loadout names its
items only by manifest hash, and the manifest isn't read yet (D14): the catalog has hashes for most
aspects, fragments, mods and exotics it holds, none for supers, grenades, melees or weapons. **Decision.**
`LoadoutImporting` (pure) reads the link and maps the loadout by hash: the subclass from the glossary's
`subclasses` (the nine Light ones, whose hashes DIM's own source lists, and the six Stasis and Strand ones, by Bungie's manifest), every other item to the
catalog element with that hash. An
ability the catalog has no hash for is `"?"` in the build (D13: unknown is data, never guessed —
D12), and every item it doesn't recognise goes in the build's `leftOut` by hash, so whoever opens
the loop sees what is missing. The build is written as an ordinary `build.yaml`, so a loop from a
DIM link exports, shares and replays like any other. The web host fetches a dim.gg share from DIM's
open `loadout_share` API with a plain GET (no key: a header would need a CORS preflight DIM grants only
its registered apps); links that carry their loadout need nothing, and a share saved with the app
(`builds/<slug>/dim-loadout.json`: its link and the loadout as DIM's share page carries it) opens
without asking DIM. DIM offers no embeddable preview of a share (its page has only a stock link
image), so Home draws its own card of what the catalog recognised. Items the manifest excerpt lacks are
read from Bungie's full manifest first (D14), so the link's weapons, armor sets and left-out items have their
names. **Consequence.** A Prismatic loadout can't start a build yet (the glossary has no Prismatic subclass:
it waits on an element per ability); Loopsmith needs no registration with DIM ([docs/hosting.md](hosting.md), "DIM links").

### D28 — A build's stats come from the player's own armor, added up like DIM, after a read-only Bungie login

**Context.** Some cause and effect depends on a stat: a Gambler's Dodge gives back 1% of a melee charge per Melee
point (twice that near an enemy), a Combination Blow kill refills the class ability in full only from 70 Class.
A DIM link carries no stats, and the armor it names may sit in the player's vault: only the player's login
lets an app read it (Bungie shows anyone only what a character wears). **Decision.** A build's `stats:` stay a
fact of the build (the engine still simulates no energy, D1, D5), and the elements' `statChecks` say what a stat
changes in the build check. The designer sets the stats by hand, or from the player's own armor after a Bungie
login: a public OAuth client (no secret in a static site, D25), read-only, its token used for two reads and
dropped. `StatLoading` (pure) reads Bungie's answers, matches the build's pieces with the player's copies (a pick
when they own several) and adds the total up the way DIM adds up a loadout — each copy's roll and masterwork,
the build's stat mods, tuning and fragments — from the manifest excerpt's `statPlugs`, every plug that changes
an armor stat (shipped, so a phone needn't download Bungie's whole item table). **Consequence.** Stats are as
exact as DIM's for a DIM loadout's pieces; a hand-written build names no pieces, so it takes typed stats until
pieces are picked. A dim.gg share names each item's copy (its instance id): the build keeps them (`armorCopies`),
so the share's owner gets its own pieces and their stats at once; anyone else is told to pick their own, and a long
link (no ids) is like anyone else's. After the login the Stats section says only which of the two it is, in one
line: no total of its own. A piece picked from the player's armor is worn by the build — its exotic or a left-out
piece — and its set bonuses follow the pieces it wears (`ArmorSwapping`), so the loop the stats come from is the
loop that plays; a build wears one exotic. A piece whose roll the excerpt doesn't know (a newer manifest) leaves
the stats as they were, never guessed (D12). The login's page is the site's
`/login`, Bungie's one redirect URL ([docs/hosting.md](hosting.md), "Bungie login").

### D29 — The step's state shows its damage resistance, its sources multiplied, PvE and PvP

**Context.** Builds are often judged by how much damage they shrug off ("70% damage resistance"), and players rarely
know how the number is reached. The game has a known rule: different sources of resistance multiply what the others
let through, 1 − (1 − a) × (1 − b) × … (compendium Armor Mods#7), so 25% and 45% make 58.75%, not 70%. A Guardian
starts with 34.5% against combatants and none in Crucible (Game Mechanics#106). D1 forbids adding a source's numbers
up to decide what happens. **Decision (product owner).** The end of each step shows the damage resistance that holds
right then: the resistance passives (`resistDamage`, `resistCombatantDamage`) of the elements equipped and of the statuses present, the
Guardian's base among them (a keyword element, so in every build), multiplied by the game's rule. It is the one
total besides the build's stats (D28): a fact shown with its sources and formula, never game state and never read to
decide what a step fires. It shows two columns: PvE (every source) and PvP (only the sources that reduce any damage,
each by its PvP value; one the source gives none for is "?"). A resistance against some combatants only (Arc
Resistance, Melee or Sniper Damage Resistance, Concussive Dampener; Harmonic Resistance on its Super's element) is a
line of its own, all damage's sources and its own together, PvE only. An unknown value makes its total "?".
**Consequence.** A source that is only a description is not counted until it is authored as a resistance passive.
(Product owner, later.) A Super's resistance holds while you are in your Super: casting it applies the `super` status,
which the player ends (`end:super`, D3), since a roaming Super spans several steps. A resistance during an animation
(Flow State's dodge) holds on the step that casts that ability (the `during` condition). Amplified's is its keyword
element's, like Woven Mail's. A condition the format can't express is
treated as holding, as for any passive (Spark of Resistance's "3+ enemies within 15 m", D7). Copies of one mod take
the mod's own value by copies (15% | 25% | 30%); different mods multiply.

### D30 — Armor mods are picked in the pieces' sockets; the energy budget holds when its capacity is known

**Context.** Mods set much of a loop off (the Siphons, Elemental Charge, the Kickstarts), but a build got them only
from a DIM link or a build file. The game decides what fits: each piece has its sockets (a general one, its slot's,
maybe an artifice or a raid one), each mod an energy cost, each copy of a piece an energy capacity; many mods have a
cheaper copy the seasonal artifact allows. A build holds its mods as a list, as a DIM loadout does. **Decision
(product owner).** The designer shows each piece's mod sockets and a mod picker for each, from Bungie's manifest
(the listing read when the app opens: every mod the pieces' sockets take, with its cost, and each piece's sockets).
The build keeps its list (`armorMods`, the mods it leaves out) and, for a mod with a cheaper copy, which copy it holds
(`armorModPlugs`); the designer places the list in the sockets, the general mods where the energy lets them, as DIM
assigns them. The energy a piece's mods use is a build check, not game state: compared with the capacity of the
player's copy (after a Bungie login, D28) it refuses a pick that takes the piece over it, and it never decides what a
step fires. Without a login the capacity is "?" and nothing is refused (D13): a piece picked from every piece of the
game has no copy to read it from. **Consequence.** Mods the rules don't know can be picked (⊘), like perks; a raid's
or an activity's socket isn't shown (D35). A slot with no piece holds one free socket more than its
mods, since it has no sockets to read. A tuning mod (a Tier 5 copy's tuning socket) isn't picked here; it stays in
the build. A stat mod the manifest excerpt lacks (`statPlugs`) doesn't add to the stats yet.

### D31 — "This weapon" is bound by the build check; a counter the game doesn't show splits into certain and chance kills

**Context.** A weapon mod or perk acts on the weapon that holds it ("Synergy: kills with this weapon … spawns a pickup of
the weapon's element"), but an element is one YAML entry for every weapon that can hold it, so its rules can't name a
slot or a damage type (the perks of `weapons/perks.yaml` named their one weapon's type instead). Synergy's counter is
progress the game doesn't show as a stacking status (D8: a chance rule), yet the owner wanted a way to make it fire
for sure, as a kill count, and each enhanced mod as its own element, offered only where the game allows it.
**Decision.** A rule says `weapon:this` (or `weapon:this:<type>`), and only an element a weapon
holds may; the build check binds it to the weapon holding it — its slot, and its type for the typed form, a rule for
another type left out — so the engine sees an ordinary weapon-in-slot source, and the same element
on two weapons is two elements, not stacked copies. A kill trigger takes `fewerThan` beside `atLeast`, so one count
splits the kills of one action: the kills that fill the counter on their own (12 rank-and-file for Synergy, 8 for its
enhanced version, from the Compendium's per-kill share) fire for sure, fewer fire as a chance. **Consequence.** The
trigger palette offers the counts equipped rules name (12) beside 1 to 9. Kills of earlier steps don't add up (D1): a
step of fewer kills stays a chance, whatever came before. An enhanced weapon mod is its own element and its own plug;
the designer offers it only for a weapon known to be Tier 4 or 5 (after a Bungie login), as the game does.

### D32 — A build names its artifact and the perk in each of its slots (Artifacts 2.0)

**Context.** Monument of Triumph (Update 9.7.0, June 2026) replaced the seasonal artifact with seven artifacts, one
equipped at a time, each with three tiers of seven perks, seven perk slots and no unlock thresholds. A build named its
artifact perks, not its artifact, which was found from them (the one that unlocks the most), so it couldn't hold an
artifact with no perks picked, and the perks couldn't be changed but by a new DIM link or build file. **Decision
(product owner).** The designer's artifact editor changes the artifact and picks the perk in each slot, laid out like
the game's screen: the slots in three groups under ◆, ◆◆ and ◆◆◆, each picked from a dropdown like a fragment. The
slots are Bungie's, not authored: the artifact's seven perk sockets take three plug sets that reach one tier further
each (two Tier 1, three Tiers 1–2, two Tiers 1–3), so a slot offers only the perks of its tiers and nothing is
counted. A build names its artifact and the perk in each slot (`artifact: { hash, slots }`), so a perk comes back in
the slot it was put in; `artifactPerks` and the left-out perks stay what it equips. One that doesn't (a build file, a
DIM link) keeps the artifact its perks point to, each placed in the free slot that takes the fewest tiers. The manifest
excerpt holds all seven artifacts with their `tiers`, `slots` and flavour text, and every perk. Switching artifact in
the editor shows the slots last picked for it while the loop is open (the tab's memory, D25: nothing is stored).
**Consequence.** A perk the rules don't know can be picked (⊘, in `leftOut`), like a fragment or a mod. Artifact perks
being off in Trials and Competitive Crucible isn't modelled.

### D33 — Armor Charge is counted: sure stacks and chance stacks, and the count decides

**Context.** Armor Charge is a pillar of build crafting: many parts of a build gain it, keep it or spend it. D1 left
every stack uncounted and D3 had the player declare "at max", so a loop couldn't say how many stacks it holds, a
Kickstart's refund was "?" and a finisher that needs x3 fired as a chance whatever came before. Yet most gains are
steps of the loop the engine already plays — "dodge for Reaper, a weapon kill, pick up the orb" surely gives one — and
the rest are chance rules (a Siphon's orb from one kill, Elemental Charge's progress), which D8 already marks.
**Decision (product owner).** A buff the glossary marks `counted: true` (Armor Charge only, for now) holds its stacks
in the state: the **sure** ones, from rules that always fire, and the **chance** ones, from chance rules and from what
they set off — a pickup on the ground only chance rules dropped gives what it gives only by chance, and so does an
event a chance rule raised. A grant adds its stacks and the passives' extra ones (Stacks on Stacks) to the sure or the
chance stacks, up to the build's cap (x3, Charged Up's x4–x6); the rest is wasted ("at cap"), and sure stacks at the
cap put the buff at its maximum, as a declaration would. A spend takes them off: every stack (a Kickstart, a removal,
`end:`) or a fixed number (`spendStacks`: sure ones first, then chance ones), and the buff ends with none left. The
count decides: a guard on the buff (`has`, `lacks`, `holds: { atLeast: N }`) holds for sure when the sure stacks meet
it, by chance (the rule fires marked *(chance)*, a passive holds as a chance) when only the chance ones do, and not at
all otherwise — the rule doesn't fire, and the step says what it needed ("Bulwark Finisher needs x3 Armor Charge,
holds x1"). A table by the stacks spent (the finishers: x3 → 70%) or by charges, each copy and each stack counting one
(the Kickstarts: 2 mods and x2 → 28.4%), gives the value for the sure stacks and, beside it, the value with the chance
ones. `max:armor-charge` is offered while the sure stacks are below the cap and makes them the cap. The state shows
the count ("Armor Charge x1 · +1 (chance)") and each change since the buff was last gained from none, with its step;
the next pass of a loop starts from what the last one ended with. A kill count takes the window its source gives
(`atLeast: 2, within: 3s`): the kills of one step are at once, so a Siphon's orb from two kills is sure and from one a
chance (the split of D31), and so are Spark of Amplitude's two kills within 4 s and Spark of Discharge's three kills
(its counter, as Synergy's); the enemy-count selector says what each count is for ("2 in 3 s · Arc Siphon",
"3 · Spark of Discharge") and goes up to the last count that still fills something — playing the step with one enemy
more each time from the state it starts in, until no buff that stacks gains more below its cap and no ability's energy
below a full charge ("5 enemies · Bolt Charge x10", "7 enemies · grenade full"). Those sums bound the choices only:
never game state, never read to decide what fires (D1, D5).
**Consequence.** The count is game state, the one number besides the step (`CausalStateTests` allows it and nothing
else): it is a fact of the loop's order, not a simulation — time is still not modelled, so the decay (one stack every
10 s, slower with Time Dilation) is a fact shown with the count, never applied, and "sure" means "if the player
doesn't wait". Other stacking statuses (Bolt Charge, Combination Blow, Slice) stay declared (D3) until they are marked
counted. Kills of earlier steps still don't add up (D31).

### D34 — A hit step is damage kept up; a kill step's killing blow is one blow

**Context.** Some sources need several hits on one enemy or a share of its health: To Shreds unravels after 6 weapon
damage instances on a Severed enemy, Horde Shuttle spawns a Threadling for weapon damage worth 10% of an Unraveled
enemy's health, Attrition Orbs drops an orb after a magazine-based number of hits (3 for a grenade launcher), and
Thunderlord's Lightning Rounds strikes every 10th hit. They were *(chance)* rules on any damage (D8), so they fired as a
chance under every killing blow, cluttering each kill with effects the kill may not have earned: in easy content one
shot kills, in hard content many hits come first.
**Decision (product owner).** A step's hits stand for how much damage the player says it is. A **hit step**
(`kinetic:hit`, `grenade`) is damage kept up on the enemy without killing it, enough for any number of hits or share
of health a source needs: such a rule (`damage: { …, sustained: true }`) fires there **for sure**. A **kill step**'s hit
is the killing blow, one blow: such a rule doesn't fire on it. A strike's, a summon's and a finisher's hits are one
blow too. A rule that needs one hit (Shinobu's Vow's Bolt Charge, Spark of Shock's Jolt, Slice's Sever) fires on every
hit, the killing blow included.
**Consequence.** The player adds a hit step before the kill when the loop keeps shooting an enemy (as `max:` declares a
threshold, D3); a kill step shows only what killing does. Kill counters the game doesn't show (Elemental Charge's progress,
Synergy, Spark of Discharge) are kills, not damage, and stay as D31 says.

### D35 — Raid, activity and seasonal armor mods aren't supported: a DIM link's are kept but ignored, and no picker offers them

**Context.** Raid armor (and Last Wish's, the Nightmare Hunts', the Dreaming City's) has a socket of its own for mods that
work only in that raid or activity: most act on its mechanics — carrying the Relic, Flux of Darkness, a Vex relay, Taken
or Nightmare enemies — and the rest on what a loop already has (an elite's kill, a Strand weapon kill). The seasonal
sockets of older armor (Black Armory, Opulence, Undying) hold mods Bungie has deprecated: "no longer functions". A loop
of triggers holds none of an encounter's mechanics, and modelling them (declaring the pack's race, a relic carried, a
plate stood on) is a lot of format for builds tuned to one encounter. **Decision (product owner).** Not supported, for
now. What a mod is comes from its plug category in Bungie's manifest — `enhancements.raid_*`, `enhancements.season_*`
and `enhancements.rivens_curse` — never from its name, so a new raid's mods are covered too: the join and the excerpt
call such a mod an `activityMod`. The pickers don't offer them: a piece's raid or activity socket isn't one of its mod
sockets, so the designer shows none and the mod picker lists none. A DIM link's are kept in the build's `leftOut`, as
the loadout had them, but ignored: no socket, not a gap (not counted under "Not in Loopsmith yet"), their squares faded
with a ✕ instead of ⊘, and the build check says so in one info line ("Ignored from the DIM loadout: Herd Thinner ×2 —
raid, activity and seasonal mods aren't supported."). **Consequence.** A raid build's loop is designed without its raid
mods. The Core tests, which read only the manifest excerpt, know such a mod when a saved DIM share wears it (the excerpt
then holds it); otherwise it is a hash no rule knows. Supporting them later means declarations for an encounter's mechanics
(D3), a decision of its own.

### D36 — The player says when a weapon step breaks a shield and when an ability step spends its last charge

**Context.** Shield Break Charge gives Armor Charge when you break a combatant's shield with damage of its element, and
the Kickstarts (Grenade, Melee, Utility) spend it when the ability's energy is "fully expended", as Bungie's text says
(the Compendium says "on use"). Neither could be said in a loop: no step broke a shield, so Shield Break Charge was
inert, and the Kickstarts fired on every use, wrong for a build with two charges (Shinobu's Vow: only the second
throw empties the grenade). Which enemy has a shield, and which charge a use spends, are play (D1): the engine counts
neither a shield nor an ability's energy (D5).
**Decision (product owner).** A step carries an option the player turns on, as it carries a target count (D4). A weapon
step may **break a shield** (`kinetic:kill:shield`, "Festival Flight (💀 kill · 🛡 shield)"): it breaks one, with its
first hit, raising `ShieldBroken` from that weapon, which the `shieldBroken` trigger matches (`via` as a hit's). The
player says it only when the weapon's element matches the shield's, so "with damage of its element" holds. A grenade,
melee or class ability step may spend the **last charge** (`grenade:kill:last`, "Grenade (💀 kill · ⬜ spend fully)"):
it raises `EnergyExpended` after the cast, which the `energyExpended` trigger matches. With one charge in the build every
use raises it; with more (the ability's profile plus every equipped `extraCharges`) only a use the player marks. A
Super has none. In the designer each row shows its option when the build has a rule that reacts to it and, for the last
charge, when the build gives the ability more than one: "🛡 shield" next to a weapon's hit and kill, "⬜ Spend fully" next
to an ability's; on, it goes with every button of the row. A grant may give stacks by copies (`stacks: "1 | 2"`:
Shield Break Charge's x1 with one copy, x2 with two).
**Consequence.** The ability's charges are a number of the build added up (its profile's and the `extraCharges`) and
compared with one to decide what a step fires: docs/CODING_RULES.md names it as the fourth exception to "State is causal"
(the owner's decision). It is never counted down: a use doesn't take a charge off. Shield Break Charge feeds the Armor
Charge count (D33), and a Kickstart in a two-charge build spends it
only where the loop says the last charge goes. The option is part of the step's token, so loop files and share links
carry it, and the analysis and comparison tell `grenade:kill` from `grenade:kill:last`. An ability's energy is still not
counted: a later decision may track it (a use empties it, each energy grant fills it), and the last charge would then
follow from the loop instead of the player.

### D37 — A reload is a step the player declares, and a rule can reload a weapon; no ammo is counted

**Context.** Demolitionist's two halves make a loop: kills with the weapon give grenade energy, and using the grenade
reloads the weapon. Other weapon perks react to a reload (Heal Clip, Voltshot: "reload after a kill"), and some effects
put rounds back (Return Stroke's lightning strike). Loopsmith counted no ammo and had no reload, so Return Stroke was
inert, and a perk's rules couldn't say "reload this weapon". When a weapon runs dry is play: the engine counts neither a
magazine nor its reserves (D1).
**Decision (product owner).** A reload is a step of its own the player declares, `kinetic|energy|power:reload` ("Reload
Festival Flight"), offered for every weapon next to its hit and kill ("🔄 reload"): it takes no count and no option, and
it raises `Reloaded` from that weapon, which the `reload` trigger matches (`via` names a weapon, `weapon:this` the one
holding the element). An outcome reloads a weapon too (`reloadWeapon: weapon:this`, or a slot): it is a reload, so that
weapon's `reload` rules fire as on the step (the owner: a reload that has an effect, like Heal Clip's heal, fires it),
and a partial refill (Return Stroke's 5 rounds) is one reload, its rounds a fact. An energy grant may go by the archetype
of the weapon holding the element (`byArchetype`: Demolitionist's 10%, 20% on Fusion Rifles, Glaives, Shotguns and
Sniper Rifles): the build check picks the weapon's amount when its archetype is known, and shows both when it isn't.
**Consequence.** A reload is never needed and never blocked (but for an empty slot): a loop shows a Reload step where
the player reloads, and nothing checks the magazine. A `reloadWeapon` outcome fires the weapon's `reload` rules, and
the weapon shoots again in the next step, which closes Demolitionist's loop (grenade → reload → kills → grenade energy
→ grenade). Cooldowns between refills (Demolitionist's
3 s) and the rounds put back are facts in the description. Reload speed stays outside the loop (Feeding Frenzy is inert).

### D38 — The web designer is the only host; the CLI and the loop graph are removed

**Context.** The CLI (`loopsmith`: `explain`, `validate`, `trace`, `play`, `loop`, `compare`, `graph`, `loops`) was the
scripting and golden-test host of D24. The owner doesn't use it: the web designer (D25), with each PR's e2e scenarios
recorded as GIFs and MP4s, is how Loopsmith is iterated on, and the golden tests call the core directly.
**Decision (product owner).** `Loopsmith.Web` is the only host. The CLI goes, with the core code only it used: the
command shells, the interactive `play` session, the `Effect` union its shells returned, and the `LoopGraphing` slice
(the cause → effect graph and its cycles, `graph` / `loops`, and the generated `builds/*/loop-graph.md`); git history
keeps them. What the CLI gave sessions and agents stays as Core tests: `RepoFileValidationTests` opens every build and
loop file of `builds/` and every e2e fixture against the repository's rules, as the designer's import does, and names
the file and the error of each one that doesn't (`dotnet test --filter RepoFileValidationTests`). **Consequence.** A
loop is found by designing it (D2): the designer's analysis judges its order (FR-6); nothing discovers cycles any
more (FR-8 dropped). The host boundary test checks the web host only, and the core's pure API is unchanged.
