# Requirements

## 1. What Loopsmith is — the level of abstraction

**Loopsmith describes cause and effect in a Destiny 2 build. It does not simulate the game.**
([ADRs D1](../ADRs.md))

Build guides explain a build as triggers and what they set off: "dodge → Reaper → the next grenade
kill drops an Orb of Power → picking it up gives Armor Charge → the next grenade spends it". Loopsmith
makes that explicit and checkable. For every trigger in a build it shows:

* every **outcome** that fires, and which **element** caused it;
* what that **unlocks** next (an outcome that is another rule's trigger);
* what **doesn't work together**: a grant the game throws away because it doesn't stack with
  another element's (wasted potential, D6).

What it deliberately does **not** answer: how many, how often, how long, or when. Combat has too
many variables — how many enemies and which, where they stand, shields, whether the player hits,
whether a grenade lands — and almost everything runs on time (cooldowns, durations, decay). So:

| Loopsmith does | Loopsmith doesn't |
|---|---|
| Show a source's number as a fact with its outcome: "+12% grenade energy", "Amplified (15s)", "up to x10" | Add numbers up, count stacks to a maximum, or count a duration down |
| Keep **what is present**: a buff on you, a debuff on the pack, a pickup on the ground | Track time, energy, health or enemy counts |
| Let the **player declare** what only play decides: "Bolt Charge at max", "Amplified ends", "this grenade kills 3" | Decide when a threshold is reached or a buff runs out |
| Judge a loop by its **order of triggers**: does each step get what it needs from the steps before it, can the order be repeated | Score a loop by kills, uptime, damage or energy per cycle |

The product is the **loop the player designs** (D2): an ordered list of triggers and declared states
("dodge → grenade kill → pick up orb → Bolt Charge at max → grenade kill"), saved as a `*.loop.yaml`,
shared as a link, replayed, analysed and compared with another order.

## 2. Functional requirements

| ID | Requirement | Status |
|---|---|---|
| FR-1 | **Compose a build** like DIM: class, subclass, super, grenade, melee, class ability, aspects, fragments, exotic armor, armor set bonuses, armor mods (repeat = stacked copies), artifact perks, weapons with type, archetype and perks, stats. | Build files (`build.yaml`); a picker UI over the catalog is open |
| FR-2 | **Validate a build** against the rule catalog: unknown elements, wrong slots, another class, another affinity, aspect and fragment limits, elements with no rules (inert), rules that don't stack with an equipped element. | Done; armor energy budget and the exotic weapon limit are open |
| FR-3 | **Explain a build**: every trigger it reacts to → the outcomes it fires `[source]`, in the shape of a hand-written build note. | Done (`explain`, web) |
| FR-4 | **Play one step** against the current state — a trigger (dodge, grenade kill on 3 enemies, pick up an orb, an air move) or a declared state (D3) — and get every rule it fires, cascades included, which element fired it and why, and what is available next. | Done for triggers; declared states are open (`docs/backlog.md`) |
| FR-5 | **Design a loop** step by step, with notes and a description; undo, branch from an earlier step; save it as a self-contained `*.loop.yaml` and share it as a link. | Done (`play`, web designer) |
| FR-6 | **Analyse a loop's order**: played from a fresh spawn and repeated from where it ends — does it repeat or where does it break, what each step needs and which earlier step provides it, what it sets off, what is wasted. | Open: today's report still counts (`docs/backlog.md`) |
| FR-7 | **Compare two loops' orders** trigger by trigger: what each trigger sets off in its place in each loop, and why it differs. | Open: today's comparison still counts |
| FR-8 | **Discover loops** in the cause → effect graph (cycles), and draw the graph. | Done (`loops`, `graph`) |
| FR-9 | Every fired rule carries its **source element, trigger, outcomes and reason**; every number its **provenance and certainty** (known, approximate `~`, unknown `?`). | Done |
| FR-10 | Builds and loops **pin a catalog version**, so a rules change never silently changes them (D15). | Done |
| FR-11 | **Ingest** a dated Compendium snapshot and Clarity; a **coverage report** (elements without rules, names that don't resolve, numbers that disagree with the sources). | Open: the first snapshot was ingested by hand |
| FR-12 | **Draft rules** from the Compendium's "On X:" phrasing for a human to confirm (D10). | Open |
| FR-13 | **Join the Bungie manifest**: hashes, official names, icons (D14). | Open |

## 3. Non-functional requirements

* **Deterministic:** the same build, state and step give the same output. Golden tests replay real
  build notes ([builds/skip-grenade-hunter/note-map.md](../builds/skip-grenade-hunter/note-map.md)).
* **Auditable:** rules live in git; every number traces to a Compendium row, a Clarity hash, a
  creator's claim or a rule file's line. Disagreements between sources are recorded per build
  (`discrepancies.md`).
* **Honest about the unknown:** a "?" stays "?" — never 0 — and a rule that might not happen in game is
  marked *(chance)* (D8, D13).
* **Enforced architecture:** architecture tests in CI guard slice boundaries, I/O, and that hosts
  own no logic ([CONVENTIONS.md](../CONVENTIONS.md)).
* **Serverless web app:** runs in the browser from static files; nothing is sent to a server or
  stored in the browser (D25).
* **Private sources stay private:** Compendium snapshots and creator transcripts are never committed or
  served; rules paraphrase and cite ([README](../README.md#data-sources)).

## 4. Out of scope

* Simulating combat: quantities, time, cooldowns, energy, damage, health, DPS (D1).
* PvP numbers (the `[PvP]` part of a value is read and dropped), enemy AI, positioning.
* Importing a player's inventory (Bungie OAuth, DIM loadouts) — possible after the manifest join.
