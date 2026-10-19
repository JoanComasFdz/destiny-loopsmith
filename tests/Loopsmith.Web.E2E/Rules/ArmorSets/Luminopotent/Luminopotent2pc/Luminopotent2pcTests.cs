using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.Luminopotent;

/// <summary>Luminopotent's 2-piece bonus, Ionic Overclock (rules/armor-sets/luminopotent.yaml), one scenario per rule claim.</summary>
public sealed class Luminopotent2pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ionic Overclock gives fusions and heat weapons handling and reload while Amplified
          # Rule armor-sets-luminopotent-luminopotent-2pc-1: Always ➜ Ionic Overclock equipped and Amplified on you ➜ Weapon stats (handling ?, reload ?)
          Given the build "luminopotent-2pc-only" is imported, with no steps
          When I tap "What this build does" above the loop
          # A step's card shows no weapon stats: the panel names Amplified as the line's condition, and the unknown values as "?".
          Then its "While Amplified" line shows "Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload [Ionic Overclock]"
        """)]
    public async Task IonicOverclockGivesFusionsAndHeatWeaponsHandlingAndReloadWhileAmplified(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("luminopotent-2pc-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        // A step's card shows no weapon stats: the panel names Amplified as the line's condition, and the unknown values as "?".
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained).Shows("While Amplified", "Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload [Ionic Overclock]");
    }
}
