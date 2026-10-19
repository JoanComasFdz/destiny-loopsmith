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
        Scenario: Spark of Beacons's Arc weapon kill while Amplified may blind the enemies around it
          # Rule all-classes-arc-fragments-spark-of-beacons-1: Arc weapon kill ➜ Spark of Beacons equipped and Amplified on you ➜ Blind
          Given the build "spark-of-beacons-spark-of-volts-crisis-inverted" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what #2's kill needs
          When I tap "Crisis Inverted" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 kill"
          # A chance: only a Special or Heavy Arc weapon's kill sets it off, and the rules can't tell a weapon's ammo.
          And "Spark of Beacons" fires "Blind target (10s) (chance)"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task SparkOfBeaconssArcWeaponKillWhileAmplifiedMayBlindTheEnemiesAroundIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-beacons-spark-of-volts-crisis-inverted.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // A chance: only a Special or Heavy Arc weapon's kill sets it off, and the rules can't tell a weapon's ammo.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 kill")
            .Fires(new Fragment.SparkOfBeacons(), "Blind target (10s) (chance)")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }
}
