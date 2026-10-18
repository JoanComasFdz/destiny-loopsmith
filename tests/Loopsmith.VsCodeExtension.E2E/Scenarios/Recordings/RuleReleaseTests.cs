using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>
/// A rule scenario (under Rules/) finds its recordings in its rule group's release, rules-&lt;group&gt;-resources, not in
/// prs-resources.
/// </summary>
public sealed class RuleReleaseTests : BaseE2E
{
    private const string MeleesRelease = "rules-hunter-arc-melees-resources";

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A rule scenario's CodeLens plays the recording from its rule group's release
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release "rules-hunter-arc-melees-resources" holds a recording of "CombinationBlowTests.cs" made by PR #140 · V2
          And the release "prs-resources" holds no recording of it
          When I open "Rules/Hunter/Arc/Melees/CombinationBlowTests.cs" in the explorer
          Then the CodeLens above its method reads "Play desktop recording", "Play mobile recording" and "PR #140 · V2"
        """)]
    public async Task ARuleScenariosCodeLensPlaysTheRecordingFromItsRuleGroupsRelease(Screen screen)
    {
        // Given
        GitHub.ReleaseNamed(MeleesRelease).AddRecordingsOf(new ScenarioFile.CombinationBlow(), new Recording(140, 2));
        GitHub.Release.HasNoRecordingOf(new ScenarioFile.CombinationBlow());

        // When
        await Workbench.Explorer.OpenAsync(new ScenarioFile.CombinationBlow());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("Play desktop recording", "Play mobile recording", "PR #140 · V2");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A rule scenario's recording panel plays from its rule group's release
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release "rules-hunter-arc-melees-resources" holds a recording of "CombinationBlowTests.cs" made by PR #140 · V2
          And "Rules/Hunter/Arc/Melees/CombinationBlowTests.cs" is open
          When I tap "Play mobile recording" in its CodeLens
          Then the panel reads "CombinationBlowTests · Rules/Hunter/Arc/Melees · PR #140 · V2"
          And its tabs are "Desktop" and "Mobile", "Mobile" selected
          And it plays "pr-140-v2-combination-blow-stacks-on-a-powered-melee-kill-mobile.mp4" from "rules-hunter-arc-melees-resources"
          And "Open in e2e player ↗" goes to "https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-140-v2-combination-blow-stacks-on-a-powered-melee-kill-mobile.mp4&r=rules-hunter-arc-melees-resources"
        """)]
    public async Task ARuleScenariosRecordingPanelPlaysFromItsRuleGroupsRelease(Screen screen)
    {
        // Given
        GitHub.ReleaseNamed(MeleesRelease).AddRecordingsOf(new ScenarioFile.CombinationBlow(), new Recording(140, 2));
        await Workbench.Explorer.OpenAsync(new ScenarioFile.CombinationBlow());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // When
        await Workbench.Editor.ClickCodeLensAsync("Play mobile recording");
        await Workbench.RecordingPanel.WaitUntilPlayingAsync();

        // Then
        var panel = await Workbench.RecordingPanel.ReadAsync();
        Asserting.That(panel)
            .Reads("CombinationBlowTests · Rules/Hunter/Arc/Melees · PR #140 · V2")
            .HasTabSelected("Mobile", "Desktop", "Mobile")
            .Plays("pr-140-v2-combination-blow-stacks-on-a-powered-melee-kill-mobile.mp4", MeleesRelease)
            .LinksToThePlayer("https://joancomasfdz.github.io/destiny-loopsmith/e2e-player.html?v=pr-140-v2-combination-blow-stacks-on-a-powered-melee-kill-mobile.mp4&r=rules-hunter-arc-melees-resources");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A rule scenario whose release doesn't exist yet says it has no recording
          # Desktop only: the extension runs in VS Code on a desktop.
          Given GitHub has no release "rules-hunter-arc-melees-resources"
          When I open "Rules/Hunter/Arc/Melees/CombinationBlowTests.cs" in the explorer
          Then the CodeLens above its method reads "No recording yet"
        """)]
    public async Task ARuleScenarioWhoseReleaseDoesntExistYetSaysItHasNoRecording(Screen screen)
    {
        // Given
        GitHub.HasNoRelease(MeleesRelease);

        // When
        await Workbench.Explorer.OpenAsync(new ScenarioFile.CombinationBlow());
        await Workbench.Editor.WaitUntilRecordingsShownAsync();

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("No recording yet");
    }
}
