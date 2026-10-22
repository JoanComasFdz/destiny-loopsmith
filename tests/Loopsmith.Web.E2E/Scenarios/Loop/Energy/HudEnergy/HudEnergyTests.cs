using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>
/// The trigger panel's ability squares show their energy after the loop's last step (ADRs D48): gray when spent, their
/// colour growing from below with the energy, like a vertical bar.
/// </summary>
public sealed class HudEnergyTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The grenade's square goes gray when it is thrown and fills again when it is recharged
          Given the build "energy-hud-skip-grenade" is imported, with no steps
          Then the "Skip Grenade" square in the trigger panel is filled to 100%
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then the "Skip Grenade" square in the trigger panel is filled to 0%
          # keeping step #1: its throw spent the grenade #2 declares recharged
          When I tap "Grenade recharged" under "On you" in the trigger panel
          Then the "Skip Grenade" square in the trigger panel is filled to 100%
        """)]
    public async Task TheGrenadesSquareGoesGrayWhenItIsThrownAndFillsAgainWhenItIsRecharged(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-hud-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var fresh = await Frontend.Designer.TriggerPalette.ReadFillAsync(new Ability.SkipGrenade());
        Asserting.That(fresh).IsFilledTo("100%");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var thrown = await Frontend.Designer.TriggerPalette.ReadFillAsync(new Ability.SkipGrenade());
        Asserting.That(thrown).IsFilledTo("0%");

        // When
        // keeping step #1: its throw spent the grenade #2 declares recharged
        await Frontend.Designer.TriggerPalette.ClickRechargedAsync(new AbilitySlot.Grenade());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var recharged = await Frontend.Designer.TriggerPalette.ReadFillAsync(new Ability.SkipGrenade());
        Asserting.That(recharged).IsFilledTo("100%");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The grenade's square fills from below with the energy a refund gives back
          Given the build "energy-hud-skip-grenade-spark-of-ions-70-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          # The Ionic Trace's 11.25% × Skip Grenade's 0.75 chunk scalar, at 70 Grenade.
          Then the "Skip Grenade" square in the trigger panel is filled to 8.44%
        """)]
    public async Task TheGrenadesSquareFillsFromBelowWithTheEnergyARefundGivesBack(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-hud-skip-grenade-spark-of-ions-70-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The Ionic Trace's 11.25% × Skip Grenade's 0.75 chunk scalar, at 70 Grenade.
        var refilled = await Frontend.Designer.TriggerPalette.ReadFillAsync(new Ability.SkipGrenade());
        Asserting.That(refilled).IsFilledTo("8.44%");
    }
}
