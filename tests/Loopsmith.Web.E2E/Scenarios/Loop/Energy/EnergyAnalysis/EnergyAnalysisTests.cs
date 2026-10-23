using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>The loop's analysis by its abilities' energy (ADRs D48): a step whose ability surely isn't ready breaks the loop there.</summary>
public sealed class EnergyAnalysisTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The analysis says a loop breaks where its grenade is not back
          Given the build "energy-analysis-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          And I tap "Analyze"
          # The next pass starts with the grenade the throw spent: nothing gave it back.
          Then the analysis's verdict reads "✗ BREAKS AT #1"
          And it says "✗ Breaks at #1 (Grenade (💀 kill)): Grenade not ready: 0% before #1"
          And its step #1 reads "not ready" "Grenade not ready: 0% before #1"
        """)]
    public async Task TheAnalysisSaysALoopBreaksWhereItsGrenadeIsNotBack(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-analysis-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.ClickAnalyzeAsync();
        await Frontend.Designer.Analysis.WaitUntilShownAsync();

        // Then
        // The next pass starts with the grenade the throw spent: nothing gave it back.
        var analysis = await Frontend.Designer.Analysis.ReadAsync();
        Asserting.That(analysis)
            .HasVerdict("✗ BREAKS AT #1")
            .SaysVerdict("✗ Breaks at #1 (Grenade (💀 kill)): Grenade not ready: 0% before #1")
            .SaysNotReady(1, "Grenade not ready: 0% before #1");
    }
}
