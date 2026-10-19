using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>
/// Spark of Frequency's passive (rules/all-classes/arc/fragments.yaml), one scenario per rule claim. Bolt Charge isn't
/// counted (ADRs D33), so the extra stack is a caveat of each gain, shown with the "caveats" checkbox ticked.
/// </summary>
public sealed class SparkOfFrequencyTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Frequency gives an extra Bolt Charge while you're Amplified
          # Rule all-classes-arc-fragments-spark-of-frequency-1: Always ➜ Spark of Frequency equipped and Amplified on you ➜ Bolt Charge (adds to · +1 stack)
          Given the build "spark-of-frequency-spark-of-volts" is imported, with no steps
          When I tick "caveats" above the loop
          And I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Amplified · 1 effect" and "Bolt Charge"
          And under "Spark of Volts" the caveats read "+1 Bolt Charge: +1 from Spark of Frequency"
        """)]
    public async Task SparkOfFrequencyGivesAnExtraBoltChargeWhileYoureAmplified(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-frequency-spark-of-volts.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TickCaveatsAsync();
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Amplified · 1 effect", "Bolt Charge")
            .HasCaveats(new Fragment.SparkOfVolts(), "+1 Bolt Charge: +1 from Spark of Frequency");
    }
}
