# Loopsmith — instructions for Claude

Destiny 2 build-loop engine: compose a build (DIM-like), then step through its gameplay
loop and see every outcome each trigger fires, which element caused it, and what it
unlocks next. **The product is the loop the user designs** (`*.loop.yaml`: save, share,
replay, analyse, compare). .NET 10 / C# 14. The main UI is the Blazor WebAssembly designer,
live at https://joancomasfdz.github.io/destiny-loopsmith/; the CLI is the scripting/test host.
Open items: [docs/backlog.md](docs/backlog.md).

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
  `Orchestration.LoopDesigning`). Deployed to GitHub Pages by `.github/workflows/pages.yml`;
  every PR gets a preview at `…/pr-preview/pr-<n>/` (`pr-preview.yml`)
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

## How we work
- **Branch → PR → merge.** Never push to `main` directly. Every PR runs CI and gets a web
  preview at `…/pr-preview/pr-<n>/` (link commented on the PR); merging redeploys the live site.
  The owner merges; open PRs only when asked.
- **`gh-pages` is generated** by `pages.yml` and `pr-preview.yml` (ruleset: no deletion, no
  force push). Don't commit to it except a deliberate cleanup; never add "require PR/status
  checks" rules to it (the workflows push straight to it).
- **Verify web changes in a browser, not just tests:** `dotnet publish src/Loopsmith.Web -c Release -o <dir>`,
  `tools/web/prepare-pages.sh <dir>/wwwroot /destiny-loopsmith/ --root`, serve the parent of a
  `destiny-loopsmith/` copy with `python3 -m http.server`, drive it with Playwright using the
  pre-installed Chromium (`/opt/pw-browsers`; never `playwright install`), check the console.
- **Snapshot tests (Verify):** review `*.received.txt`, then rename to `*.verified.txt`.
  Verify.XunitV3 stays on 32.0.1 (33.x needs a licence decision — ADRs D20).
- **Parallel subagents:** define the contract first (Domain types + a doc), then give each
  agent its own git worktree (`git worktree add /home/user/wt/<name> -b wt/<name>`) and a
  disjoint set of folders; merge their branches back and run the full suite.
- **Cloud-session limits:** the network policy blocks bungie.net, docs.google.com, youtube.com,
  github.io, light.gg, d2foundry.gg and destinyitemmanager.com (NuGet, PyPI,
  raw.githubusercontent.com and git clones from github.com work). Repository settings and
  deleting remote branches aren't possible from a session — ask the owner. If the clone is
  single-branch, after `git push -u` of a new branch run
  `git config --add remote.origin.fetch '+refs/heads/<b>:refs/remotes/origin/<b>' && git fetch`
  so the branch tracks its remote.
- **History rewrites** (e.g. purging a file): rewrite only `c1074e8..<branch>` — the root
  commit is GitHub-signed and must keep its hash, or `main` loses its shared history.

## Project subagents (`.claude/agents/`)
- `rule-author` — Compendium/Clarity/guide text → rules YAML (provenance, no invented numbers).
- `build-extractor` — video transcript / note → `builds/<slug>/build.yaml` + `note-map.md`.
- `conventions-reviewer` — reviews a diff against CONVENTIONS.md.

## Source priority for mechanics
Compendium snapshot / Clarity (game data) > the user's notes > a creator's claim.
Record every disagreement in the build's `discrepancies.md`.
