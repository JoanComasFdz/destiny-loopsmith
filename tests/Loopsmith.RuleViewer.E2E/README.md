# Loopsmith.RuleViewer.E2E — end-to-end tests of the rule viewer's page

The e2e scenarios of the rule viewer's page (`tools/rule-viewer`, ADRs D39): each Gherkin about how its tree of the rules
folder behaves (an arrow folds an item, a checkbox shows or hides its flows and dashes the items above it, a name
selects it and brings its flows into view, the tree beside the flows or above them on a phone) is one scenario here,
played in Chromium on a desktop and a phone. Their code rules: [E2E-TEST-CODE-RULES.md](E2E-TEST-CODE-RULES.md). A PR
that adds or changes them shows them on the PR like the web app's: their Gherkins and their GIFs and MP4s, posted by
the "E2E videos" workflow ([docs/hosting.md](../../docs/hosting.md), "E2E videos on pull requests").

## Run and record

`tools/e2e/record.sh` runs them with the other projects' scenarios (its header says how to choose):

```bash
tools/e2e/record.sh --no-video RuleTree               # this project's scenarios, unrecorded: the fast run while developing
tools/e2e/record.sh --no-video TreeTickingTests       # one class
tools/e2e/record.sh --changed-since origin/main       # recorded: out/RuleTree/<scenario>-<screen>.gif + .mp4, right before a push
```

For this project it:
1. draws the rules page from `Fixtures/rules/` with the rule viewer
   (`dotnet run --project tools/rule-viewer/Loopsmith.RuleViewer -- --rules <Fixtures/rules> --out out/.rule-viewer-page`);
2. serves that folder with the static server (`tools/e2e/Loopsmith.StaticServer`);
3. runs the scenarios with `E2E_RULE_VIEWER_URL` set to its `rules.html` (and `E2E_VIDEO_DIR` when recording), and makes
   each run's GIF and MP4 like the web app's, trimmed from when the page showed.

Without `E2E_RULE_VIEWER_URL` the scenarios are skipped, so `dotnet test` runs only their checks that need no browser
(`ScenarioShapeTests`, `FixtureRulesTests`). CI doesn't run them: its runner has no Chromium. The page's HTML has its
own tests in `tools/rule-viewer/Loopsmith.RuleViewer.Tests` (`RuleTreePageTests`).

## Where tests go

`Scenarios/<domain folder>/<Topic>Tests.cs`, by what the reader does with the page: `RuleTree/` (the tree:
`TreeFoldingTests`, `TreeTickingTests`, `TreeSelectingTests`, `TreeLayoutTests`). The domain folder is the namespace
after `.Scenarios.`, and the folder their GIFs go in under `out/`: a new one must not take another project's domain
folder's name (`Starting`, `Loop`, `Recordings`, …).

## The fixture rules

The page is drawn from `Fixtures/rules/`, never from `rules/`, so a rule added to the repository changes no scenario:
copies of `rules/hunter/arc/melees.yaml`, `rules/hunter/arc/supers.yaml` and `rules/mods/arms.yaml` (files that point
to no element in another file), and `rules/glossary.yaml`. They draw the tree `rules/` → `hunter/` → `arc/` →
`melees.yaml` (Combination Blow, Disorienting Blow), `supers.yaml` (Arc Staff, Gathering Storm, Storm's Edge), and
`mods/` → `arms.yaml` (its arm mods), with enough flows below each item for its name to bring it to the top of the view.
`FixtureRulesTests` fails when they no longer open against the rules' format; copy the file again from `rules/` (or
fix the copy) when it does. They are never kept in step with `rules/` otherwise.

## Isolation and parallelism

Each run gets its own browser context and only reads the page, so runs share nothing and run in any order, in parallel
(`xunit.runner.json`).
