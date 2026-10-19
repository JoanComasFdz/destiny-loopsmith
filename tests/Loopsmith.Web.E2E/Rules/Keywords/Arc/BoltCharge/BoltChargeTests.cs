using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Arc;

/// <summary>Bolt Charge's rules (rules/keywords/arc.yaml), one scenario per rule claim.</summary>
public sealed class BoltChargeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolt Charge gives melee energy for every stack gained
          # Rule keywords-arc-bolt-charge-2: Bolt Charge gained ➜ Melee energy (2.5%)
          Given the build "bolt-charge-disorienting-blow" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And on "↳ Bolt Charge gained" "Bolt Charge" fires "+2.5% melee energy"
          And its BUFFS show only "Amplified" and "Bolt Charge"
        """)]
    public async Task BoltChargeGivesMeleeEnergyForEveryStackGained(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolt-charge-disorienting-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Melee (🥊 hit)")
            .FiresOn("↳ Bolt Charge gained", new Keyword.BoltCharge(), "+2.5% melee energy")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolt Charge at max discharges on a grenade hit
          # Rule keywords-arc-bolt-charge-1: Grenade hit ➜ Bolt Charge at max stacks ➜ Bolt Charge strike, ends Bolt Charge
          Given the build "bolt-charge-disorienting-blow-skip-grenade" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Amplified" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3 discharges
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Grenade (🥊 hit)"
          And "Bolt Charge" fires "consumes Bolt Charge" and "Bolt Charge strike (kills)"
          And its BUFFS show only "Amplified"
        """)]
    public async Task BoltChargeAtMaxDischargesOnAGrenadeHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolt-charge-disorienting-blow-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var discharged = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(discharged)
            .HasHeading("#3 Grenade (🥊 hit)")
            .Fires(new Keyword.BoltCharge(), "consumes Bolt Charge", "Bolt Charge strike (kills)")
            .Shows(new StateGroup.Buffs(), "Amplified");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolt Charge at max discharges on a melee hit
          # Rule keywords-arc-bolt-charge-1: Melee hit ➜ Bolt Charge at max stacks ➜ Bolt Charge strike, ends Bolt Charge
          Given the build "bolt-charge-arc-staff-combination-blow" is imported, with no steps
          When I tap "Arc Staff" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Super (💀 kill)"
          # A Super's first kill also casts it: Super is on you from then on.
          And its BUFFS show only "Super · 1 effect" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Super · 1 effect" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3 discharges
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Melee (🥊 hit)"
          And "Bolt Charge" fires "consumes Bolt Charge" and "Bolt Charge strike (kills)"
          And its BUFFS show only "Super · 1 effect"
        """)]
    public async Task BoltChargeAtMaxDischargesOnAMeleeHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolt-charge-arc-staff-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.ArcStaff());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A Super's first kill also casts it: Super is on you from then on.
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Super (💀 kill)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect", "Bolt Charge (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var discharged = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(discharged)
            .HasHeading("#3 Melee (🥊 hit)")
            .Fires(new Keyword.BoltCharge(), "consumes Bolt Charge", "Bolt Charge strike (kills)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolt Charge at max discharges on a sliding melee hit
          # Rule keywords-arc-bolt-charge-1: Sliding melee hit ➜ Bolt Charge at max stacks ➜ Bolt Charge strike, ends Bolt Charge
          Given the build "bolt-charge-disorienting-blow-tempest-strike" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Amplified" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3 discharges
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Sliding melee (🥊 hit)"
          And on "Sliding melee 🥊 hit on Blinded target" "Bolt Charge" fires "consumes Bolt Charge" and "Bolt Charge strike (kills)"
          # Bolt Charge is back, but no longer at max: the strike kills the target the slide just jolted, and Tempest Strike
          # gives a new stack for that kill.
          And its BUFFS show only "Amplified" and "Bolt Charge"
        """)]
    public async Task BoltChargeAtMaxDischargesOnASlidingMeleeHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolt-charge-disorienting-blow-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        // Bolt Charge is back, but no longer at max: the strike kills the target the slide just jolted, and Tempest Strike
        // gives a new stack for that kill.
        var discharged = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(discharged)
            .HasHeading("#3 Sliding melee (🥊 hit)")
            .FiresOn("Sliding melee 🥊 hit on Blinded target", new Keyword.BoltCharge(), "consumes Bolt Charge", "Bolt Charge strike (kills)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolt Charge at max discharges on a Super hit
          # Rule keywords-arc-bolt-charge-1: Super hit ➜ Bolt Charge at max stacks ➜ Bolt Charge strike, ends Bolt Charge
          Given the build "bolt-charge-disorienting-blow-storms-edge" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Amplified" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3 discharges
          When I tap "Storm's Edge" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Super (🥊 hit)"
          And "Bolt Charge" fires "consumes Bolt Charge" and "Bolt Charge strike (kills)"
          # A Super's first hit also casts it: Super is on you from then on.
          And its BUFFS show only "Amplified" and "Super · 1 effect"
        """)]
    public async Task BoltChargeAtMaxDischargesOnASuperHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolt-charge-disorienting-blow-storms-edge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var atMax = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(atMax)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        // A Super's first hit also casts it: Super is on you from then on.
        var discharged = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(discharged)
            .HasHeading("#3 Super (🥊 hit)")
            .Fires(new Keyword.BoltCharge(), "consumes Bolt Charge", "Bolt Charge strike (kills)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Super · 1 effect");
    }
}
