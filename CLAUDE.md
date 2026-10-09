# Loopsmith — instructions for Claude

Destiny 2 build-loop engine: compose a build (DIM-like), then step through its gameplay
loop and see every outcome each trigger fires, which element caused it, and what it
unlocks next. .NET 10 / C# 14, CLI host first, API later.

## Binding rules
- **[CONVENTIONS.md](CONVENTIONS.md) is binding and wins over everything** (vertical
  slices, kernel-only dependencies, impure → pure → impure with one pure-or-impure
  statement per line, verb-named functions, immutable records, Dunet DUs, Vogen value
  objects at the boundary, own Optional/Result/Unit, no exceptions as control flow).
  [ADRs.md](ADRs.md) explains why and never overrides.
- The architecture tests (`tests/Loopsmith.Core.Tests/Architecture/`) guard slice
  boundaries — never weaken them to make code fit; fix the code.
- **Unknown is data**: never invent a game number. Use `"?"` in YAML; the engine shows
  "?" and doesn't apply it.
- Never commit Compendium snapshots (`snapshots/` is gitignored — licensing, see README).

## Build & test
```bash
dotnet build Loopsmith.slnx          # TreatWarningsAsErrors is on
dotnet test                          # unit, golden and architecture tests
dotnet run --project src/Loopsmith.Cli -- explain builds/skip-grenade-hunter/build.yaml
dotnet run --project src/Loopsmith.Web          # the loop designer in the browser
```
Cloud sessions: `.claude/hooks/session-start.sh` installs `dotnet-sdk-10.0` from Ubuntu's
repos (Microsoft's download hosts are blocked by the network policy) and restores.

## Where things live
- `src/Loopsmith.Core/` — kernel (`Domain`, `Functional`, `Phrasing`, `Causality`) and
  slices (`SourceFetching`, `RuleParsing`, `BuildParsing`, `LoopFiles`, `BuildComposition`,
  `Simulation`, `BuildExplanation`, `LoopGraphing`, `TraceRendering`, `ReportComparison`,
  `Orchestration`).
- `src/Loopsmith.Cli/` — host only: argv → shell → execute effects.
- `src/Loopsmith.Web/` — Blazor WebAssembly loop designer (host only; calls
  `Orchestration.LoopDesigning`). Deployed to GitHub Pages by `.github/workflows/pages.yml`
  ([docs/hosting.md](docs/hosting.md)).
- **The product is the designed loop**: `*.loop.yaml`, self-contained (embeds its build) —
  format, share links, analysis and comparison in [docs/loop-format.md](docs/loop-format.md).
- `rules/` — authored causality YAML; format + engine semantics in
  [docs/rule-format.md](docs/rule-format.md).
- `builds/<slug>/` — `build.yaml`, the user's `note.txt`, `note-map.md` (golden-test
  mapping), `discrepancies.md`, `loops/*.loop.yaml` (designed loops). Creator transcripts
  are third-party content: keep them local as `transcript.txt` (gitignored), never commit them.
- `docs/design/loopsmith-design-v0.3.html` — the design proposal (requirements FR-1…FR-10,
  roadmap, risks).
- `tools/compendium/sheet_dump.py` — dumps the Destiny Data Compendium sheet (run locally;
  see README).

## Project subagents (`.claude/agents/`)
- `rule-author` — Compendium/Clarity/guide text → rules YAML (provenance, no invented numbers).
- `build-extractor` — video transcript / note → `builds/<slug>/build.yaml` + `note-map.md`.
- `conventions-reviewer` — reviews a diff against CONVENTIONS.md.

## Source priority for mechanics
Compendium snapshot / Clarity (game data) > the user's notes > a creator's claim.
Record every disagreement in the build's `discrepancies.md`.
