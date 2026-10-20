using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Bomber's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class BomberTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bomber's dodge gives grenade energy
          # Rule mods-armor-bomber-1: Class ability ➜ Bomber equipped ➜ Grenade energy
          Given the build "bomber-marksmans-dodge" is imported, with no steps
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #1's heading is "#1 Class ability"
          And "Bomber" fires "+12% grenade energy"
        """)]
    public async Task BombersDodgeGivesGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bomber-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#1 Class ability")
            .Fires(new ArmorMod.Bomber(), "+12% grenade energy");
    }
}
