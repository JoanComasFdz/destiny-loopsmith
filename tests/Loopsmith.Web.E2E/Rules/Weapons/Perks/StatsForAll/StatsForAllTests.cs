using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>
/// Stats for All's rules (rules/weapons/perks.yaml), one scenario per rule claim.
/// Leaves out weapons-perks-stats-for-all-3: with the enhanced trait the buff shows as two WEAPONS chips, "Stats for All · 1 effect · Ogma PR6" (its details read the base 10s, not 11s) and "Stats for All (enhanced) · Ogma PR6" (no "Right then" card), so the stats can't be shown as the flow draws them.
/// </summary>
public sealed class StatsForAllTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stats for All's 3 quick hits give Stats for All
          # Rule weapons-perks-stats-for-all-1: This weapon 3+ hits within 3s ➜ Stats for All equipped ➜ Stats for All
          Given the build "stats-for-all-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 3 s · Stats for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit 3)"
          And it reads "Ogma PR6 🥊 hit 3 enemies in 3 s"
          And "Stats for All" fires "Stats for All (10s)"
          And its WEAPONS show only "Stats for All · 1 effect · Ogma PR6"
        """)]
    public async Task StatsForAlls3QuickHitsGiveStatsForAll(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stats-for-all-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Ogma PR6 (🥊 hit 3)")
            .Reads("Ogma PR6 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.StatsForAll(), "Stats for All (10s)")
            .Shows(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stats for All's fewer hits may give Stats for All
          # Rule weapons-perks-stats-for-all-2: This weapon fewer than 3 hits ➜ Stats for All equipped ➜ Stats for All (chance)
          Given the build "stats-for-all-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit)"
          And it reads "Ogma PR6 🥊 hit 1 enemy"
          And "Stats for All" fires "Stats for All (10s) (chance)"
          And its WEAPONS show only "Stats for All · 1 effect · Ogma PR6"
        """)]
    public async Task StatsForAllsFewerHitsMayGiveStatsForAll(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stats-for-all-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Ogma PR6 (🥊 hit)")
            .Reads("Ogma PR6 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.StatsForAll(), "Stats for All (10s) (chance)")
            .Shows(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stats for All raises its weapon's stats while it is up
          # Rule weapons-perks-stats-for-all-3: Always ➜ Stats for All equipped and Stats for All on you ➜ Weapon stats
          Given the build "stats-for-all-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 3 s · Stats for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit 3)"
          And its WEAPONS show only "Stats for All · 1 effect · Ogma PR6"
          # keeping step #1: its Stats for All is what the stats need
          When I tap the "Stats for All · 1 effect · Ogma PR6" chip in card #1's WEAPONS
          Then the item details popup opens, titled "Stats for All"
          And its "Right then, after #1" card reads only "10s" and "Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed"
        """)]
    public async Task StatsForAllRaisesItsWeaponsStatsWhileItIsUp(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stats-for-all-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var struck = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(struck)
            .HasHeading("#1 Ogma PR6 (🥊 hit 3)")
            .Shows(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "Stats for All · 1 effect · Ogma PR6");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.StatsForAll())
            .HasRightThen("Right then, after #1", "10s", "Energy weapon: +10 Range, +10 Stability, +35 Handling, +35 Reload Speed");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Stats for All's 3 quick hits give Stats for All
          # Rule weapons-perks-stats-for-all-1: This weapon 3+ hits within 3s ➜ Stats for All (enhanced) equipped ➜ Stats for All
          Given the build "stats-for-all-enhanced-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 3 s · Stats for All (enhanced)"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit 3)"
          And it reads "Ogma PR6 🥊 hit 3 enemies in 3 s"
          And "Stats for All (enhanced)" fires "Stats for All (11s)"
          # WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        """)]
    public async Task EnhancedStatsForAlls3QuickHitsGiveStatsForAll(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stats-for-all-enhanced-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Ogma PR6 (🥊 hit 3)")
            .Reads("Ogma PR6 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.StatsForAllEnhanced(), "Stats for All (11s)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Stats for All's fewer hits may give Stats for All
          # Rule weapons-perks-stats-for-all-2: This weapon fewer than 3 hits ➜ Stats for All (enhanced) equipped ➜ Stats for All (chance)
          Given the build "stats-for-all-enhanced-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit)"
          And it reads "Ogma PR6 🥊 hit 1 enemy"
          And "Stats for All (enhanced)" fires "Stats for All (11s) (chance)"
          # WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        """)]
    public async Task EnhancedStatsForAllsFewerHitsMayGiveStatsForAll(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("stats-for-all-enhanced-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // WEAPONS isn't checked: the enhanced buff shows as two chips (the summary's Leaves out line).
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Ogma PR6 (🥊 hit)")
            .Reads("Ogma PR6 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.StatsForAllEnhanced(), "Stats for All (11s) (chance)");
    }
}
