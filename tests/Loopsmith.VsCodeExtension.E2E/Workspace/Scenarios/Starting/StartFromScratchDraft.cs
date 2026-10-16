using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>A fixture of the VS Code extension's e2e scenarios: not a scenario file (its name doesn't end in "Tests").</summary>
public sealed class StartFromScratchDraft : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Start from scratch opens an Arc Hunter with nothing equipped
          Given the app is open on Home
          When I tap "Start from scratch"
          Then the loop has no steps
        """)]
    public async Task StartFromScratchOpensAnArcHunterWithNothingEquipped(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
    }

    private static string DescribeStart() => "not a scenario: no [TestDescription]";
}
