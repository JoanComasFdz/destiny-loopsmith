using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Grenades;

/// <summary>Flashbang Grenade's rules (rules/all-classes/arc/grenades.yaml), one scenario per rule claim.</summary>
public sealed class FlashbangGrenadeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Flashbang Grenade's hit blinds the target
          # Rule all-classes-arc-grenades-flashbang-grenade-1: Grenade hit ➜ Flashbang Grenade equipped ➜ Blind (5s)
          Given the build "flashbang-grenade-only" is imported, with no steps
          When I tap "Flashbang Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And it reads "Grenade 🥊 hit"
          And "Flashbang Grenade" fires "Blind target (5s)"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task FlashbangGrenadesHitBlindsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("flashbang-grenade-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.FlashbangGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.FlashbangGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Reads("Grenade 🥊 hit")
            .Fires(new Ability.FlashbangGrenade(), "Blind target (5s)")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }
}
