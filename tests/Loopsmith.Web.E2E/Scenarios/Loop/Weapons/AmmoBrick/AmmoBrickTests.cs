using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Sliding over an ammo brick with a weapon in hand, as a step of its own (Spark of Momentum): it takes no enemy count.</summary>
public sealed class AmmoBrickTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Slide over an ammo brick stands apart from the count and takes none
          Given the build "festival-flight-one-for-all-spark-of-momentum" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows "🥊 hit" and "💀 kill" with the "×" enemy count
          And "📦 slide over ammo" stands apart from the count
          When I select "3 in 3 s · One for All"
          And I tap "📦 slide over ammo"
          Then step #1 is "kinetic:ammo", with no enemy count
        """)]
    public async Task SlideOverAnAmmoBrickStandsApartFromTheCountAndTakesNone(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all-spark-of-momentum.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTheCountWith(new StepTrigger.Hit(), new StepTrigger.Kill())
            .ShowsApartFromTheCount(new StepTrigger.SlideOverAmmo());

        // When
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.SlideOverAmmo());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(slid).HasStep(1, new LoopStep.SlideOverAmmo(new WeaponSlot.Kinetic()));
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Slide over an ammo brick shows on every weapon but is disabled when no rule reacts to it
          Given the build "festival-flight-one-for-all-ammo-brick" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows "🥊 hit", "💀 kill", "📦 slide over ammo" and "🔄 reload"
          And "📦 slide over ammo" is disabled
        """)]
    public async Task SlideOverAnAmmoBrickShowsOnEveryWeaponButIsDisabledWhenNoRuleReactsToIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all-ammo-brick.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTriggers(new StepTrigger.Hit(), new StepTrigger.Kill(), new StepTrigger.SlideOverAmmo(), new StepTrigger.Reload())
            .IsDisabled(new StepTrigger.SlideOverAmmo());
    }
}
