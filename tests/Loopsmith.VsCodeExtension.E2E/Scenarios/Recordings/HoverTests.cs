using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>The hover on a scenario's method name: each screen's GIF, and links that play its MP4s.</summary>
public sealed class HoverTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: The hover shows each screen's recording and plays its MP4
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          And "Scenarios/Starting/StartFromScratchTests.cs" is open
          When I rest the mouse on "StartFromScratchOpensAnArcHunterWithNothingEquipped"
          Then the hover's title is "▶ Start from scratch opens an Arc Hunter with nothing equipped"
          And it reads "PR #109 · V1 · StartFromScratchTests"
          And it shows "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif" labelled "Desktop" and "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif" labelled "Mobile", side by side
          And it links "▶ Play desktop MP4", "▶ Play mobile MP4" and "Open in e2e player ↗"
          And "Open in e2e player ↗" goes to "https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4"
          When I tap "▶ Play mobile MP4" in the hover
          Then the panel "Recording · StartFromScratchOpensAnArcHunterWithNothingEquipped" shows beside the code
          And its tabs are "Desktop" and "Mobile", "Mobile" selected
          And it plays "pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.mp4"
        """)]
    public async Task TheHoverShowsEachScreensRecordingAndPlaysItsMP4(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilCodeLensReadAsync();

        // When
        await Workbench.Editor.HoverMethodNameAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Hover.WaitUntilShownAsync();

        // Then
        var hover = await Workbench.Hover.ReadAsync();
        Asserting.That(hover)
            .HasTitle("▶ Start from scratch opens an Arc Hunter with nothing equipped")
            .Reads("PR #109 · V1 · StartFromScratchTests")
            .ShowsGifsSideBySide(("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.gif", "Desktop"), ("pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-mobile.gif", "Mobile"))
            .Links("▶ Play desktop MP4", "▶ Play mobile MP4", "Open in e2e player ↗")
            .LinksTo("Open in e2e player ↗", "https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-109-v1-start-from-scratch-opens-an-arc-hunter-with-nothing-equipped-desktop.mp4");

        // When
        await Workbench.Hover.ClickLinkAsync("▶ Play mobile MP4");
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
        Scenario: The hover of a recording without MP4s links none
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "UnrelentingTests.cs" from PR #101 · V1, as GIFs only
          And "Scenarios/Loop/Weapons/UnrelentingTests.cs" is open
          When I rest the mouse on "UnrelentingHealsForSureOn3QuickKills"
          Then the hover reads "PR #101 · V1 · UnrelentingTests"
          And it shows "pr-101-v1-unrelenting-heals-for-sure-on3-quick-kills-desktop.gif" labelled "Desktop" and "pr-101-v1-unrelenting-heals-for-sure-on3-quick-kills-mobile.gif" labelled "Mobile", side by side
          And it has no links
        """)]
    public async Task TheHoverOfARecordingWithoutMP4sLinksNone(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.Unrelenting(), new Recording(101, 1, HasMp4s: false));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.Unrelenting());
        await Workbench.Editor.WaitUntilCodeLensReadAsync();

        // When
        await Workbench.Editor.HoverMethodNameAsync(new ScenarioFile.Unrelenting());
        await Workbench.Hover.WaitUntilShownAsync();

        // Then
        var hover = await Workbench.Hover.ReadAsync();
        Asserting.That(hover)
            .Reads("PR #101 · V1 · UnrelentingTests")
            .ShowsGifsSideBySide(
                ("pr-101-v1-unrelenting-heals-for-sure-on3-quick-kills-desktop.gif", "Desktop"),
                ("pr-101-v1-unrelenting-heals-for-sure-on3-quick-kills-mobile.gif", "Mobile"))
            .HasNoLinks();
    }
}
