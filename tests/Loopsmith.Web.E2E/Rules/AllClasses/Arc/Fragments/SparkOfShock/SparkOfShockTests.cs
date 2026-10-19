using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Shock's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfShockTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Shock's grenade hit jolts the target
          # Rule all-classes-arc-fragments-spark-of-shock-1: Grenade hit ➜ Spark of Shock equipped ➜ Jolt
          Given the build "spark-of-shock-and-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And it reads "Grenade 🥊 hit"
          And "Spark of Shock" fires "Jolt target (10s)"
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task SparkOfShocksGrenadeHitJoltsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-shock-and-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Reads("Grenade 🥊 hit")
            .Fires(new Fragment.SparkOfShock(), "Jolt target (10s)")
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }
}
