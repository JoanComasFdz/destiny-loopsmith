# End-to-end test code rules of the VS Code extension (binding)

These rules are binding for `tests/Loopsmith.VsCodeExtension.E2E`, the e2e scenarios of the VS Code extension
(`src/Loopsmith.VsCodeExtension`). [docs/CODING_RULES.md](../../docs/CODING_RULES.md) doesn't apply to this project;
these do.

They are the web app's e2e rules, [tests/Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md](../Loopsmith.Web.E2E/E2E-TEST-CODE-RULES.md),
rule for rule: one Gherkin is one scenario, played on the screens its tags name, reading like the Gherkin line by line,
with everything that isn't the scenario in `Infrastructure/`. What both projects share (a Gherkin and its
`[TestDescription]`, `Screen` and `[PlayInDesktop]`, `ScenarioNaming`, the record next to a video, the recording's
pointer and click ring) is `tests/Loopsmith.E2E.Shared`. This file says only where this project differs.

## A full example

```csharp
using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

public sealed class CodeLensTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A scenario with no recording says so
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds no recording of "ReloadTests.cs"
          When I open "Scenarios/Loop/Weapons/ReloadTests.cs" in the explorer
          Then the CodeLens above its scenario reads "No recording yet"
        """)]
    public async Task AScenarioWithNoRecordingSaysSo(Screen screen)
    {
        // Given
        GitHub.Release.HasNoRecordingOf(new ScenarioFile.Reload());

        // When
        await Workbench.Explorer.OpenAsync(new ScenarioFile.Reload());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("No recording yet");
    }
}
```

## a. Test shape

As the web rules' rule a, except:
- every scenario is played on the desktop only: `[Theory, PlayInDesktop]`, its Gherkin tagged `@desktop` with the `#`
  line `# Desktop only: the extension runs in VS Code on a desktop.` right under its `Scenario:` line (the extension is
  for VS Code on a desktop, in a browser or the app). `ScenarioShapeTests` fails any other screen or tag;
- the body calls only `Workbench` (VS Code as the user sees it), `GitHub` (the fake GitHub the extension reads) and
  `Asserting.That`.

## b. BaseE2E

`Infrastructure/BaseE2E.cs` owns everything that isn't the scenario, as the web rules' rule b, except:
- the skip: a scenario is skipped unless `E2E_VSCODE_URL` points at VS Code for the web with the extension, opened on
  `Workspace/` (`tools/e2e/record.sh` serves it: README, "Run and record");
- the window is the desktop's 1280×800, with a mouse, its video the same size;
- **GitHub is faked** (`Infrastructure/Fakes/FakeGitHub.cs`), per scenario, never reached: the context's requests to
  `api.github.com` (the `prs-resources` release, then its assets) and to `github.com/…/releases/download/…` (a GIF or
  an MP4) are answered from the release the scenario fills. Its release starts empty;
- it opens VS Code and waits until its workbench is shown, noting when, so `record.sh` trims VS Code's loading screen
  off the video (the record's `homeShownSeconds`);
- it gives the scenario its `Workbench` and its `GitHub`.

## c. Workbench

`Infrastructure/Workbench/` is an object model of VS Code as the user sees it, with the web rules' rule c's members
(Act, Wait, Read, `ReadLayoutAsync`), each going through `OnScreen`:
- `Workbench.Explorer` (the tree of `Workspace/`), `Workbench.Editor` (the scenario file's editor: its CodeLens, a
  click on a line, its title bar's buttons), `Workbench.RecordingPanel` (the extension's panel, a webview),
  `Workbench.CommandPalette`;
- **locators:** role + accessible name where VS Code gives one (a tree item, a tab, a command); the extension's own panel
  by its `data-testid` and `data-action` attributes (`extension.js`, `writePanel`); and VS Code's own class names, which
  have no role to find them by, only in `Infrastructure/Workbench/VsCodeDom.cs`, one constant each, so a VS Code update
  that renames one changes one line;
- **the panel is a webview:** its page sits in two frames drawn over its editor group
  (`FrameLocator(VsCodeDom.Webview).FrameLocator(VsCodeDom.WebviewContent)`). The frames keep their mouse events from
  the page, so `OnScreen` draws the pointer's last stretch and the click's ring itself for an action in the panel
  (`inPanel: true`, through `RecordingOverlay`'s hooks);
- VS Code's workbench doesn't scroll, so `OnScreen` shows no box first; it paces a recorded scenario
  (`--action-gap`) and glides the pointer to each action's target, as in the web project.

## d. Assertions

As the web rules' rule d: one `<Type>Assertions` class next to each snapshot, in the extension's words
(`ReadsAboveTheScenario(…)`, `HasNoOtherCodeLens()`, `ShowsGifsSideBySide(…)`, `Plays("pr-109-v1-….mp4")`,
`IsBesideTheCode()`).

## e. Vocabulary

`Infrastructure/Vocabulary/` holds what a scenario sets up: `ScenarioFile` (a Dunet union, one case per scenario file of
`Workspace/`: its path, its scenario's method, domain folder and screens, the only place they live) and `Recording`
(`new Recording(109, 1)`: "PR #109 · V1", GIFs and MP4s; `HasMp4s: false` for GIFs only). An asset's name is never
written by hand in the fake: `FakeRelease` names each run as the workflow does (`AssetNaming`; `AssetNamingTests` checks it against names
`tools/e2e/post-scenarios.py`'s `name_asset` wrote). A Gherkin's quoted asset name or URL is the quoted string itself.

## f. The workspace

`Workspace/` is the folder VS Code for the web opens: its `.vscode/settings.json` (a dark theme, no welcome page, no
compact folders, a larger font for the videos) and the scenario files the scenarios open (`Workspace/Scenarios/`,
never compiled: the project leaves them out). A scenario needing a new kind of file adds one there, and its
`ScenarioFile` case.

## g. Isolation

As the web rules' rule g: each scenario has its own context (VS Code's state is in its IndexedDB) and its own fake
GitHub; the workspace is only read.

## h. Adding a test

As the web rules' rule h, with `Scenarios/<domain folder>/` of this project (today `Recordings`), `Workspace/` for its
files, and `tools/e2e/record.sh --no-video <its class>` (it serves VS Code for the web instead of the web app).

## i. From a Gherkin to a scenario

| In the Gherkin | In the scenario |
|---|---|
| `@desktop` | `[Theory, PlayInDesktop]` |
| `Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1` | `GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1))` |
| `… from PR #101 · V1, as GIFs only` | `new Recording(101, 1, HasMp4s: false)` |
| `Given the release holds no recording of "ReloadTests.cs"` | `GitHub.Release.HasNoRecordingOf(new ScenarioFile.Reload())` |
| `"Scenarios/Starting/StartFromScratchTests.cs" is open` / `I open … in the explorer` | `Explorer.OpenAsync(new ScenarioFile.StartFromScratch())`, `Editor.WaitUntilRecordingsShownAsync()` |
| `I tap "Play desktop recording" in its CodeLens` | `Editor.ClickCodeLensAsync("Play desktop recording")`, `RecordingPanel.WaitUntilPlayingAsync()` |
| `I tap its "Mobile" tab` / `I tap "PR #104 · V1" in "Recorded in"` | `RecordingPanel.ClickTabAsync("Mobile")` / `RecordingPanel.ClickVersionAsync("PR #104 · V1")`, then `RecordingPanel.WaitUntilPlayingAsync()` |
| `"…" is open, its CodeLens reading "PR #109 · V1"` | `Explorer.OpenAsync(…)`, `Editor.WaitUntilCodeLensShowsAsync("PR #109 · V1")` |
| `I run "Loopsmith: Refresh e2e recordings" from the Command Palette` | `CommandPalette.RunAsync("Loopsmith: Refresh e2e recordings")` |
| `the CodeLens above its method reads "…", "…" and "…"` / `no other line has a CodeLens` | `Editor.ReadCodeLensAsync()`, `ReadsAboveTheScenario("…", "…", "…")` / `HasNoOtherCodeLens()` |
| `no line has a CodeLens` (a file that gets none) | When ends with `Editor.WaitUntilCodeLensSettledAsync()` (VS Code draws a CodeLens a moment after a file opens); `ReadCodeLensAsync()`, `HasNoCodeLens()` |
| `it plays "….mp4", with its controls` | `Plays("….mp4")`, `HasVideoControls()` |
| `the panel "Recording · …" shows beside the code` | `RecordingPanel.ReadLayoutAsync()`, `IsBesideTheCode()`; `RecordingPanel.ReadAsync()`, `HasEditorTab("Recording · …")` |
| `its tabs are "Desktop" and "Mobile", "Desktop" selected` / `it plays "….mp4"` | `HasTabSelected("Desktop", "Desktop", "Mobile")` / `Plays("….mp4")` |
