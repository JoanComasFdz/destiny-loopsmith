using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>A fixture of the VS Code extension's e2e scenarios: a web scenario recorded as GIFs only.</summary>
public sealed class UnrelentingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unrelenting heals for sure on 3 quick kills
          Given the build "ogma-pr6-unrelenting" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          Then "Unrelenting" fires "heals you 65 HP"
        """)]
    public async Task UnrelentingHealsForSureOn3QuickKills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ogma-pr6-unrelenting.build.yaml");
    }
}
