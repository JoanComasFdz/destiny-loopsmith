using JoanComasFdz.AssertingThat;
using Loopsmith.VsCodeExtension.E2E.Infrastructure;

namespace Loopsmith.VsCodeExtension.E2E.Scenarios.Recordings;

/// <summary>A fixture of the VS Code extension's e2e scenarios: a scenario whose asset names pass 200 characters.</summary>
public sealed class LongNameTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: A scenario whose title goes on and on past two hundred characters still finds its recording because the workflow shortens its asset names with a hash and the extension shortens them the same way
          Given VS Code is open on the workspace
        """)]
    public async Task AScenarioWhoseTitleGoesOnAndOnPastTwoHundredCharactersStillFindsItsRecordingBecauseTheWorkflowShortensItsAssetNamesWithAHashAndTheExtensionShortensThemTheSameWay(Screen screen)
    {
        // Given
        await Workbench.WaitUntilShownAsync();
    }
}
