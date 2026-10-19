# Requirements

## 1. What Loopsmith is — the level of abstraction

**Loopsmith describes cause and effect in a Destiny 2 build. It does not simulate the game.**
([ADRs D1](ADRs.md))

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
| Show a source's number as a fact with its outcome: "+12% grenade energy", "Amplified (15s)", "up to x10" | Add numbers up or count stacks to a maximum (two exceptions deciding nothing: the build's stats, D28, and the step's damage resistance, D29; one that decides: Armor Charge's stacks, sure and chance ones, D33) |
| Keep **what is present**: a buff on you, a debuff on the pack, a pickup on the ground | Track time, energy, health or enemy counts |
| Let the **player declare** what only play decides: "Bolt Charge at max", "Amplified ends", "this grenade kills 3" | Decide when a threshold is reached or a buff ends |
| Judge a loop by its **order of triggers**: does each step get what it needs from the steps before it, can the order be repeated | Score a loop or add up what it gives |

The product is the **loop the player designs** (D2): an ordered list of triggers and declared states
("dodge → grenade kill → pick up orb → Bolt Charge at max → grenade kill"), saved as a `*.loop.yaml`,
shared as a link, replayed, analysed and compared with another order.

## 2. Functional requirements

| ID | Requirement | Status |
|---|---|---|
| FR-1 | **Compose a build** like DIM: class, subclass, super, grenade, melee, class ability, jump (known, but inert: movement has no part in a loop), aspects, fragments, exotic armor, armor set bonuses, armor mods (repeat = stacked copies), artifact perks, weapons with type, archetype, perks and mod, stats. | Build files (`build.yaml`), a DIM link (FR-14) or from scratch (an Arc Hunter with nothing equipped, [loop-format.md](loop-format.md#starting-from-scratch)); the designer edits the subclass (any of the nine Light ones and the six Stasis and Strand ones: super, abilities, jump, aspects, fragments) and fills, swaps or empties any weapon or armor slot from every weapon and armor piece of the game, no login needed ([loop-format.md](loop-format.md#every-item)), picks, swaps or clears any armor mod in each piece's sockets ([loop-format.md](loop-format.md#armor-mods)), picks each weapon's mod among what its mod socket takes, and changes the artifact (any of the seven of Artifacts 2.0) and picks the perk in each of its seven slots (two take Tier 1, three Tiers 1–2, two Tiers 1–3) ([loop-format.md](loop-format.md#artifact)); the class needs a new DIM link or build file |
| FR-2 | **Validate a build** against the rule catalog: unknown elements, wrong slots, another class, another affinity, aspect and fragment limits, elements with no rules (inert), rules that don't stack with an equipped element. | Done; the armor energy budget refuses a mod picked in the designer that takes a piece over its copy's capacity, when a Bungie login gives it ([D30](ADRs.md#d30--armor-mods-are-picked-in-the-pieces-sockets-the-energy-budget-holds-when-its-capacity-is-known)), and the weapon picker refuses a second exotic weapon; the build check doesn't report a build file or DIM loadout that is already over a piece's energy or already wears two exotic weapons |
| FR-3 | **Explain a build**: every trigger it reacts to → the outcomes it fires `[source]`, in the shape of a hand-written build note. | Done (web designer) |
| FR-4 | **Play one step** against the current state — a trigger (dodge, grenade kill on 3 enemies, a sliding melee, pick up an orb, an air move) or a declared state (D3) — and get every rule it fires, cascades included, which element fired it and why, and what is available next (in the web designer one line per element and per outcome, each name tappable for its details, then what is present after the step: buffs on you, on your weapons, debuffs on the pack, the ground, and what each status switches on). | Done (web designer) |
| FR-5 | **Design a loop** step by step, with notes and a description; undo, branch from an earlier step, remove or move any step (the loop replays in its new order); save it as a self-contained `*.loop.yaml` and share it as a link. | Done (web designer) |
| FR-6 | **Analyse a loop's order**: played from a fresh spawn and repeated from where it ends — does it repeat or where does it break, what each step needs and which earlier step provides it (drawn as a chain of which step feeds which), what it sets off because of the order and on its own, what is wasted. | Done (web Analysis) |
| FR-7 | **Compare two loops' orders** trigger by trigger: where each trigger's needs come from in its place in each loop, and what it sets off in only one of them; and as two chains, the links only one loop has marked. | Done (web Compare) |
| FR-8 | **Discover loops** in the cause → effect graph (cycles), and draw the graph. | Dropped ([D38](ADRs.md#d38--the-web-designer-is-the-only-host-the-cli-and-the-loop-graph-are-removed)): the loop graph went with the CLI; a loop is designed and its order analysed in the web designer (FR-5, FR-6) |
| FR-9 | Every fired rule carries its **source element, trigger, outcomes and reason**; every element its **provenance**, every number its **certainty** (known, approximate `~`, unknown `?`). | Done |
| FR-10 | Builds and loops **pin a catalog version**, so a rules change never silently changes them (D15). | Done |
| FR-11 | **Check** the rules against a dated Compendium snapshot and Clarity (the sources stay private: they are read, never loaded into the app or committed); a **coverage report** (elements without rules, names that don't resolve, numbers that disagree with the sources). | Partly done: the Compendium check (`CompendiumReading`, a test run with `LOOPSMITH_COMPENDIUM_DIR`; tools/compendium/README.md) reads the subclass tabs, Class Abilities, Armor Mods, Artifact Perks and Weapon Perks, and checks that every rule citing them finds its element in the cited row and every number it takes from its source among the row's (names that differ: `tools/compendium/aliases.yaml`, D12). The numbers were copied by hand: from 2026-10-09 most armor mods (the chest mods among them), 12 Arcstrider plugs, the artifact perks, the exotics, three weapon perks and some keywords; from 2026-10-13 the other 22 Arcstrider plugs, the weapon mods and some keywords; from 2026-10-14 eight weapon perks. Open: the other tabs (Exotic Weapons, Exotic Armors, Armor Perks, Game Mechanics), Clarity, and the coverage report |
| FR-12 | **Draft rules** from the Compendium's "On X:" phrasing for a human to confirm (D10). | Open |
| FR-13 | **Join the Bungie manifest**: hashes, official names, icons (D14). | Done in the web app: `rules/manifest.yaml`, an excerpt for the hashes Loopsmith names (names, Bungie's descriptions, icons, slots, damage types, a weapon's trait columns and the perks each rolls with, intrinsic perks, catalysts, armor sets and their perks), generated by `tools/manifest/`; for any other hash (a pasted link's) the browser reads Bungie's full manifest, loaded when the app opens and kept for the tab, and saved in the browser with the listing of every weapon, armor piece and armor mod (each piece with its mod sockets), so a later visit with the same manifest version downloads nothing (D14, D25). An item's details show Bungie's text next to Loopsmith's rules. The Core tests know only the excerpt |
| FR-14 | **Start from a DIM link** (D27): a dim.gg share or a link carrying its loadout → a build of what the catalog recognises by hash, named after the loadout with the link as its source; an ability without a hash is `?`, everything unrecognised is listed (`leftOut`), nothing is guessed. | Done for links that carry their loadout and for the Arc, Solar, Void, Stasis and Strand subclasses, shown the way DIM shows a loadout, with each weapon's perks picked and an exotic's catalyst switched on in the designer (a link carries neither), and the armor set perks its pieces grant; items outside the excerpt are read from Bungie's full manifest (FR-13); dim.gg links are read from DIM's open share API, no key ([hosting.md](hosting.md#dim-links)); the Prismatic subclass waits on an element per ability |
| FR-15 | **A build's stats** (D28): typed in the designer, or added up from the player's own armor after a read-only Bungie login (a DIM loadout's pieces matched with their copies, the way DIM adds a loadout up); the build check says what a stat changes (`statChecks`: Gambler's Dodge below 50 Melee, Combination Blow below 70 Class). | Done in the web app: the owner of a dim.gg share gets its own pieces' stats at the login (the share names each copy; one line says so); anyone else is told to pick their own pieces with ⋯ (the armor picker's Yours: their pieces for the slot, the build's set bonuses following the pieces; Every item lists the game's); a hand-written build takes typed stats until pieces are picked; the player's copies show their gear tier and masterwork on the build's armor, weapons and the picker as DIM shows them, and each weapon its breaker and element; the login also loads the build's weapons from the player's copies (their perks and catalyst), and ⋯ next to each weapon picks one of theirs (the weapon picker, one exotic); for a weapon whose copy is theirs, the perk picker marks the perks the copy doesn't have (each perk of a column that rolled two or more is the copy's) and Validation warns when one is picked |
| FR-16 | **The step's damage resistance** (D29): the end of each step shows the damage resistance that holds right then, PvE and PvP — the resistance passives of the elements equipped and the statuses present, the Guardian's base 34.5% against combatants among them, multiplied the way the game stacks them (1 − (1 − a) × (1 − b) × …); a line of its own, PvE only, for each resistance against some combatants only (Arc Resistance, Harmonic Resistance on the Super's element, Melee and Sniper Damage Resistance, Concussive Dampener); tapping its title lists each source and the formula; "?" when a source's value is unknown. Below it, the damage bonuses that hold right then (Combination Blow's melee bonus, Flashover's, the Weapon Surges'), a row per damage source with each source's own value, never added up ("?" when unknown); tapping its title lists the sources. | Done (web designer); the Supers' (while in your Super, until `end:super`), Amplified's and Flow State's (on a dodge step) count, and Emergency Reinforcement's (after a declared `health:critical`, until `end:emergency-reinforcement`); the armor set bonuses' and artifact perks' aren't counted until authored |
| FR-17 | **Count Armor Charge** (D33): each step's state shows the Armor Charge the loop surely holds and the stacks it may hold besides ("x1 · +1 (chance)"), up to the build's cap, with each step that gained or spent some; the count decides — a rule that needs it fires for sure, as a chance or not at all (with a note), a Kickstart's or a finisher's energy and duration come from the Compendium's table for the stacks spent — and the enemy-count selector says what each count is for ("2 in 3 s · Arc Siphon"). | Done (web designer); the decay is shown, never applied; Bolt Charge, Combination Blow and Slice stay declared |
| FR-18 | **Say what only play decides about a step** (D36): a weapon step breaks a combatant's shield (`:shield`, "🛡 shield") or lands precision kills (`:precision`, "🎯 precision"), and a grenade, melee or class ability step spends the ability's last charge (`:last`, "⬜ Spend fully"), its energy fully expended. | Done (web designer: the row's box beside its count; loop files and share links: the token's option); Shield Break Charge gives Armor Charge by copies, Spark of Brilliance blinds on a precision kill only, the Kickstarts spend it on the last charge (with one charge every use); an ability's energy isn't tracked, so with more charges the player marks the last one |
| FR-19 | **Reload a weapon** (D37): a Reload step for each weapon (`energy:reload`, "🔄 reload" next to its hit and kill), a `reload` trigger rules react to (`via`, `weapon:this`), and an outcome that reloads a weapon as the step does; an outcome that refills one (`refillWeapon`) is no reload and fires no `reload` rule (D42); no ammo is counted. | Done (web designer, loop files and share links); Demolitionist (grenade energy by the weapon's archetype, and the grenade refills it) and Return Stroke refill |
| FR-20 | **Pick up an ammo brick** (D41, D43): a step for a Special or a Heavy brick, walked or slid over (`ammo:heavy`, `ammo:special:slide`; "📦 Ammo brick" below the weapons, with "special", "heavy" and "🛝 by sliding" when a rule reacts to a slide), always available, and an `ammoPickedUp` trigger rules react to (`brick:`, `by: slide`); a refill is no reload, and no ammo is counted. | Done (web designer, loop files and share links); Spark of Momentum (a slide only), All-Star and Adhesive Ordnance use it; Overflow and Lead from Gold are inert (ammo) |

## 3. Non-functional requirements

* **Deterministic:** the same build, state and step give the same output. Golden tests replay real
  build notes ([builds/skip-grenade-hunter/note-map.md](../builds/skip-grenade-hunter/note-map.md)).
* **Auditable:** rules live in git; every number traces to a Compendium row, a Clarity hash, a
  creator's claim or a rule file's line. Disagreements between sources are recorded per build
  (`discrepancies.md`).
* **Honest about the unknown:** a "?" stays "?" — never 0 — and a rule that might not happen in game is
  marked *(chance)* (D8, D13).
* **Enforced architecture:** architecture tests in CI guard slice boundaries, I/O, and that hosts
  own no logic ([CODING_RULES.md](CODING_RULES.md)).
* **Serverless web app:** runs in the browser from static files; nothing is sent to a server or
  stored in the browser but Bungie's manifest, saved by its version (D25).
* **Private sources stay private:** Compendium snapshots and creator transcripts are never committed or
  served; rules paraphrase and cite ([README](../README.md#data-sources)).

## 4. Out of scope

* Simulating combat: quantities, time, cooldowns, energy, damage, health, DPS (D1).
* PvP numbers (the `[PvP]` part of a value is read and dropped, but for damage resistance: FR-16), enemy AI, positioning.
* Raid, activity and seasonal armor mods (an encounter's mechanics: the Relic, a Vex relay, Taken enemies; the seasonal
  ones no longer work): no picker offers them, and a DIM link's are kept but ignored ([loop-format.md](loop-format.md#raid-activity-and-seasonal-mods),
  ADRs D35). Supporting them would take declarations for an encounter's mechanics, a later iteration if asked for.
* Importing a player's inventory (Bungie OAuth) — possible after the manifest join. A shared DIM loadout
  can start a build (FR-14).
