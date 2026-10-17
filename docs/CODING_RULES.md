# Coding rules (binding)

These are **requirements, not suggestions** — the plan and the code must follow
them. They are the house style for **Loopsmith** (a .NET 10 / C# 14 project),
carried over verbatim in substance from the owner's house style and adapted only
in names and examples.

This file is the canonical, citable home for the coding rules — it is binding, and
it wins over any other document. They bind the C# code under `src/` and
`tests/Loopsmith.Core.Tests/`. The *reasoning* behind the cross-cutting choices
is recorded in **[`ADRs.md`](ADRs.md)**; an ADR explains why, it never overrides
what this file says. The domain model these rules produce is the code
itself, under `src/Loopsmith.Core/Domain/`.

## Architecture

- **Vertical Slice Architecture is the top-level driver.** Group by *feature*,
  never by kind. A behaviour change touches one slice, not a layer. The slices
  live under `src/Loopsmith.Core/` (`SourceFetching`, `RuleParsing`,
  `BuildParsing`, `LoopFiles` (reads and writes loop files, and writes the build
  files they embed), `BuildComposition`, `StepPlaying` (plays steps against the
  state, and a loop's passes until it repeats), `LoopAnalyzing` (judges a played loop
  by its order of triggers), `BuildExplanation`, `TraceRendering`,
  `ReportComparison`, `LoadoutImporting` (a DIM link → a build), `ManifestJoining` (Bungie's full
  manifest → the excerpt some hashes need), `StatLoading` (a Bungie login's answers → the player's armor
  → the build's stats), `Orchestration`).
- **Feature slices depend only on the shared kernel** (`Domain` / `Functional` /
  `Phrasing` / `Causality`) — never sideways on each other. The only cross-slice
  dependency is `Orchestration` → every slice. An **architecture test** enforces
  these boundaries in CI.
  The kernel has two small non-`Domain` modules for behaviour that is shared across
  slices but is not pure value arithmetic: **`Phrasing`** (plain-English descriptions
  of Domain values plus the styled-text model every renderer and the host use) and
  **`Causality`** (what a trigger *means*: trigger/damage-source matching, shared by
  `StepPlaying` and the slices that read a rule's trigger). Both are guarded by architecture tests and may
  not depend on any feature slice.
- **Build elements are data, not code.** A new aspect, fragment, mod, exotic,
  artifact perk or weapon trait is **one YAML entry** under `rules/` — never an
  edit to a shared classifier or a dispatch `switch`. A new trigger or outcome
  kind is one Dunet case, and the compiler flags every `Match` that must handle it.
- **`Domain` holds data + pure value arithmetic only** — no feature behaviour, no
  outward dependency, never depends on a feature slice or on `System.IO`. The
  allowed exceptions are pure, dependency-free value arithmetic (e.g.
  `GameValueArithmetic.ResolveForCopies`, `ToDamageType`, the `Functional`
  primitives' generic `Map`/`Bind`) and pure reads of its records that slices share
  (`BuildReading`, `GlossaryReading`, `GameStateReading`: what a build, the glossary
  or a state holds).
- **The host owns no logic.** `Loopsmith.Web` (Blazor WebAssembly), the only host,
  holds UI state and calls the pure `Orchestration` API (`LoopDesigning`); its side
  effects live in `Hosting/`: the browser's (clipboard, downloads, confirm, picked files,
  the URL) in `BrowserInterop` (and `wwwroot/js/loopsmith.js`), its HTTP requests (DIM
  shares, the Bungie login and manifest) in `DimLoadoutFetching`, `BungieAccount` and
  `BungieManifestJoining`. Any other host (an HTTP API, say) would do the same.

- **The end-to-end tests have their own rules.** `tests/Loopsmith.Web.E2E` follows
  [`E2E-TEST-CODE-RULES.md`](../tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md), not this file.

## Web host

- **Every element a user acts on carries a `data-action`, every element a user reads a `data-testid`**, both
  kebab-case and named for what the element is or does, never how it looks (`data-action="scratch-start"`,
  `data-testid="weapon-slot"`). They are what the end-to-end tests locate by: renaming one updates its page object
  (`tests/Loopsmith.Web.E2E/Infrastructure/Frontend/`) in the same PR.

## Functional design

- **Pure/impure split — strict `impure → pure → impure` sandwich.** The
  boundary (`SourceFetching`) reads files (rules, builds, loop files, and the
  sources it ingests: the manifest, the Compendium snapshot, Clarity); the host
  performs every other side effect (the browser's). Everything between is pure. Read at the start, compute in the middle,
  write at the end.
- **Never mix or nest pure and impure calls on one line.** A statement is either
  pure or impure, never both — don't wrap an impure call around a pure one (or
  vice versa). Split them into separate statements, introducing a named
  intermediate even when it feels redundant:

  ```csharp
  // No — pure Export nested inside impure CopyText
  await Browser.CopyTextAsync(LoopDesigning.ExportLoop(session));

  // Yes — one line pure, the next impure
  var text = LoopDesigning.ExportLoop(session);   // pure
  await Browser.CopyTextAsync(text);              // impure
  ```

  The extra line/variable is an accepted cost. It makes the sandwich readable
  vertically — you can scan straight down and see exactly where each pure↔impure
  boundary is — and it makes those boundary jumps easy to step through when
  debugging. Impure code (the host's handlers, a test's file reads) marks each line
  `// pure` or `// impure`.
- **Pure functions return values, the host acts on them.** `Orchestration` returns
  what the host shows or saves (a `Result`, a session, a text); the host's handler
  performs the side effect in its own `// impure` line ([ADRs D38](ADRs.md)).
- **Honest, total signatures** via our own `Optional<T>`, `Result<T, F>`, and
  `Unit` (in `Functional/`) — defined as Dunet DUs with `Map`/`Bind`/`Match` as
  extension methods. **No third-party FP library.** No partial functions, no
  null-as-absence, no exceptions-as-control-flow across the pure core.
- **Railway-oriented (ROP)** for the end-to-end flow: the pipeline composes
  `Result`-returning steps (read rules → parse catalog → parse build → validate
  → play) that stay on the success track and short-circuit onto the failure
  track at the first blocking error — no explicit app-state object.
- **Typestate when call order matters** (validation must precede playing). It
  composes with ROP: `ValidatedBuild` is constructible only by
  `BuildComposition.BuildValidation.ValidateBuild` and is the success-track
  payload `StepPlaying` consumes, so "play before validation" cannot happen
  (architecture test on `newobj` / `<Clone>$`).
- **Pure state machine** is reserved for genuinely stateful situations with more
  than two states — `GameState`, played by `StepPlaying`, the slice that plays steps
  ((state, step) → (state, fired)), *not* the linear app flow, which is a
  railway, not a machine.

## Domain modelling

- **Data ⟂ behaviour.** Records hold data; behaviour lives in static / extension
  functions over the data. No methods carrying logic on the records themselves.
- **Functions are actions — name them with a verb.** Every function/method name
  must contain a verb describing what it does (`ResolveAction`, `CascadeEvent`,
  `DescribeTrigger`, `ParseRuleFile`, `ComputeCoverage`) — never a bare noun
  (`Phase`, `Cascade`, `Rules`). A name that is only a noun denotes a *thing*, so
  it belongs to data, not to a function. Two idioms satisfy the rule without an
  explicit action word: boolean predicates may lead with `Is`/`Has`/`Can`
  (`IsTriggeredBy`, `HasBuff`), and conversion/factory members may use
  `Parse`/`ToX`/`FromX` / the union-case constructor names (`ToDamageType`,
  `Optional.Some`, `Optional.FromNullable`).
- **Naming clash resolved:** the game's consequences (energy, buffs, orbs,
  debuffs) are called **`Outcome`**, never `Effect`.
- **Immutability everywhere.** All domain types are immutable `record`s.
- **Domain via discriminated unions, not inheritance.** DUs are generated with
  **Dunet**. Behaviour is static/extension functions over the DU that
  pattern-match internally — never `ISomething` + two classes. Plain enums are
  fine for closed tags that carry no data (`AbilityKind`, `Severity`).
- **Make illegal states unrepresentable.** Each DU case carries exactly its own
  figures (e.g. `Trigger.KillDebuffed(Via, TargetHas)` vs
  `Trigger.KillOfTier(Via, Tier)` — not `Kill(via, Tier?, TargetHas?)`). Prefer a
  data-carrying case over a flat "tag + optionals" shape.
- **Value objects via Vogen** (`ElementId`, `StatusId`, `PickupId`, `SummonId`,
  `ItemHash`, `CatalogVersion`, `Seconds`, `TargetCount`, `StackCount`,
  `StatValue`, `SnapshotDate`), constructed and validated **at the boundary**
  (`TryFrom` in the parsing slices) — once inside the pure core a value object is
  known-valid.
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
- **Add a named failure/`Severity` case only when the app branches on it** (to
  recover or take a different path), never merely to carry a message. IO whose only
  outcome is "show the user what went wrong" uses `Result<_, string>`; the acted-on
  distinction is `Severity = Blocking | Warning | Info` with a `string` message.

## Practical API gotchas

- **Prefer static direct calls** over interfaces/delegates.
- **Dunet unions have no static factories.** Construct cases with `new` —
  `new Result<T, F>.Ok(x)` / `new Result<T, F>.Error(e)` — and use the
  `Optional.Some<T>(x)` / `Optional.None<T>()` helpers (not `Result.Ok(...)` /
  `Optional<T>.None()`).
- **`Match` needs an explicit type argument when the branches return different
  types**, e.g. `value.Match<string>(...)`.
- **A Dunet case can't share its name with one of its own properties**
  (`Summon(SummonId Summon)` fails with CS8866) — rename the case
  (`SpawnSummon`, `Summoned`).
- **Records holding `ImmutableArray` don't compare by content.** Where identity
  matters (cascade dedupe), derive an explicit key (`EventCascading.ToEventKey`).
- **Boundary error policy.** Catch *foreseeable* exceptions at the boundary
  (missing/denied file, unreadable YAML) and wrap them in `Result<_, string>`.
  Let *unrecoverable* exceptions (e.g. `OutOfMemoryException`) bubble to the app.
