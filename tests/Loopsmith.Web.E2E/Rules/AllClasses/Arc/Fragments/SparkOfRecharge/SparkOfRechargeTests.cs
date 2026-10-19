using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Recharge's rule and passive (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfRechargeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Recharge's critical health gives you Spark of Recharge until critical health ends
          # Rule all-classes-arc-fragments-spark-of-recharge-1: Critical health ➜ Spark of Recharge equipped ➜ Spark of Recharge
          Given the build "spark-of-recharge-alone" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And "Spark of Recharge" fires "Spark of Recharge"
          # "1 effect": its faster regeneration (the next scenario).
          And its BUFFS show only "Critical health" and "Spark of Recharge · 1 effect"
          # keeping step #1: its critical health is what #2 ends
          When I tap "ends" on "Critical health" under "On you" in the trigger panel
          Then card #2's heading is "#2 Critical health ends"
          # Spark of Recharge's buff lasts until your shields are full: it ends with critical health.
          And its BUFFS show none
        """)]
    public async Task SparkOfRechargesCriticalHealthGivesYouSparkOfRechargeUntilCriticalHealthEnds(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-recharge-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // "1 effect": its faster regeneration (the next scenario).
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Fires(new Fragment.SparkOfRecharge(), "Spark of Recharge")
            .Shows(new StateGroup.Buffs(), "Critical health", "Spark of Recharge · 1 effect");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.CriticalHealth(), "ends");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // Spark of Recharge's buff lasts until your shields are full: it ends with critical health.
        var ended = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(ended)
            .HasHeading("#2 Critical health ends")
            .ShowsNone(new StateGroup.Buffs());
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: Spark of Recharge speeds up grenade and melee regeneration while it is on you
          # Rule all-classes-arc-fragments-spark-of-recharge-2: Always ➜ Spark of Recharge equipped and Spark of Recharge on you ➜ Grenade energy (400% [PvP 200%] faster regeneration), Melee energy (400% [PvP 200%] faster regeneration)
          # Desktop only: what it checks is the chip's tooltip, which shows on hover, and a phone has none.
          Given the build "spark-of-recharge-alone" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And its BUFFS show only "Critical health" and "Spark of Recharge · 1 effect"
          And the "Spark of Recharge · 1 effect" chip's tooltip reads "400% [PvP 200%] faster grenade and melee regeneration"
        """)]
    public async Task SparkOfRechargeSpeedsUpGrenadeAndMeleeRegenerationWhileItIsOnYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-recharge-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .Shows(new StateGroup.Buffs(), "Critical health", "Spark of Recharge · 1 effect")
            .HasTooltip(new StateGroup.Buffs(), "Spark of Recharge · 1 effect", "400% [PvP 200%] faster grenade and melee regeneration");
    }
}
