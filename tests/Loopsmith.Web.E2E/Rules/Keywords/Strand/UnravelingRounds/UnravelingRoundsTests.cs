using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Strand;

/// <summary>Unraveling Rounds's rules (rules/keywords/strand.yaml), one scenario per rule claim.</summary>
public sealed class UnravelingRoundsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unraveling Rounds's Strand weapon hit unravels the target
          # Rule keywords-strand-unraveling-rounds-1: Strand weapon hit ➜ Unraveling Rounds on you ➜ Unravel
          Given the build "unraveling-rounds-unraveling-orbs-festival-flight" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its WEAPONS show only "Unraveling Rounds"
          # keeping step #2: its Unraveling Rounds is what #3's hit uses
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Festival Flight (🥊 hit)"
          And "Unraveling Rounds" fires "Unravel target"
          And its DEBUFFS show only "Blind" and "Unravel"
        """)]
    public async Task UnravelingRoundssStrandWeaponHitUnravelsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unraveling-rounds-unraveling-orbs-festival-flight.build.yaml");
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
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Weapons(), "Unraveling Rounds");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var hit = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#3 Festival Flight (🥊 hit)")
            .Fires(new Keyword.UnravelingRounds(), "Unravel target")
            .Shows(new StateGroup.Debuffs(), "Blind", "Unravel");
    }
}
