using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>
/// The trigger panel greys out only while the build equips nothing (docs/loop-format.md, "Starting from scratch"): the
/// finisher, a new pack and the declared states need nothing equipped, so a build with only a fragment can set things off.
/// </summary>
public sealed class TriggerPanelTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build with only a fragment can tap the Finisher
          Given the build "spark-of-volts-alone" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And "Spark of Volts" fires "Amplified" and "Bolt Charge"
        """)]
    public async Task ABuildWithOnlyAFragmentCanTapTheFinisher(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-volts-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickFinisherAsync();
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finisher = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finisher)
            .HasHeading("#1 Finisher")
            .Fires(new Fragment.SparkOfVolts(), "Amplified", "Bolt Charge");
    }
}
