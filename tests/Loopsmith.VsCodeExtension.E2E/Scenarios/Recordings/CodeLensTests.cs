using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Vocabulary;
using Loopsmith.VsCodeExtension.E2E.Infrastructure.Workbench;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>The CodeLens above a scenario: its newest recording's screens, or that it has none.</summary>
public sealed class CodeLensTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: The CodeLens plays each screen of the newest recording
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "StartFromScratchTests.cs" from PR #104 · V1 and PR #109 · V1
          When I open "Scenarios/Starting/StartFromScratchTests.cs" in the explorer
          Then the CodeLens above its scenario reads "Play desktop recording", "Play mobile recording" and "PR #109 · V1"
          And no other line has a CodeLens
        """)]
    public async Task TheCodeLensPlaysEachScreenOfTheNewestRecording(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.StartFromScratch(), new Recording(104, 1), new Recording(109, 1));

        // When
        await Workbench.Explorer.OpenAsync(new ScenarioFile.StartFromScratch());
        await Workbench.Editor.WaitUntilCodeLensReadAsync();

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens)
            .ReadsAboveTheScenario("Play desktop recording", "Play mobile recording", "PR #109 · V1")
            .HasNoOtherCodeLens();
    }

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
        await Workbench.Editor.WaitUntilCodeLensReadAsync();

        // Then
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("No recording yet");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A scenario named past two hundred characters finds its recording
          # Desktop only: the extension runs in VS Code on a desktop.
          Given the release holds the recordings of "LongNameTests.cs" from PR #112 · V3
          When I open "Scenarios/Recordings/LongNameTests.cs" in the explorer
          # Its assets' names would pass 200 characters: the workflow cuts them and ends them with a hash of the GIF's path.
          Then the CodeLens above its scenario reads "Play desktop recording" and "PR #112 · V3"
        """)]
    public async Task AScenarioNamedPastTwoHundredCharactersFindsItsRecording(Screen screen)
    {
        // Given
        GitHub.Release.AddRecordingsOf(new ScenarioFile.LongName(), new Recording(112, 3));

        // When
        await Workbench.Explorer.OpenAsync(new ScenarioFile.LongName());
        await Workbench.Editor.WaitUntilCodeLensReadAsync();

        // Then
        // Its assets' names would pass 200 characters: the workflow cuts them and ends them with a hash of the GIF's path.
        var codeLens = await Workbench.Editor.ReadCodeLensAsync();
        Asserting.That(codeLens).ReadsAboveTheScenario("Play desktop recording", "PR #112 · V3");
    }
}
