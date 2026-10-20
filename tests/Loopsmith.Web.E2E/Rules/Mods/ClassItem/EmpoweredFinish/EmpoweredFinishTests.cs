using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Empowered Finish's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class EmpoweredFinishTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Empowered Finish gives Armor Charge on a finisher without it
          # Rule mods-class-item-empowered-finish-1: Finisher ➜ Empowered Finish equipped and Armor Charge not on you ➜ Armor Charge
          Given the build "empowered-finish-alone" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And "Empowered Finish" fires "+1 Armor Charge"
          And its BUFFS show only "Armor Charge x1"
        """)]
    public async Task EmpoweredFinishGivesArmorChargeOnAFinisherWithoutIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("empowered-finish-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .Fires(new ArmorMod.EmpoweredFinish(), "+1 Armor Charge")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");
    }
}
