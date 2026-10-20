using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Utility Kickstart's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class UtilityKickstartTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Utility Kickstart spends Armor Charge on class ability energy when its last charge is used
          # Rule mods-class-item-utility-kickstart-1: Class ability (last charge) ➜ Utility Kickstart equipped and Armor Charge on you ➜ Class ability energy (16.2% | 21.7% | 28.4% | 34.4% | 38.4% | 41.3% | 43.4% | 45% by charges from 2), ends Armor Charge (spends all)
          Given the build "utility-kickstart-empowered-finish-marksmans-dodge" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #1: its Armor Charge is what #2 spends; Marksman's Dodge has one charge, so every dodge is its last
          When I tap "Marksman's Dodge" in the trigger panel
          Then card #2's heading is "#2 Class ability"
          And "Utility Kickstart" fires "spends Armor Charge x1 → +16.2% class ability energy"
          And its BUFFS show none
        """)]
    public async Task UtilityKickstartSpendsArmorChargeOnClassAbilityEnergyWhenItsLastChargeIsUsed(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("utility-kickstart-empowered-finish-marksmans-dodge.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var dodged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#2 Class ability")
            .Fires(new ArmorMod.UtilityKickstart(), "spends Armor Charge x1 → +16.2% class ability energy")
            .ShowsNone(new StateGroup.Buffs());
    }
}
