using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Amplitude's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfAmplitudeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Amplitude's two kills at once while Amplified drop an Orb of Power
          # Rule all-classes-arc-fragments-spark-of-amplitude-1: Any 2+ kills within 4s ➜ Spark of Amplitude equipped and Amplified on you ➜ Orb of Power
          Given the build "spark-of-amplitude-spark-of-volts-skip-grenade" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what #2's kills need
          When I tap "Skip Grenade" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          Then card #2's heading is "#2 Grenade (💀 kill 2)"
          And it reads "Grenade 💀 killed 2 enemies in 4 s"
          And "Spark of Amplitude" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task SparkOfAmplitudesTwoKillsAtOnceWhileAmplifiedDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-amplitude-spark-of-volts-skip-grenade.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 4 s · Spark of Amplitude");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Grenade (💀 kill 2)")
            .Reads("Grenade 💀 killed 2 enemies in 4 s")
            .Fires(new Fragment.SparkOfAmplitude(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Amplitude's single kill while Amplified may drop an Orb of Power
          # Rule all-classes-arc-fragments-spark-of-amplitude-2: Any fewer than 2 kills ➜ Spark of Amplitude equipped and Amplified on you ➜ Orb of Power
          Given the build "spark-of-amplitude-spark-of-volts-skip-grenade" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Amplified is what #2's kill needs
          When I tap "Skip Grenade" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #2's heading is "#2 Grenade (💀 kill)"
          And it reads "Grenade 💀 killed 1 enemy"
          # A chance: a kill of another step may be the second within 4 s.
          And "Spark of Amplitude" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task SparkOfAmplitudesSingleKillWhileAmplifiedMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-amplitude-spark-of-volts-skip-grenade.build.yaml");
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
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // A chance: a kill of another step may be the second within 4 s.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Grenade (💀 kill)")
            .Reads("Grenade 💀 killed 1 enemy")
            .Fires(new Fragment.SparkOfAmplitude(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
