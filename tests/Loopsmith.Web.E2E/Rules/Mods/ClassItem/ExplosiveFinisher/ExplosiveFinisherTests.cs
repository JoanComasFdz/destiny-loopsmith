using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Explosive Finisher's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class ExplosiveFinisherTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Explosive Finisher spends x3 Armor Charge on grenade energy
          # Rule mods-class-item-explosive-finisher-1: Finisher ➜ Explosive Finisher equipped and Armor Charge 3+ stacks ➜ Grenade energy (70% | 80% | 90% | 100% by stacks spent from x3), ends Armor Charge (spends all)
          Given the build "explosive-finisher-empowered-finish" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #1: its Armor Charge is what #2 declares at max
          When I tap "at max (x3)" on "Armor Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Armor Charge at max (x3)"
          And its BUFFS show only "Armor Charge x3 (at max)"
          # keeping step #2: its x3 Armor Charge is what #3 spends
          When I tap "Finisher" in the trigger panel
          Then card #3's heading is "#3 Finisher"
          And "Explosive Finisher" fires "spends Armor Charge x3 → +70% grenade energy"
          And its BUFFS show none
        """)]
    public async Task ExplosiveFinisherSpendsX3ArmorChargeOnGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explosive-finisher-empowered-finish.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.ArmorCharge(), "at max (x3)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var maxed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(maxed)
            .HasHeading("#2 Armor Charge at max (x3)")
            .Shows(new StateGroup.Buffs(), "Armor Charge x3 (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var finished = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#3 Finisher")
            .Fires(new ArmorMod.ExplosiveFinisher(), "spends Armor Charge x3 → +70% grenade energy")
            .ShowsNone(new StateGroup.Buffs());
    }
}
