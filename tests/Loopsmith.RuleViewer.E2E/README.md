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
(`ScenarioShapeTests`, `FixtureRulesTests`). CI plays them unrecorded, the same `tools/e2e/record.sh --no-video` run, on every PR that changes the rule viewer, this
project, `tests/Loopsmith.E2E.Shared`, Core, what plays them or the Playwright pin (`ci.yml`'s `rule-viewer-e2e` job,
which installs Playwright's Chromium and caches it and the icons and fonts); recording them stays the session's job. The page's HTML has its
own tests in `tools/rule-viewer/Loopsmith.RuleViewer.Tests` (`RuleTreePageTests`).

## Where tests go

`Scenarios/<domain folder>/<Topic>Tests.cs`, by what the reader does with the page: `RuleTree/` (the tree:
`TreeFoldingTests`, `TreeTickingTests`, `TreeSelectingTests`, `TreeLayoutTests`). The domain folder is the namespace
after `.Scenarios.`, and the folder their GIFs go in under `out/`: a new one must not take another project's domain
folder's name (`Starting`, `Loop`, `Recordings`, …).

## The fixture rules

The page is drawn from `Fixtures/rules/`, never from `rules/`, so a rule added to the repository changes no scenario:
copies of `rules/hunter/arc/melees.yaml`, `rules/hunter/arc/supers.yaml` and `rules/mods/arms.yaml` (files that point
to no element in another file), `rules/glossary.yaml`, and `manifest.yaml`: `rules/manifest.yaml`'s header and small
sections, and of its `items` only the lines whose hash those files or the glossary name (so each element and keyword
shows its real icon, as on the real page; without it every icon is an empty dashed square). They draw the tree `rules/` → `hunter/` → `arc/` →
`melees.yaml` (Combination Blow, Disorienting Blow), `supers.yaml` (Arc Staff, Gathering Storm, Storm's Edge), and
`mods/` → `arms.yaml` (its arm mods), with enough flows below each item for its name to bring it to the top of the view.
`FixtureRulesTests` fails when they no longer open against the rules' format; copy the file again from `rules/` (or
fix the copy; for `manifest.yaml`, take the same lines again from `rules/manifest.yaml`) when it does. They are never kept in step with `rules/` otherwise.

## Icons and fonts

The scenarios are also how the page is judged by eye: their GIFs on the PR show it as a reader sees it, with its
icons and fonts. Those come from outside (Bungie's icons, named by the manifest excerpt; Google Fonts), so
`Infrastructure/OutsideRouting.cs` answers them from a local cache, `${E2E_PAGE_CACHE:-~/.cache/loopsmith/rule-viewer-page}`,
filled once from the network (bungie.net and Google Fonts answer a cloud session) and never committed (Bungie's and
Google's files). Any other request is refused, which the page logs to its console, so the scenario fails rather than
showing a page with a missing icon.

## Isolation and parallelism

Each run gets its own browser context and only reads the page, so runs share nothing and run in any order, in parallel
(`xunit.runner.json`).
