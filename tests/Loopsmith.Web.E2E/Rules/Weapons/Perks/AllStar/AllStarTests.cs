using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>
/// All-Star's rules (rules/weapons/perks.yaml), one scenario per rule claim.
/// Leaves out weapons-perks-all-star-2: with the enhanced trait the buff shows as two WEAPONS chips, "All-Star · 1 effect · Battle Scar" (its details read the base 6s, not 7s) and "All-Star (enhanced) · Battle Scar" (no "Right then" card), so the reload speed can't be shown as the flow draws it.
/// </summary>
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
          And "All-Star" fires "All-Star (6s)" and "+35% [PvP 15%] kinetic weapon damage"
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
            .Fires(new WeaponPerk.AllStar(), "All-Star (6s)", "+35% [PvP 15%] kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "All-Star · 1 effect · Battle Scar");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced All-Star's ammo brick pickup gives All-Star for 7 s
          # Rule weapons-perks-all-star-1: Pick up an ammo brick ➜ All-Star (enhanced) equipped ➜ Damage bonus and All-Star
          Given the build "all-star-enhanced-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "heavy"
          Then card #1's heading is "#1 Pick up a Heavy ammo brick"
          And "All-Star (enhanced)" fires "All-Star (7s)" and "+35% [PvP 15%] kinetic weapon damage"
          # WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        """)]
    public async Task EnhancedAllStarsAmmoBrickPickupGivesAllStarFor7S(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("all-star-enhanced-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Heavy()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Heavy ammo brick")
            .Fires(new WeaponPerk.AllStarEnhanced(), "All-Star (7s)", "+35% [PvP 15%] kinetic weapon damage");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: All-Star speeds up its weapon's reload while it is up
          # Rule weapons-perks-all-star-2: Always ➜ All-Star equipped and All-Star on you ➜ Weapon stats
          Given the build "all-star-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "heavy"
          Then card #1's heading is "#1 Pick up a Heavy ammo brick"
          And its WEAPONS show only "All-Star · 1 effect · Battle Scar"
          # keeping step #1: its All-Star is what the reload speed needs
          When I tap the "All-Star · 1 effect · Battle Scar" chip in card #1's WEAPONS
          Then the item details popup opens, titled "All-Star"
          And its "Right then, after #1" card reads only "6s" and "Kinetic weapon: +~100 Reload Speed"
        """)]
    public async Task AllStarSpeedsUpItsWeaponsReloadWhileItIsUp(Screen screen)
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
            .Shows(new StateGroup.Weapons(), "All-Star · 1 effect · Battle Scar");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "All-Star · 1 effect · Battle Scar");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.AllStar())
            .HasRightThen("Right then, after #1", "6s", "Kinetic weapon: +~100 Reload Speed");
    }
}
