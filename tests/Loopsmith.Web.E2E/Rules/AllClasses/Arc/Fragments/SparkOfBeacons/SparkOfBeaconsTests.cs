using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Beacons's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfBeaconsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Beacons's Special Arc weapon kill while Amplified blinds the enemies around it
          # Rule all-classes-arc-fragments-spark-of-beacons-1: Special Arc weapon kill ➜ Spark of Beacons equipped and Amplified on you ➜ Blind
          Given the build "spark-of-beacons-spark-of-volts-matador-64" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what #2's kill needs
          When I tap "Matador 64" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Matador 64 (💀 kill)"
          And "Spark of Beacons" fires "Blind target (10s)"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task SparkOfBeaconssSpecialArcWeaponKillWhileAmplifiedBlindsTheEnemiesAroundIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-beacons-spark-of-volts-matador-64.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var amplified = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(amplified)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Matador64());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Matador 64 (💀 kill)")
            .Fires(new Fragment.SparkOfBeacons(), "Blind target (10s)")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Beacons's Heavy Arc weapon kill while Amplified blinds the enemies around it
          # Rule all-classes-arc-fragments-spark-of-beacons-2: Heavy Arc weapon kill ➜ Spark of Beacons equipped and Amplified on you ➜ Blind
          Given the build "spark-of-beacons-spark-of-volts-thunderlord" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what #2's kill needs
          When I tap "Thunderlord" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Thunderlord (💀 kill)"
          And "Spark of Beacons" fires "Blind target (10s)"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task SparkOfBeaconssHeavyArcWeaponKillWhileAmplifiedBlindsTheEnemiesAroundIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-beacons-spark-of-volts-thunderlord.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var amplified = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(amplified)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Thunderlord (💀 kill)")
            .Fires(new Fragment.SparkOfBeacons(), "Blind target (10s)")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }
}
