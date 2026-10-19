using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Resistance's passive (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfResistanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Resistance gives damage resistance
          # Rule all-classes-arc-fragments-spark-of-resistance-1: Always ➜ Spark of Resistance equipped ➜ Damage resistance (25% [PvP 10%])
          Given the build "spark-of-resistance-only" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Finisher"
          # 34.5% is every Guardian's own against combatants; Spark of Resistance's 25% adds to it, 1 − (1 − 34.5%) × (1 − 25%).
          And its "All damage" resistance reads "50.88%" PvE and "10%" PvP
          And "Spark of Resistance" gives "25% · PvP 10%" of it
        """)]
    public async Task SparkOfResistanceGivesDamageResistance(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-resistance-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // 34.5% is every Guardian's own against combatants; Spark of Resistance's 25% adds to it, 1 − (1 − 34.5%) × (1 − 25%).
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .HasResistance("All damage", "50.88%", "10%")
            .HasResistanceFrom(new Fragment.SparkOfResistance(), "25% · PvP 10%");
    }
}
