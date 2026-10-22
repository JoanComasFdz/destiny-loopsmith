using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Energy;

/// <summary>
/// Recharging over time is a declaration (ADRs D48): nothing regenerates by itself between steps, so "On you" offers
/// "Grenade recharged" while the grenade isn't full, and it fills every charge.
/// </summary>
public sealed class EnergyDeclarationTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Declaring the grenade recharged fills it
          Given the build "energy-declaration-skip-grenade" is imported, with no steps
          Then "On you" in the trigger panel offers no "Grenade recharged"
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then "On you" in the trigger panel offers "Grenade recharged"
          # keeping step #1: its throw spent the grenade #2 declares recharged
          When I tap "Grenade recharged" under "On you" in the trigger panel
          Then card #2's heading is "#2 Grenade recharged"
          And its ENERGY reads "Grenade 0% → 100%"
          And "On you" in the trigger panel offers no "Grenade recharged"
        """)]
    public async Task DeclaringTheGrenadeRechargedFillsIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("energy-declaration-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var fresh = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(fresh).DoesNotOfferRecharged(new AbilitySlot.Grenade());

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var spent = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(spent).OffersRecharged(new AbilitySlot.Grenade());

        // When
        // keeping step #1: its throw spent the grenade #2 declares recharged
        await Frontend.Designer.TriggerPalette.ClickRechargedAsync(new AbilitySlot.Grenade());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var recharged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        var full = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(recharged)
            .HasHeading("#2 Grenade recharged")
            .ShowsEnergy(new AbilitySlot.Grenade(), "0%", "100%");
        Asserting.That(full).DoesNotOfferRecharged(new AbilitySlot.Grenade());
    }
}
