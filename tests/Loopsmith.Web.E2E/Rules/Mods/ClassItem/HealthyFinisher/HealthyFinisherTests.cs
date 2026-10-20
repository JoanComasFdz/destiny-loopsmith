using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Healthy Finisher's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class HealthyFinisherTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Healthy Finisher spends one Armor Charge to heal you in full
          # Rule mods-class-item-healthy-finisher-1: Finisher ➜ Healthy Finisher equipped and Armor Charge on you ➜ Health (100%), ends Armor Charge (spends 1)
          Given the build "healthy-finisher-empowered-finish" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #1: its Armor Charge is what #2 spends
          When I tap "Finisher" in the trigger panel
          Then card #2's heading is "#2 Finisher"
          And "Healthy Finisher" fires "spends Armor Charge x1" and "heals you 100%"
          And its BUFFS show none
        """)]
    public async Task HealthyFinisherSpendsOneArmorChargeToHealYouInFull(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("healthy-finisher-empowered-finish.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var finished = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#2 Finisher")
            .Fires(new ArmorMod.HealthyFinisher(), "spends Armor Charge x1", "heals you 100%")
            .ShowsNone(new StateGroup.Buffs());
    }
}
