using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>Where a weapon's row of buttons sits in the trigger panel: checked from the boxes of its parts.</summary>
public sealed class WeaponRowLayoutTests : BaseE2E
{
    [Theory, PlayInMobile]
    [TestDescription("""
        @mobile
        Scenario: The weapon row fits a phone with reload at its right end
          # Mobile only: the row only wraps at a phone's width.
          Given the build "festival-flight-one-for-all" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          Then the row wraps onto more than one line
          And none of "🥊 hit", "💀 kill", the "×" enemy count and "🔄 reload" overlap
          And "🔄 reload" sits at the row's right end
          And the row fits inside the screen, with no sideways scroll
        """)]
    public async Task TheWeaponRowFitsAPhoneWithReloadAtItsRightEnd(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-one-for-all.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());

        // Then
        var layout = await Frontend.Designer.TriggerPalette.ReadLayoutAsync();
        Asserting.That(layout)
            .WrapsOntoSeveralLines()
            .HasNoOverlap()
            .HasAtRightEnd(new StepTrigger.Reload())
            .FitsInsideTheScreen();
    }
}
