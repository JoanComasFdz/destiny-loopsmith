using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>
/// Critical health is a status on you (ADRs D3): "Critical health" puts it on you and its "ends" takes it off, and while it
/// lasts "On you" offers the hit at it beside it.
/// </summary>
public sealed class CriticalHealthTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Critical health stays on you until you declare it ended
          Given the build "critical-health-spark-of-instinct" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health"
          And "On you" in the trigger panel shows "Critical health" with "hit" and "ends"
          # keeping step #1: its critical health is what #2 ends
          When I tap "ends" on "Critical health" under "On you" in the trigger panel
          Then card #2's heading is "#2 Critical health ends"
          And its BUFFS show none
          And "On you" in the trigger panel shows "Critical health" as a button again
        """)]
    public async Task CriticalHealthStaysOnYouUntilYouDeclareItEnded(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("critical-health-spark-of-instinct.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        var atCritical = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health");
        Asserting.That(atCritical)
            .ShowsOnYouWith(new Keyword.CriticalHealth(), "hit", "ends");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.CriticalHealth(), "ends");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var ended = await Frontend.Designer.Loop.Step(2).ReadAsync();
        var afterEnd = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(ended)
            .HasHeading("#2 Critical health ends")
            .ShowsNone(new StateGroup.Buffs());
        Asserting.That(afterEnd)
            .ShowsOnYouAsAButton(new Keyword.CriticalHealth());
    }
}
