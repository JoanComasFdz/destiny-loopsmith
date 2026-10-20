using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Distribution's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class DistributionTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Distribution gives energy to every ability on a class ability
          # Rule mods-class-item-distribution-1: Class ability ➜ Distribution equipped ➜ Grenade energy (4% | 6% | 7%), Melee energy (4% | 6% | 7%), Class ability energy (4% | 6% | 7%), Super energy (2% | 3% | 4%)
          Given the build "distribution-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Distribution" fires "+4% grenade, melee and class ability energy" and "+2% super energy"
        """)]
    public async Task DistributionGivesEnergyToEveryAbilityOnAClassAbility(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("distribution-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new ArmorMod.Distribution(), "+4% grenade, melee and class ability energy", "+2% super energy");
    }
}
