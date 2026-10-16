using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>Reading the release again, for a recording posted after the file was opened.</summary>
public sealed class RefreshTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Refreshing finds a recording posted after the file was opened
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds no recording of "ReloadTests.cs"
          And "Scenarios/Loop/Weapons/ReloadTests.cs" is open, its CodeLens reading "No recording yet"
          When the release gets the recordings of "ReloadTests.cs" from PR #113 · V1
          And I run "Loopsmith: Refresh e2e recordings" from the Command Palette
          Then the CodeLens above its method reads "Play desktop recording", "Play mobile recording" and "PR #113 · V1"
        """)]
    public async Task RefreshingFindsARecordingPostedAfterTheFileWasOpened(Screen screen)
    {
        // Given
        GitHub.Release.HasNoRecordingOf(new ScenarioFile.Reload());
        await Workbench.Explorer.OpenAsync(new ScenarioFile.Reload());
        await Workbench.Editor.WaitUntilCodeLensShowsAsync("No recording yet");

        // When
        GitHub.Release.AddRecordingsOf(new ScenarioFile.Reload(), new Recording(113, 1));
        await Workbench.CommandPalette.RunAsync("Loopsmith: Refresh e2e recordings");
        await Workbench.Editor.WaitUntilCodeLensShowsAsync("PR #113 · V1");

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("Play desktop recording", "Play mobile recording", "PR #113 · V1");
    }
}
