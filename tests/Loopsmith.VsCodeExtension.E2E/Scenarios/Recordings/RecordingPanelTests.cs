using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>The panel beside the code: a scenario's recording, a tab per screen, every version it was recorded in.</summary>
public sealed class RecordingPanelTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Play desktop recording opens the desktop video beside the code
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I tap "Play desktop recording" in its CodeLens
          Then the panel "Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped" shows beside the code
          And its title is "Start from scratch opens an Arc Hunter with nothing equipped"
          And it reads "StartFromScratchTests · Starting · PR #109 · V1"
          And its tabs are "Desktop" and "Mobile", "Desktop" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4", with its controls
          And "Open in e2e player ↗" goes to "https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4"
          When I tap its "Mobile" tab
          Then its tabs are "Desktop" and "Mobile", "Mobile" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4"
        """)]
    public async Task PlayDesktopRecordingOpensTheDesktopVideoBesideTheCode(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickCodeLensAsync("Play desktop recording");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var layout = await Workbench.RecordingPanel.ReadLayoutAsync();
        Asserting.That(layout).IsBesideTheCode();
        var desktop = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(desktop)
            .HasEditorTab("Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped")
            .HasTitle("Start from scratch opens an Arc Hunter with nothing equipped")
            .Reads("StartFromScratchTests · Starting · PR #109 · V1")
            .HasTabSelected("Desktop", "Desktop", "Mobile")
            .Plays("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4")
            .HasVideoControls()
            .LinksToThePlayer("https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4");

        // When
        await Workbench.RecordingPanel.ClickTabAsync("Mobile");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var mobile = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(mobile)
            .HasTabSelected("Mobile", "Desktop", "Mobile")
            .Plays("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Play mobile recording opens the panel on the mobile video
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I tap "Play mobile recording" in its CodeLens
          Then the panel "Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped" shows beside the code
          And its tabs are "Desktop" and "Mobile", "Mobile" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4"
        """)]
    public async Task PlayMobileRecordingOpensThePanelOnTheMobileVideo(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickCodeLensAsync("Play mobile recording");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var layout = await Workbench.RecordingPanel.ReadLayoutAsync();
        Asserting.That(layout).IsBesideTheCode();
        var panel = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(panel)
            .HasEditorTab("Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped")
            .HasTabSelected("Mobile", "Desktop", "Mobile")
            .Plays("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Recorded in lists every version newest first and plays the one picked
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And it holds its recordings from PR #101 · V1, as GIFs only
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I tap "Play desktop recording" in its CodeLens
          Then the panel's "Recorded in" lists "PR #109 · V1", "PR #104 · V1" and "PR #101 · V1"
          And "PR #109 · V1" is picked
          When I tap "PR #104 · V1" in "Recorded in"
          Then it reads "StartFromScratchTests · Starting · PR #104 · V1"
          And "PR #104 · V1" is picked
          And it plays "pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4"
          When I tap "PR #101 · V1" in "Recorded in"
          Then it shows the GIF "pr-101-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif"
          And it shows no "Open in e2e player ↗"
        """)]
    public async Task RecordedInListsEveryVersionNewestFirstAndPlaysTheOnePicked(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(101, 1, HasMp4s: false));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickCodeLensAsync("Play desktop recording");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var newest = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(newest)
            .ListsVersions("PR #109 · V1", "PR #104 · V1", "PR #101 · V1")
            .HasPicked("PR #109 · V1");

        // When
        await Workbench.RecordingPanel.ClickVersionAsync("PR #104 · V1");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var older = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(older)
            .Reads("StartFromScratchTests · Starting · PR #104 · V1")
            .HasPicked("PR #104 · V1")
            .Plays("pr-104-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4");

        // When
        await Workbench.RecordingPanel.ClickVersionAsync("PR #101 · V1");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var gifsOnly = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(gifsOnly)
            .ShowsGif("pr-101-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif")
            .HasNoPlayerLink();
    }
}
