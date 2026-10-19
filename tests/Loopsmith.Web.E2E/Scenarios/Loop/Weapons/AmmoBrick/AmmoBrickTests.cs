using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Picking up an ammo brick as a step of its own (ADRs D42): a Special or a Heavy one, by sliding over it or not; no weapon offers a slide over ammo any more.</summary>
public sealed class AmmoBrickTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Pick up an ammo brick takes its type and can be a slide
          Given the build "ammo-brick-spark-of-momentum" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          Then its row shows "special", "heavy" and "🛝 by sliding"
          When I tap "heavy"
          Then step #1 is "ammo:heavy"
          When I tap "📦 Ammo brick" in the trigger panel
          And I tick "🛝 by sliding"
          And I tap "special"
          Then step #2 is "ammo:special:slide"
        """)]
    public async Task PickUpAnAmmoBrickTakesItsTypeAndCanBeASlide(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ammo-brick-spark-of-momentum.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTriggers(new StepTrigger.PickUp(new Brick.Special()), new StepTrigger.PickUp(new Brick.Heavy()))
            .ShowsTheOption(new RowOption.BySliding());

        // When
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Heavy()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(picked).HasStep(1, new LoopStep.PickUpAmmo(new Brick.Heavy()));

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.BySliding());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var slid = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(slid).HasStep(2, new LoopStep.SlideOverAmmo(new Brick.Special()));
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Weapons no longer offer a slide over ammo
          Given the build "ammo-brick-spark-of-momentum" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows only "🥊 hit", "💀 kill" and "🔄 reload"
        """)]
    public async Task WeaponsNoLongerOfferASlideOverAmmo(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ammo-brick-spark-of-momentum.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).ShowsTriggers(new StepTrigger.Hit(), new StepTrigger.Kill(), new StepTrigger.Reload());
    }
}
