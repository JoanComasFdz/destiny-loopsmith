using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>
/// Elemental Charge's rules (rules/mods/armor.yaml), one scenario per rule claim. Spark of Discharge drops the Ionic Trace it picks up: the designer
/// picks a trace up in the step that drops it (a trace homes in on you).
/// </summary>
public sealed class ElementalChargeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Elemental Charge's Ionic Trace pickup may give an Armor Charge
          # Rule mods-armor-elemental-charge-1: Pick up Ionic Trace ➜ Elemental Charge equipped ➜ Armor Charge (chance)
          Given the build "elemental-charge-crisis-inverted-spark-of-discharge" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "3 · Spark of Discharge"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 3)"
          # The trace the kills drop homes in on you: the same step picks it up.
          And on "📍 Picked up Ionic Trace" "Elemental Charge" fires "+1 Armor Charge (chance)"
          And its BUFFS show only "Bolt Charge" and "Armor Charge +1 (chance)"
        """)]
    public async Task ElementalChargesIonicTracePickupMayGiveAnArmorCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elemental-charge-crisis-inverted-spark-of-discharge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Spark of Discharge");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The trace the kills drop homes in on you: the same step picks it up.
        var pickedUp = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#1 Crisis Inverted (💀 kill 3)")
            .FiresOn("📍 Picked up Ionic Trace", new ArmorMod.ElementalCharge(), "+1 Armor Charge (chance)")
            .Shows(new StateGroup.Buffs(), "Bolt Charge", "Armor Charge +1 (chance)");
    }
}
