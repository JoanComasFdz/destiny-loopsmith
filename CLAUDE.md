# Loopsmith — instructions for Claude

Loopsmith describes **cause and effect** in a Destiny 2 build; it doesn't simulate the game
([docs/ADRs.md](docs/ADRs.md) D1, [docs/requirements.md](docs/requirements.md)). Compose a build (DIM-like),
then step through its gameplay loop and see every outcome each trigger fires, which element caused
it, what it unlocks next and what doesn't work together. Numbers are facts shown with their outcome;
the player declares what only play decides: a threshold reached ("Bolt Charge at max",
"Combination Blow at max"), a buff ended, how many enemies an action hits.
**The product is the loop the user designs** (`*.loop.yaml`: save, share, replay,
analyse), judged by its order of triggers: does each step get what it needs from the steps before
it, and does the order repeat.
.NET 10 / C# 14. The main UI is the Blazor WebAssembly designer, live at
https://joancomasfdz.github.io/destiny-loopsmith/, and the only host a player uses (ADRs D38; the developer tools under
`tools/` are hosts too, D39).
Open items: [docs/backlog.md](docs/backlog.md).

## Binding rules
- **[docs/CODING_RULES.md](docs/CODING_RULES.md) is binding and wins over everything** (vertical
  slices, kernel-only dependencies, impure → pure → impure with one pure-or-impure
  statement per line, verb-named functions, immutable records, Dunet DUs, Vogen value
  objects at the boundary, own Optional/Result/Unit, no exceptions as control flow).
  [docs/ADRs.md](docs/ADRs.md) explains why and never overrides.
- The architecture tests (`tests/Loopsmith.Core.Tests/Architecture/`) guard slice
  boundaries, that only `SourceFetching` touches files (and only to read), and that the hosts
  call only `Orchestration` and the kernel — never weaken them to make code fit; fix the code.
- **Cause and effect** (ADRs D1): the state is what is present (a buff on you, a debuff on the pack,
  a pickup on the ground) and what the player declared; it holds no number. Show a source's number as
  a fact with its outcome, never added up; the player declares when a threshold is reached or a buff
  ends (`max:`, `end:` — D3).
- **Unknown is data**: never invent a game number. Use `"?"` in YAML; the engine shows
  "?", never 0.
- Never commit Compendium snapshots (`snapshots/` is gitignored — licensing, see README).

## Build & test
```bash
dotnet build Loopsmith.slnx          # TreatWarningsAsErrors is on
dotnet test                          # unit, golden and architecture tests (the e2e scenarios skip without E2E_URL / E2E_VSCODE_URL)
node --test src/Loopsmith.VsCodeExtension/test/*.test.js   # the VS Code extension's unit tests
tools/e2e/record.sh --no-video Starting   # e2e scenarios in Chromium, unrecorded: the fast run while developing (Recordings: the extension's)
tools/e2e/record.sh --changed-since origin/main   # recorded → out/<domain>/<scenario>-<screen>.gif + .mp4 + out/scenarios.json (right before a push)
dotnet test --filter RepoFileValidationTests   # every e2e build and loop fixture file opens against the rules (names the file and the error)
LOOPSMITH_COMPENDIUM_DIR=$PWD/snapshots/compendium/<date> dotnet test --filter CompendiumCheckTests   # the rules' citations and numbers against a Compendium snapshot (skipped without it)
dotnet run --project src/Loopsmith.Web          # the loop designer in the browser
```
Cloud sessions: `.claude/hooks/session-start.sh` installs `dotnet-sdk-10.0` from Ubuntu's
repos (Microsoft's download hosts are blocked by the network policy), restores, installs the
Python packages of the Compendium download (`requests`, `beautifulsoup4`), and installs the manifest extractor's PyYAML
(`python3-yaml`) and `ffmpeg` (turns the e2e recordings into GIFs and MP4s) when they are missing.

## Where things live
- `src/Loopsmith.Functional/` — `Optional`, `Result`, `Unit` (namespace `Loopsmith.Functional`; Dunet unions,
  nothing else): the coding building blocks, a project of its own so a tool can use them without Core.
  **Core is the main app's domain and business logic**: a tool under `tools/` references Core only when it uses that
  (the rule viewer does); a development tool that doesn't references only `Loopsmith.Functional`, or nothing, and still
  follows [docs/CODING_RULES.md](docs/CODING_RULES.md) ("The host owns no logic").
- `src/Loopsmith.Core/` — kernel (`Domain`, `Phrasing`, `Causality`) and
  slices (`SourceFetching`, `RuleParsing`, `BuildParsing`, `LoopFiles`, `BuildComposition`,
  `StepPlaying` (plays steps, and a loop's passes until it repeats), `LoopAnalyzing` (judges a played loop by its
  order of triggers), `BuildExplanation`, `TraceRendering`,
  `ManifestJoining` (Bungie's full manifest,
  read in the browser, → the excerpt of what an imported build's or loop's other items need, and the list of every weapon, armor
  piece and armor mod the pickers offer, with each piece's mod sockets; the browser saves the manifest and that list between visits, by the manifest's version), `StatLoading` (a Bungie login's
  answers → the player's armor → the build's stats, added up like DIM), `CompendiumReading` (a Compendium snapshot's tabs → the
  check of the rules' citations and numbers against it, with `tools/compendium/aliases.yaml`; run by a test only),
  `Orchestration`).
- `src/Loopsmith.Web/` — Blazor WebAssembly loop designer (host only; calls
  `Orchestration.LoopDesigning`). Deployed to GitHub Pages by `.github/workflows/pages.yml`;
  every PR that can change the site (not only docs, tooling or tests) gets a preview at `…/pr-preview/pr-<n>/` (`pr-preview.yml`)
  ([docs/hosting.md](docs/hosting.md)). Home offers: continue designing, start from scratch (an Arc Hunter
  with nothing equipped), import a `build.yaml`, import a loop. No example builds or loops, and no DIM link (both removed:
  [docs/example-loops.md](docs/example-loops.md)).
- **The product is the designed loop**: `*.loop.yaml`, self-contained (embeds its build) —
  format, share links and analysis in [docs/loop-format.md](docs/loop-format.md).
- `rules/` — authored causality YAML; format + engine semantics in
  [docs/rule-format.md](docs/rule-format.md). Grouped by class, then subclass, then area (`hunter/arc/melees.yaml`), with
  what every class gets on a subclass in `all-classes/<subclass>/` and the rest by where it comes from (`mods/`, `weapons/`,
  `keywords/`…): an element's kind, class and affinity name its file, and `RuleFileGroupingTests` holds it there. Every rule
  and passive has an `id` (`combination-blow-2`); its full name is its file's path then that id
  (`hunter-arc-melees-combination-blow-2`), what the rule viewer, the e2e rule tests and their recordings are named by
  (ADRs D40). `rules/manifest.yaml` is generated (the Bungie manifest's
  names, descriptions, icons, slots, perks and armor sets for every hash in the rules and in `tools/manifest/test-items.yaml`
  (the weapons, armor and mods the Core tests equip that no rule names), and the seven artifacts with all their perks): rerun
  `tools/manifest/` after adding a hash to either ([tools/manifest/README.md](tools/manifest/README.md)), never edit it.
- [docs/example-loops.md](docs/example-loops.md) — the example builds and loops (`builds/`), the DIM link and the
  `build-extractor` agent, all removed: what they were, how their tests looked, how example loops could come back, the
  agent's definition verbatim; the last commit holding them is `d56905d`. Creator transcripts are third-party content:
  keep them local (`transcript.txt`, gitignored), never commit them.
- `tools/rule-viewer/` — the rule viewer (ADRs D39): `dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer`
  writes `out/rule-viewer/arc-hunter-rules.html` and `.json`, every Arc Hunter rule as one flow, the list of the atomic e2e
  scenarios to write; `-- --rule <rule name>` writes that one rule's flows (any rule in `rules/`, whatever its folder) as `out/rule-viewer/<rule name>.png` instead
  (Playwright's Chromium; `--theme dark` draws it dark, as the PRs' e2e posts and the `loopsmith-task` session show it). `Loopsmith.RuleViewer` is the host (its page's parts are components in `Page/`);
  `Loopsmith.RuleViewer.Core` its pure logic (the rules → a subclass's dependency graph, with three scopes: the subclass,
  + what it needs from outside, + what builds on it → its rules: every rule and passive as one flow, trigger ➜ what must
  be in place ➜ what it gives, one per trigger when a rule names several), using only Core's kernel and `Orchestration`;
  `Loopsmith.RuleViewer.Tests` its unit, golden and architecture tests ([tools/rule-viewer/README.md](tools/rule-viewer/README.md)).
  `tools/rule-viewer/prototype/` is the page the owner designed, kept as the reference the tool reproduces.
- `src/Loopsmith.VsCodeExtension/` — the VS Code extension (plain JavaScript, no build, no dependencies): a CodeLens, a
  play button and a panel beside the code show each e2e scenario's recordings from its release (`prs-resources`, or a rule
  scenario's `rules-<group>-resources`), in vscode.dev
  and desktop VS Code; its `.vsix` is published with the site in `vscode-extension/` by `Loopsmith.Web.csproj`, and
  vscode.dev installs it from a local container (its `Dockerfile`: vscode.dev fetches only from `localhost` and its own
  hosts) ([docs/hosting.md](docs/hosting.md) "The VS Code extension", its [README](src/Loopsmith.VsCodeExtension/README.md)).
- `.devcontainer/` — the dev container for a GitHub Codespace or desktop VS Code's Dev Containers: one numbered script
  per feature (.NET 10, ffmpeg, the Python packages of the Compendium download and the manifest extractor, Playwright's Chromium, zsh, tmux), run by
  `postCreateCommand.sh`, and the VS Code extension installed from its source each time VS Code attaches
  (`7-install-e2e-recordings-extension.sh`) ([.devcontainer/README.md](.devcontainer/README.md)).
- `tests/Loopsmith.Core.Tests/` — unit, golden (`Golden/*.verified.txt`) and architecture tests; the docs' ```yaml examples
  are read by tests from the docs themselves, through a marker comment on the line above each fence
  (`<!-- <TestClass> reads the next yaml block … -->`, or `<!-- abridged: no test reads the next yaml block -->`; `Support/DocExamples.cs`).
- `tests/Loopsmith.Web.E2E/` — end-to-end scenarios of the web app (Playwright), one per Gherkin, one GIF per screen via `tools/e2e/record.sh`;
  `Infrastructure/` (BaseE2E, page objects, vocabulary, assertions), `Scenarios/<domain>/<Topic>Tests.cs` and
  `Rules/<Class>/<Subclass>/<Area>/<Rule>/<Rule>Tests.cs` (one scenario per rule claim, mirroring `rules/`; each class in a
  folder of its own with its fixtures, its namespace the domain folder's; `RuleScenarioCoverageTests` fails a rule file that has
  rule scenarios but lacks one for a claim); what it
  shares with the extension's e2e project (Gherkin, `[TestDescription]`, screens, naming) is `tests/Loopsmith.E2E.Shared`. Binding
  rules: its [E2E-TEST-CODE-RULES.md](tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md), not
  [docs/CODING_RULES.md](docs/CODING_RULES.md); how to run, isolation and the cache: its [README](tests/Loopsmith.Web.E2E/README.md). Playwright is pinned
  (`Directory.Packages.props`) to the cloud image's Chromium (`/opt/pw-browsers/chromium-1194` → 1.56.0; never
  `playwright install`): if the image's Chromium changes, bump the pin to match. The scenarios never reach bungie.net
  except to fill `${E2E_MANIFEST_CACHE:-~/.cache/loopsmith/bungie-manifest}` once, and read the manifest version
  pinned as `ManifestContentId` in `src/Loopsmith.Web/wwwroot/appsettings.json` (bumping it downloads the new version
  once); the cache is Bungie's data, never committed.
- `tests/Loopsmith.VsCodeExtension.E2E/` — end-to-end scenarios of the VS Code extension, one per Gherkin, played in VS
  Code for the web (`@vscode/test-web`, pinned in its `vscode-web/package.json` with the VS Code build; `record.sh` serves
  it, `npm ci` once, its build cached in `${E2E_VSCODE_CACHE:-~/.cache/loopsmith/vscode-web}`), desktop only, GitHub
  faked; their GIFs post on the PR like the web app's. Binding rules: its
  [E2E-TEST-CODE-RULES.md](tests/Loopsmith.VsCodeExtension.E2E/E2E-TEST-CODE-RULES.md); how to run: its
  [README](tests/Loopsmith.VsCodeExtension.E2E/README.md).
- [docs/given-when-then-test-rules.md](docs/given-when-then-test-rules.md) — how to write a Gherkin, click by click:
  every acceptance criterion about how the app behaves is one, and every Gherkin is one e2e scenario (its
  `[TestDescription]`), played on 🖥️ desktop and/or 📱 mobile (`[PlayInDesktop]` / `[PlayInMobile]`). It holds what
  every Gherkin shares; its two children add what each kind does differently:
  [loop scenarios](docs/given-when-then-loop-scenarios.md) (`Scenarios/`, what a PR changes; worked example
  `tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/EnemyCountTests.cs`) and
  [rule scenarios](docs/given-when-then-rule-scenarios.md) (`Rules/`, one per claim of a rule, grouped like `rules/`;
  worked example `tests/Loopsmith.Web.E2E/Rules/Hunter/Arc/Aspects/LethalCurrent/LethalCurrentTests.cs`).
- `docs/requirements.md` — the requirements, starting with the level of abstraction (what Loopsmith
  does and doesn't), then FR-1…FR-13 with their status, the non-functional requirements and what is
  out of scope.
- `.github/workflows/` — `ci.yml` (build + test on every PR that changes what `dotnet` reads, and every
  push to `main`; on a PR that changes the site it also publishes the web app and hands it to `pr-preview.yml`, so a
  PR is built once), `pages.yml`, `pr-preview.yml` (deploys that site; removes it when the PR closes), `e2e-videos.yml` (records nothing: the session
  records the PR's added or changed e2e scenarios and pushes their GIFs, MP4s and rules' flow PNGs to a throwaway `e2e-gifs/pr-<n>-<sha>` branch
  with `tools/e2e/push-gifs.sh`; the workflow uploads them to their pre-release (a rule scenario's, under
  `tests/Loopsmith.Web.E2E/Rules/<…>/`, to its rule file's `rules-<…>-resources`, created on its first upload and keeping
  each rule's latest recording; any other to `prs-resources`), posts them on the PR as
  one comment per version (V1, V2…; each GIF with a "▶ Play MP4" link to the site's `e2e-player.html`; a new one collapses
  the earlier ones into a closed `<details>` each and hides them as Outdated) and deletes the
  branch; when the PR closes, keeps its newest version's GIFs and MP4s in `prs-resources`,
  deletes the older ones, keeps each rule's latest recording in the rule releases (a merged PR's replace closed PRs'; one
  closed without merging loses its own) and shrinks the posts whose recordings went to one line; [docs/hosting.md](docs/hosting.md) "E2E videos on pull requests"); `tools/web/prepare-pages.sh` readies a published site for Pages.
- `tools/compendium/` — Destiny Data Compendium download (`sheet_dump.py`; the owner runs it with
  `get-compendium.ps1` / `get-compendium.sh` / `get-compendium-docker.sh`; see its README). The
  snapshot lives in `snapshots/compendium/<date>/`: gitignored, so **a new session doesn't have it**.
  A session downloads it itself (docs.google.com answers a session, 2026-10-13; the session-start hook
  installs its Python packages): `python3 tools/compendium/sheet_dump.py <sheet URL from
  get-compendium.sh> snapshots/compendium/<today> --no-images` (about a minute); if that fails, ask
  the owner for the zip. Cite rows as
  `compendium/<date>/<Tab>#<row>` ([docs/rule-format.md](docs/rule-format.md), Provenance);
  paraphrase, never copy its text.

## How we work
- **Branch → PR → merge.** Never push to `main` directly. One branch per piece of work, started
  from the latest `main` and named for that work — don't keep stacking unrelated work on an
  unmerged branch. Every PR that changes what `dotnet` reads runs CI, and every one that can change the site gets a web
  preview (Agent workflow, step 4) at `…/pr-preview/pr-<n>/` (link commented on the PR); merging redeploys the live site.
  The owner merges. A session opens a pull request when asked, or when it works a task
  (`/loopsmith-task`, Agent workflow below).
- **`gh-pages` is generated** by `pages.yml` and `pr-preview.yml` (ruleset: no deletion, no
  force push). Don't commit to it except a deliberate cleanup; never add "require PR/status
  checks" rules to it (the workflows push straight to it).
- **Verify web changes in a browser, not just tests:** `dotnet publish src/Loopsmith.Web -c Release -o <dir>`,
  `tools/web/prepare-pages.sh <dir>/wwwroot /destiny-loopsmith/ --root`, serve the parent of a
  `destiny-loopsmith/` copy with `python3 -m http.server`, drive it with Playwright using the
  pre-installed Chromium (`/opt/pw-browsers`; never `playwright install`), check the console.
- **Snapshot tests (Verify):** review `*.received.txt`, then rename to `*.verified.txt`.
  Verify.XunitV3 stays on 32.0.1 (33.x needs a licence decision — ADRs D26).
- **Parallel subagents:** define the contract first (Domain types + a doc), then give each
  agent its own git worktree (`git worktree add /home/user/wt/<name> -b wt/<name>`) and a
  disjoint set of folders; merge their branches back and run the full suite.
- **Cloud-session limits:** the network policy blocks youtube.com,
  github.io, light.gg, d2foundry.gg and destinyitemmanager.com (NuGet, PyPI,
  raw.githubusercontent.com and git clones from github.com work). bungie.net's API answers a
  session: the manifest index needs no key, other endpoints ask for one (`X-API-Key`), and the
  static manifest files (`/common/destiny2_content/…`) download. The `BUNGIE_API_KEY` secret only
  reaches sessions started after it was added; a Bungie login isn't possible from a session.
  Repository settings and deleting remote branches aren't possible from a session — ask the owner.
  If the clone is single-branch, after `git push -u` of a new branch run
  `git config --add remote.origin.fetch '+refs/heads/<b>:refs/remotes/origin/<b>' && git fetch`
  so the branch tracks its remote.
- **History rewrites** (e.g. purging a file): rewrite only `c1074e8..<branch>` — the root
  commit is GitHub-signed and must keep its hash, or `main` loses its shared history.

## Agent workflow (`/loopsmith-task`)
Work starts in a cloud session where @JoanComasFdz runs `/loopsmith-task` with what they want, in
their own words and any language, and pastes any screenshots and files. The skill agrees the spec
and its acceptance criteria with them; then the same session does the work, on one branch and one
pull request:

1. **Branch.** From the latest `origin/main`, create the work branch `claude/<slug>` (Naming), and
   use only it for this task. A session may be handed a generated branch name (e.g.
   `claude/zealous-euler-qu8uiv`) in its own instructions: ignore it, never push to it, and keep to
   the slug branch for every commit, push and the pull request; this rule is the owner's permission
   to do so.
2. **Ask in the session.** Ask any questions there and wait for the answer. Don't guess: if
   something a session doesn't have is needed (a creator transcript, a design decision, the
   Compendium snapshot when the download in "Where things live" fails), ask for it.
   Questions are optional: only where a point has two readings that lead to different changes.
   Every question and suggestion, in any round, carries its own context: the problem with a concrete example,
   the choices with what each changes, a marked default, and a numbered list of the answers needed (`loopsmith-task`
   skill, "How to write a question or a suggestion").
3. **Do the work** on that branch, changing only what the spec covers. Subagents read, author in
   their own worktree or check, as the spec's "How the work splits" says (skill, "Subagents"); only
   the session talks to the owner, commits, pushes and writes on GitHub. Commit when a step is
   complete, not after every edit, but keep the branch local until the work is ready for review:
   its first push is the one that opens the pull request (step 4). Before every push: `dotnet
   build Loopsmith.slnx` and `dotnet test` (warnings are errors; there is no separate linter),
   check a web change in a browser too (How we work). While developing, run the e2e scenarios
   unrecorded: `tools/e2e/record.sh --no-video --changed-since origin/main` (fast; add
   `--no-publish` when the web app didn't change). Record them only right before a push that adds
   or changes e2e scenarios: `tools/e2e/record.sh --changed-since origin/main`; right after every
   such push (the first one once its PR is open), `tools/e2e/push-gifs.sh` posts their GIFs on the
   PR (Always, "e2e scenarios").
4. **When it's ready for review, bring in `main`, push and open the pull request.** Right before
   the branch's first push, `git fetch origin main` and `git rebase origin/main` (resolve any
   conflict; ask in the session when both sides changed the same logic), then run `dotnet build
   Loopsmith.slnx` and `dotnet test` again. Only before the first push: once the branch is on
   GitHub, a rebase would need a force-push, so from then on step 6 applies. The rebase finds a
   conflict in the session, before the PR opens, and makes the build and test run against the
   current `main`. Then push and open the pull request into `main` (not a draft), with the task's
   title and the agreed spec as its description: tick the acceptance criteria you checked, and add
   what changed and how to check it on the preview at `…/pr-preview/pr-<PR number>/`. A Gherkin is
   one line there, its title and test class, never its full text: the PR's e2e comment shows it in
   full with its GIFs ([docs/given-when-then-test-rules.md](docs/given-when-then-test-rules.md), section 8). A PR gets
   **no build** when every path it changes (a rename's old path too) is a Markdown file or under
   `docs/`, `.github/`, `.claude/`, `.devcontainer/`, `tools/e2e/`, `tools/compendium/` or
   `tools/manifest/` (nothing `dotnet build` or `dotnet test` reads: `ci.yml` skips `build`), and
   **no preview** when every path is on that list or under `tests/` (nothing the published site is
   built from: `ci.yml` neither publishes it nor calls `pr-preview.yml`; a test or e2e fixture change still builds and tests). A
   PR with no preview leaves the preview out of its description and messages. Then rename
   the session: put `#<PR number> ` in front of its current title ("D2 damage resistance calculation" →
   "#59 D2 damage resistance calculation"), reading it with the claude-code-remote `get_session`
   (no `session_id`) and writing it with `set_session_title`; skip it if the title already starts
   with that prefix. If the tools aren't available or the rename fails, say so in the session and
   carry on: it never blocks the PR. Watch it (auto-fix) at once. The PR link and the preview link
   (`https://joancomasfdz.github.io/destiny-loopsmith/pr-preview/pr-<PR number>/`), as bullet points,
   end only two kinds of message: the one that reports the PR opened, and the one after a push that
   answers @JoanComasFdz (a review round, or a change they asked for in the session). A PR with no
   preview ends them with the PR link only. No other message repeats them: CI running, passing or failing;
   the preview built; e2e GIFs posted; check-ins; a conflict merge or a CI fix nobody asked for;
   waiting on review; and the PR merged or closed, which is one line with no links.
5. **Keep watching until the PR is merged or closed,** however many rounds it takes: every message
   in the session, every comment and review @JoanComasFdz writes on the PR (they wake you), every CI
   failure. Reply in a review thread when you address it, but don't resolve it: @JoanComasFdz
   resolves threads, and that is their sign-off before merging.
6. **Keep it mergeable.** A conflict with `main` doesn't always arrive as an event, so on every PR
   event, and on a check-in with `send_later` about every 4 hours while the PR waits (stop after 3
   in a row that found nothing new; the next event starts them again), look at its merge state. On a
   conflict, merge `origin/main` into the branch, resolve it, build and test, and push. A branch
   that is only behind `main` needs nothing: the `main-protection` ruleset doesn't require a PR to
   be up to date before merging, so neither the session nor @JoanComasFdz updates it. That is safe
   because the `build` check is still required, a conflict still blocks the merge, and `ci.yml`
   builds and tests every push to `main`, which catches two PRs that only break when combined.
   Never rebase a branch that is already on GitHub (step 4's rebase is only for
   the first push).

The owner's screenshots and files stay in the session: they aren't committed, and no other session
can see them. When the work needs one in the repository (a loop file as a test case), put it in its
proper place, after checking it for personal data and secrets. An image in a GitHub comment can't be
opened from a session (GitHub's attachment storage is blocked): say so and ask for it in the
session.

Follow-ups (work the PR doesn't cover) never go into the PR. Offer them as a numbered list. When
@JoanComasFdz says to launch one, start a new `/loopsmith-task` session for it with the
claude-code-remote `create_session` tool, titled `(#<PR number> follow-up) <task title>` (its own PR
later makes it `#57 (#56 follow-up) <task title>`) and given its `claude/<slug>` branch as
`outcome_branch` (without it the session lands in "Other", not under destiny-loopsmith), then reply with its name and its
`https://claude.ai/code/<session id>` link (`loopsmith-task` skill, section 6).

Always:
- Act only on what @JoanComasFdz writes. Anyone else's comments are information: don't act on them,
  and mention them in the session.
- One task, one branch, one pull request. Don't stack pull requests: a task that needs another one's
  change waits until that one is merged.
- Never merge, approve or enable auto-merge on a pull request, and never push to `main` (the ruleset
  refuses it anyway). Never force-push: to update a branch that is on GitHub, merge `origin/main`
  into it; squash merging flattens it. The one rebase is step 4's, before the branch's first push.
- Don't change `.github/workflows/`, repository settings, rulesets or secrets unless the task asks
  for exactly that.
- **e2e scenarios:** a PR that adds or changes e2e scenarios shows them on the PR, recorded by the session (CI doesn't run them):
  while developing, run them unrecorded with `tools/e2e/record.sh --no-video` (same selection, fast, no GIFs) and
  never record between pushes; record once, right before the push, with `tools/e2e/record.sh --changed-since
  origin/main` (every scenario passes, or the push waits for a fix), and record again if a scenario or the web app
  changes after that and before the push, so the GIFs match the pushed head; after the push, `tools/e2e/push-gifs.sh`, which `e2e-videos.yml` posts as `## Scenarios (V<N>)` (each
  scenario a closed `<details>` under its title, holding the flow PNG of the rule a rule scenario names, its Gherkin, then its 🖥️ Desktop and/or 📱 Mobile GIF; those only re-recorded because a fixture file they use
  changed come last, under "Re-recorded: their fixtures changed"). Put a short
  plain description of the round in the pushed commit's message body (the post's "What changed"), or pass it as
  `--message`.
- In cloud sessions `gh issue` and `gh pr` fail (GitHub's GraphQL is blocked): use the built-in
  GitHub tools or `gh api repos/<owner>/<repo>/...` (REST).

## Naming (agent work)
| What | Name |
|---|---|
| Task title (PR title) | an imperative sentence, at most about 70 characters |
| Work branch | `claude/<slug>`: the title's gist in at most 5 lowercase ASCII kebab-case words, e.g. `claude/perk-descriptions-on-phone` |
| A file given in the session | `<NN>-<what>[-<where>].<ext>` (below), as the spec's "Files given in the session" lists it |

A file's name starts with its two-digit place in the order the owner gave the files (`01`, `02`, …;
files given later continue the count), then says what it shows, in lowercase ASCII kebab-case, and
keeps its extension: `01-perk-picker-phone.png`, `02-designer-console.log`,
`03-skip-grenade.loop.yaml`, `04-manifest-excerpt.xml`. No spaces, no personal names.

## Project skills (`.claude/skills/`)
- `loopsmith-task` — what the owner wants, in any language, with their screenshots and files → a
  spec with acceptance criteria agreed with them → the work, in the same session, on one pull
  request watched until it is merged (Agent workflow); each follow-up it offers is launched, on
  request, as its own session.

## Project subagents (`.claude/agents/`)
- `rule-author` — Compendium/Clarity/guide text → rules YAML (provenance, no invented numbers).
- `conventions-reviewer` — reviews a diff against docs/CODING_RULES.md.

## Source priority for mechanics
Compendium snapshot / Clarity (game data) > the user's notes > a creator's claim.
Record every disagreement in a comment next to the element in its rule file, citing each source in a `source` form
([docs/rule-format.md](docs/rule-format.md), Provenance).
