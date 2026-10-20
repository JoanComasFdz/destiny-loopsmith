using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Proximity Ward's rule (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class ProximityWardTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Proximity Ward gives an overshield on a finisher
          # Rule mods-class-item-proximity-ward-1: Finisher ➜ Proximity Ward equipped ➜ Overshield
          Given the build "proximity-ward-alone" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And "Proximity Ward" fires "Overshield"
          And its BUFFS show only "Overshield"
        """)]
    public async Task ProximityWardGivesAnOvershieldOnAFinisher(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("proximity-ward-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .Fires(new ArmorMod.ProximityWard(), "Overshield")
            .Shows(new StateGroup.Buffs(), "Overshield");
    }
}
