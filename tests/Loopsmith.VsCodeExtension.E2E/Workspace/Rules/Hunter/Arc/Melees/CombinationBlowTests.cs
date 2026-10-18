using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Melees;

/// <summary>
/// A fixture of the VS Code extension's e2e scenarios: a rule scenario (rules/hunter/arc/melees.yaml), whose recordings
/// are in its rule group's release, rules-hunter-arc-melees-resources.
/// </summary>
public sealed class CombinationBlowTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Combination Blow stacks on a powered melee kill
          Given the build "combination-blow" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          Then "Combination Blow" shows a stack
        """)]
    public async Task CombinationBlowStacksOnAPoweredMeleeKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("combination-blow.build.yaml");
    }
}
