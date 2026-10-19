using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Ions's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfIonsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Ions's kill on a jolted target drops an Ionic Trace
          # Rule all-classes-arc-fragments-spark-of-ions-1: Any kill ➜ Spark of Ions equipped and Jolt on the target ➜ Ionic Trace
          Given the build "spark-of-ions-spark-of-shock-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 kills
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Grenade (💀 kill)"
          And on "Grenade 💀 kill on Jolted target" "Spark of Ions" fires "Ionic Trace"
          # The trace homes in on you: the same step picks it up.
          And its GROUND shows none
        """)]
    public async Task SparkOfIonssKillOnAJoltedTargetDropsAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-ions-spark-of-shock-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // The trace homes in on you: the same step picks it up.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Grenade (💀 kill)")
            .FiresOn("Grenade 💀 kill on Jolted target", new Fragment.SparkOfIons(), "Ionic Trace")
            .ShowsNone(new StateGroup.Ground());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Ions's Bolt Charge strike kill drops an Ionic Trace
          # Rule all-classes-arc-fragments-spark-of-ions-2: Bolt Charge strike kill ➜ Spark of Ions equipped ➜ Ionic Trace
          Given the build "spark-of-ions-spark-of-volts-skip-grenade" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Amplified" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3's hit discharges
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Grenade (🥊 hit)"
          And on "↳ Bolt Charge 💀 kill" "Spark of Ions" fires "Ionic Trace"
          # The trace homes in on you: the same step picks it up.
          And its GROUND shows none
        """)]
    public async Task SparkOfIonssBoltChargeStrikeKillDropsAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-ions-spark-of-volts-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Status.BoltCharge(), "at max (x10)");
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
        // The trace homes in on you: the same step picks it up.
        var struck = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(struck)
            .HasHeading("#3 Grenade (🥊 hit)")
            .FiresOn("↳ Bolt Charge 💀 kill", new Fragment.SparkOfIons(), "Ionic Trace")
            .ShowsNone(new StateGroup.Ground());
    }
}
