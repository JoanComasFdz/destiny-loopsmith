using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>A weapon's ammo type (Bungie's manifest's): a rule on a Special or Heavy weapon's kill doesn't fire on a Primary's.</summary>
public sealed class AmmoTypeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Beacons doesn't blind on a Primary Arc weapon kill
          Given the build "primary-crisis-inverted-spark-of-beacons" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what Spark of Beacons needs
          When I tap "Crisis Inverted" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Crisis Inverted (💀 kill)"
          # Crisis Inverted is a Primary hand cannon: Spark of Beacons blinds only on a Special or Heavy Arc weapon's kill.
          And "Spark of Beacons" fires nothing
          And its DEBUFFS show none
        """)]
    public async Task SparkOfBeaconsDoesntBlindOnAPrimaryArcWeaponKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("primary-crisis-inverted-spark-of-beacons.build.yaml");
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
        // Crisis Inverted is a Primary hand cannon: Spark of Beacons blinds only on a Special or Heavy Arc weapon's kill.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Crisis Inverted (💀 kill)")
            .DoesNotFire(new Fragment.SparkOfBeacons())
            .ShowsNone(new StateGroup.Debuffs());
    }
}
