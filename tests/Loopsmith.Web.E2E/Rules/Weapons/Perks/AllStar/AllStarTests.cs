using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>All-Star's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class AllStarTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: All-Star's ammo brick pickup gives All-Star
          # Rule weapons-perks-all-star-1: Pick up an ammo brick ➜ All-Star equipped ➜ Damage bonus and All-Star
          Given the build "all-star-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "heavy"
          Then card #1's heading is "#1 Pick up a Heavy ammo brick"
          And "All-Star" fires "All-Star (6s)" and "+35% kinetic weapon damage"
          And its WEAPONS show only "All-Star · 1 effect · Battle Scar"
        """)]
    public async Task AllStarsAmmoBrickPickupGivesAllStar(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("all-star-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Heavy()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Heavy ammo brick")
            .Fires(new WeaponPerk.AllStar(), "All-Star (6s)", "+35% kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "All-Star · 1 effect · Battle Scar");
    }
}
