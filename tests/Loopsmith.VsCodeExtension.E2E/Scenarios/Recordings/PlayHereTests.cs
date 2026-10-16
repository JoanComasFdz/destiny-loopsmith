using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>
/// "Play e2e recording of this scenario": the editor's play button and the Command Palette play the scenario the cursor is
/// in, a way to play that a finger reaches (a CodeLens or a hover's link is hard to tap on a phone).
/// </summary>
public sealed class PlayHereTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: The editor's play button plays the scenario the cursor is in
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I tap the line "Given the app is open on Home"
          And I tap "Loopsmith: Play e2e recording of this scenario" in the editor's title bar
          Then the panel "Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped" shows beside the code
          And its tabs are "Desktop" and "Mobile", "Desktop" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4"
        """)]
    public async Task TheEditorsPlayButtonPlaysTheScenarioTheCursorIsIn(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickLineAsync("Given the app is open on Home");
        await Workbench.Editor.ClickTitleBarButtonAsync("Loopsmith: Play e2e recording of this scenario");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var layout = await Workbench.RecordingPanel.ReadLayoutAsync();
        Asserting.That(layout).IsBesideTheCode();
        var panel = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(panel)
            .HasEditorTab("Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped")
            .HasTabSelected("Desktop", "Desktop", "Mobile")
            .Plays("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: The Command Palette plays the scenario below the cursor when the cursor is above every scenario
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I tap the line "public sealed class StartFromScratchTests : BaseE2E"
          And I run "Loopsmith: Play e2e recording of this scenario" from the Command Palette
          Then the panel "Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped" shows beside the code
          And its tabs are "Desktop" and "Mobile", "Desktop" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4"
        """)]
    public async Task TheCommandPalettePlaysTheScenarioBelowTheCursorWhenTheCursorIsAboveEveryScenario(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickLineAsync("public sealed class StartFromScratchTests : BaseE2E");
        await Workbench.CommandPalette.RunAsync("Loopsmith: Play e2e recording of this scenario");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var layout = await Workbench.RecordingPanel.ReadLayoutAsync();
        Asserting.That(layout).IsBesideTheCode();
        var panel = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(panel)
            .HasEditorTab("Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped")
            .HasTabSelected("Desktop", "Desktop", "Mobile")
            .Plays("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4");
    }
}
