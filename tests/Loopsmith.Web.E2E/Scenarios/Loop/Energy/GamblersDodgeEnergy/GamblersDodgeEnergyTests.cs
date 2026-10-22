using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>
/// Gambler's Dodge's melee energy in a loop (ADRs D48): 2% per Melee point (doubled within 15 m of an enemy, which
/// Loopsmith assumes), with no chunk scalar, from the build's Melee stat; unknown without one.
/// </summary>
public sealed class GamblersDodgeEnergyTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Gambler's Dodge refills the melee by 2% per Melee point
          Given the build "energy-combination-blow-gamblers-dodge-60-melee" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #1's ENERGY reads "Melee 100% → 0%"
          # keeping step #1: its punch spent the melee #2's dodge gives back
          When I tap "Gambler's Dodge" in the trigger panel
          # 60 Melee × 2% (within 15 m, which Loopsmith assumes) = 120%: a full charge.
          Then card #2's ENERGY reads "Melee 0% → 100%"
          And its ENERGY reads "Class ability 100% → 0%"
          # keeping step #2: its dodge refilled the melee #3 punches with
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #3 has no "NOT READY" badge
          And its ENERGY reads "Melee 100% → 0%"
        """)]
    public async Task GamblersDodgeRefillsTheMeleeBy2PerMeleePoint(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-combination-blow-gamblers-dodge-60-melee.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var punched = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(punched).ShowsEnergy(new AbilitySlot.Melee(), "100%", "0%");

        // When
        // keeping step #1: its punch spent the melee #2's dodge gives back
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GamblersDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // 60 Melee × 2% (within 15 m, which Loopsmith assumes) = 120%: a full charge.
        var dodged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(dodged)
            .ShowsEnergy(new AbilitySlot.Melee(), "0%", "100%")
            .ShowsEnergy(new AbilitySlot.ClassAbility(), "100%", "0%");

        // When
        // keeping step #2: its dodge refilled the melee #3 punches with
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var again = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(again)
            .IsNotFlagged()
            .ShowsEnergy(new AbilitySlot.Melee(), "100%", "0%");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Without a Melee stat Gambler's Dodge's refill is unknown
          Given the build "energy-combination-blow-gamblers-dodge" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #1's ENERGY reads "Melee 100% → 0%"
          # keeping step #1: its punch spent the melee #2's dodge gives back
          When I tap "Gambler's Dodge" in the trigger panel
          # The dodge's melee energy goes by the Melee stat, and the build gives none: "?", never 0.
          Then card #2's ENERGY reads "Melee 0% → ?"
          # keeping step #2: what its dodge gave back is all #3's melee has
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #3 has a "MAY NOT BE READY" badge
          And it shows the note "Melee may not be ready: ? before #3"
        """)]
    public async Task WithoutAMeleeStatGamblersDodgesRefillIsUnknown(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-combination-blow-gamblers-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var punched = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(punched).ShowsEnergy(new AbilitySlot.Melee(), "100%", "0%");

        // When
        // keeping step #1: its punch spent the melee #2's dodge gives back
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GamblersDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // The dodge's melee energy goes by the Melee stat, and the build gives none: "?", never 0.
        var dodged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(dodged).ShowsEnergy(new AbilitySlot.Melee(), "0%", "?");

        // When
        // keeping step #2: what its dodge gave back is all #3's melee has
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var again = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(again)
            .IsFlagged("MAY NOT BE READY")
            .HasNote("Melee may not be ready: ? before #3");
    }
}
