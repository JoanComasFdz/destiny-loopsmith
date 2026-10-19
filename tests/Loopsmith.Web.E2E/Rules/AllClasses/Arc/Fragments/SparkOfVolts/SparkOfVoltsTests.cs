using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Volts's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfVoltsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Volts's finisher makes you Amplified and gives a Bolt Charge
          # Rule all-classes-arc-fragments-spark-of-volts-1: Finisher ➜ Spark of Volts equipped ➜ Amplified, Bolt Charge
          Given the build "spark-of-volts-only" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And it reads "Finisher"
          And "Spark of Volts" fires "Amplified (15s)" and "+1 Bolt Charge"
          And its BUFFS show only "Amplified" and "Bolt Charge"
        """)]
    public async Task SparkOfVoltssFinisherMakesYouAmplifiedAndGivesABoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-volts-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .Reads("Finisher")
            .Fires(new Fragment.SparkOfVolts(), "Amplified (15s)", "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");
    }
}
