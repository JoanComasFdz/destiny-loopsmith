using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Arc;

/// <summary>Ionic Trace's rules (rules/keywords/arc.yaml), one scenario per rule claim.</summary>
public sealed class IonicTraceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ionic Trace gives energy to all three abilities when picked up
          # Rule keywords-arc-ionic-trace-1: Pick up Ionic Trace ➜ Grenade energy (11.25%), Melee energy (11.25%), Class ability energy (13.5%)
          Given the build "ionic-trace-spark-of-shock-spark-of-ions" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          # Spark of Ions drops the trace on the jolted kill, and the trace homes in on you: the same step picks it up.
          And on "📍 Picked up Ionic Trace" "Ionic Trace" fires "+11.25% grenade and melee energy" and "+13.5% class ability energy"
        """)]
    public async Task IonicTraceGivesEnergyToAllThreeAbilitiesWhenPickedUp(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ionic-trace-spark-of-shock-spark-of-ions.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Spark of Ions drops the trace on the jolted kill, and the trace homes in on you: the same step picks it up.
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Grenade (💀 kill)")
            .FiresOn("📍 Picked up Ionic Trace", new Keyword.IonicTrace(), "+11.25% grenade and melee energy", "+13.5% class ability energy");
    }
}
