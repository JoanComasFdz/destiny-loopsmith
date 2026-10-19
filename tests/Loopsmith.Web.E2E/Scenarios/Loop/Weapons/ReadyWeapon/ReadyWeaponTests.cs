using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Readying (swapping to) a weapon as a step of its own (ADRs D43): it takes no enemy count.</summary>
public sealed class ReadyWeaponTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ready shows on every weapon but is disabled when no rule reacts to it
          Given the build "festival-flight-one-for-all-ready" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then its row shows "🥊 hit", "💀 kill", "✋ ready", "📦 slide over ammo" and "🔄 reload"
          And "✋ ready" stands apart from the count
          And "✋ ready" is disabled
        """)]
    public async Task ReadyShowsOnEveryWeaponButIsDisabledWhenNoRuleReactsToIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all-ready.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row)
            .ShowsTriggers(new StepTrigger.Hit(), new StepTrigger.Kill(), new StepTrigger.Ready(), new StepTrigger.SlideOverAmmo(), new StepTrigger.Reload())
            .ShowsApartFromTheCount(new StepTrigger.Ready())
            .IsDisabled(new StepTrigger.Ready());
    }
}
