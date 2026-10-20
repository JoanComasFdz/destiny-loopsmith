using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Snapload Finisher's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class SnaploadFinisherTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Snapload Finisher spends one Armor Charge to reload your weapons
          # Rule mods-class-item-snapload-finisher-1: Finisher ➜ Snapload Finisher equipped and Armor Charge on you ➜ ends Armor Charge (spends 1)
          Given the build "snapload-finisher-empowered-finish" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #1: its Armor Charge is what #2 spends
          When I tap "Finisher" in the trigger panel
          Then card #2's heading is "#2 Finisher"
          # The reload isn't modelled (ADRs D1): the x1 it spends is what the card shows.
          And "Snapload Finisher" fires "spends Armor Charge x1"
          And its BUFFS show none
        """)]
    public async Task SnaploadFinisherSpendsOneArmorChargeToReloadYourWeapons(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("snapload-finisher-empowered-finish.build.yaml");
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
        // The reload isn't modelled (ADRs D1): the x1 it spends is what the card shows.
        var finished = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#2 Finisher")
            .Fires(new ArmorMod.SnaploadFinisher(), "spends Armor Charge x1")
            .ShowsNone(new StateGroup.Buffs());
    }
}
