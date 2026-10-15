using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Reloading a weapon as a step of its own (ADRs D37): it takes no enemy count.</summary>
public sealed class ReloadTests : BaseE2E
{
    [Fact]
    public async Task GivenALoopForFestivalFlightWithOneForAll_WhenIPickItInTheTriggerPanelAndReloadAtThreeEnemies_ThenReloadStandsApartFromTheCountAndAddsAReloadWithoutOne()
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new WeaponSlot.Kinetic());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync();

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
        Asserting.That(reloaded).HasReloadAt(1, new WeaponSlot.Kinetic());

        // When
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(hit).HasHitAt(2, new WeaponSlot.Kinetic(), 3);
    }
}
