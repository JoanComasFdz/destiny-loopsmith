using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>A fixture of the VS Code extension's e2e scenarios: a web scenario with no recording yet.</summary>
public sealed class ReloadTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reload stands apart from the count and takes none
          Given the build "festival-flight-one-for-all" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then "🔄 reload" stands apart from the count
        """)]
    public async Task ReloadStandsApartFromTheCountAndTakesNone(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all.build.yaml");
    }
}
