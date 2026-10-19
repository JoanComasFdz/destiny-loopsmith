using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.OrbOfPower;

/// <summary>Orb of Power's rules (rules/keywords/orb-of-power.yaml), one scenario per rule claim.</summary>
public sealed class OrbOfPowerTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Picking up an Orb of Power gives Armor Charge and Super energy
          # Rule keywords-orb-of-power-orb-of-power-1: Pick up Orb of Power ➜ Super energy (?), Armor Charge
          Given the build "orb-of-power-disorienting-blow-spark-of-amplitude" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And it reads "📍 Picked up Orb of Power"
          # The Super energy an orb gives depends on what dropped it: unknown, so "?".
          And "Orb of Power" fires "+1 Armor Charge" and "+?% super energy"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x1"
          And its GROUND shows none
        """)]
    public async Task PickingUpAnOrbOfPowerGivesArmorChargeAndSuperEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("orb-of-power-disorienting-blow-spark-of-amplitude.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 4 s · Spark of Amplitude");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Melee (💀 kill 2)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // The Super energy an orb gives depends on what dropped it: unknown, so "?".
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Reads("📍 Picked up Orb of Power")
            .Fires(new Keyword.OrbOfPower(), "+1 Armor Charge", "+?% super energy")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x1")
            .ShowsNone(new StateGroup.Ground());
    }
}
