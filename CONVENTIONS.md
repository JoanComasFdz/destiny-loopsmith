# Conventions (binding)

These are **requirements, not suggestions**: the conventions Loopsmith keeps across
its code, its data (`rules/`, builds, loop files) and its UI. They are binding and,
together with the coding rules, win over any other document. The *reasoning* behind
them is recorded in **[`ADRs.md`](./ADRs.md)**; an ADR explains why, it never
overrides what this file says.

## Which rules bind what

- **The C# code** (`src/`, `tests/Loopsmith.Core.Tests/`) follows
  [`docs/CODING_RULES.md`](docs/CODING_RULES.md): architecture (vertical slices,
  kernel-only dependencies, hosts without logic), functional design (impure → pure →
  impure, one pure-or-impure statement per line, effects as data, own
  `Optional`/`Result`/`Unit`), domain modelling (verb-named functions, immutable
  records, Dunet DUs, Vogen value objects at the boundary) and the API gotchas.
- **The end-to-end tests have their own rules.** `tests/Loopsmith.Web.E2E` follows
  [`E2E-TEST-CODE-RULES.md`](tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md), not the
  coding rules; this file's conventions still bind the app it drives.
- **Every convention below binds all of them.**

## Cause and effect

- **Build elements are data, not code.** A new aspect, fragment, mod, exotic,
  artifact perk or weapon trait is **one YAML entry** under `rules/` — never an
  edit to a shared classifier or a dispatch `switch`. A new trigger or outcome
  kind is one Dunet case, and the compiler flags every `Match` that must handle it.
- **Unknowns are data.** A source's "?" becomes `GameValue.Unknown`, never 0; the
  trace shows "?". Every element keeps its `Provenance` (its `source:`).
- **State is causal** (ADRs D1). Game state says what is present (a buff, a
  debuff on the pack, a pickup on the ground) and what the player declared
  ("Bolt Charge at max"); it holds no number but the step and a counted buff's
  stacks (ADRs D33: Armor Charge's sure and chance stacks, which do decide what
  fires — the one counter of the game state). A source's number
  (`+1 Bolt Charge`, `15s`, `+12% grenade energy`) is a fact shown with its
  outcome — never accumulated, and never compared to decide what happens.
  Four exceptions, none of them game state: the build's stat line (ADRs D28) —
  `StatLoading` may add it up from the player's armor the way DIM does, and the
  build check may compare it with an element's declared `statChecks` to choose a
  warning or a note — the step's damage resistance (ADRs D29), the resistance
  passives holding right then multiplied by the game's rule, shown with its sources,
  and the armor energy a piece's mods use (ADRs D30), added up per piece and compared
  with its copy's capacity to refuse a mod picked in the designer that doesn't fit.
  None of these three ever changes what a step fires. The fourth does: an ability's
  charges in the build (ADRs D36), its profile's `charges` plus every equipped
  `extraCharges` (Shinobu's Vow), compared with one to tell whether every use spends
  the last charge (one charge) or only the use the player marks `:last` (more) — a
  fact of the build, never counted down in a loop.

## Web host

- **Every element a user acts on carries a `data-action`, every element a user reads a `data-testid`**, both
  kebab-case and named for what the element is or does, never how it looks (`data-action="scratch-start"`,
  `data-testid="weapon-slot"`). They are what the end-to-end tests locate by: renaming one updates its page object
  (`tests/Loopsmith.Web.E2E/Infrastructure/Frontend/`) in the same PR.
