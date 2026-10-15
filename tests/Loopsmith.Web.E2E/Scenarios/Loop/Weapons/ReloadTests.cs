using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Reloading a weapon as a step of its own (ADRs D37): it takes no enemy count.</summary>
public sealed class ReloadTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reload stands apart from the count and takes none
          Given the build "festival-flight-one-for-all" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows "🥊 hit" and "💀 kill" with the "×" enemy count
          And "🔄 reload" stands apart from the count
          When I select "3 in 3 s · One for All"
          And I tap "🔄 reload"
          Then step #1 is "kinetic:reload", with no enemy count
          When I tap "🥊 hit"
          Then step #2 is "kinetic:hit:3"
        """)]
    public async Task ReloadStandsApartFromTheCountAndTakesNone(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTheCountWith(new StepTrigger.Hit(), new StepTrigger.Kill())
            .ShowsApartFromTheCount(new StepTrigger.Reload());

        // When
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Reload());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var reloaded = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(reloaded).HasStep(1, new LoopStep.Reload(new WeaponSlot.Kinetic()));

        // When
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(hit).HasStep(2, new LoopStep.Hit(new WeaponSlot.Kinetic(), 3));
    }
}
