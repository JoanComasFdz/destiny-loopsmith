using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Instinct's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfInstinctTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Instinct's hit at critical health jolts the target
          # Rule all-classes-arc-fragments-spark-of-instinct-1: Hit at critical health ➜ Spark of Instinct equipped ➜ Jolt
          Given the build "spark-of-instinct-alone" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health"
          # keeping step #1: its critical health is what #2's hit needs
          When I tap "hit" on "Critical health" under "On you" in the trigger panel
          Then card #2's heading is "#2 Hit at critical health"
          And "Spark of Instinct" fires "Jolt target (10s)"
          And its BUFFS show only "Critical health"
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task SparkOfInstinctsHitAtCriticalHealthJoltsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-instinct-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.CriticalHealth(), "hit");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Hit at critical health")
            .Fires(new Fragment.SparkOfInstinct(), "Jolt target (10s)")
            .Shows(new StateGroup.Buffs(), "Critical health")
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }
}
