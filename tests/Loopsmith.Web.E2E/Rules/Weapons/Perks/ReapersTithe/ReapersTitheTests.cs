using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Reaper's Tithe's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class ReapersTitheTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's Tithe's 2 kills at once give 2 stacks
          # Rule weapons-perks-reapers-tithe-1: This weapon 2+ kills, fewer than 3 ➜ Reaper's Tithe equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "2 · Reaper's Tithe"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 2)"
          And it reads "Bellowing Giant 💀 killed 2 enemies"
          And "Reaper's Tithe" fires "+2 Reaper's Tithe (15s)" and "+20% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
        """)]
    public async Task ReapersTithes2KillsAtOnceGive2Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 · Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 2)")
            .Reads("Bellowing Giant 💀 killed 2 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+2 Reaper's Tithe (15s)", "+20% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's Tithe's 3 kills at once give 3 stacks
          # Rule weapons-perks-reapers-tithe-2: This weapon 3+ kills, fewer than 4 ➜ Reaper's Tithe equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "3 · Reaper's Tithe"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 3)"
          And it reads "Bellowing Giant 💀 killed 3 enemies"
          And "Reaper's Tithe" fires "+3 Reaper's Tithe (15s)" and "+30% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
        """)]
    public async Task ReapersTithes3KillsAtOnceGive3Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Reaper's Tithe");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 3)")
            .Reads("Bellowing Giant 💀 killed 3 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+3 Reaper's Tithe (15s)", "+30% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Reaper's Tithe's 4 kills at once give 4 stacks
          # Rule weapons-perks-reapers-tithe-3: This weapon 4+ kills ➜ Reaper's Tithe equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "4 · Reaper's Tithe · Reaper's Tithe x4"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 4)"
          And it reads "Bellowing Giant 💀 killed 4 enemies"
          And "Reaper's Tithe" fires "+4 Reaper's Tithe (15s)" and "+40% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
        """)]
    public async Task ReapersTithes4KillsAtOnceGive4Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("4 · Reaper's Tithe · Reaper's Tithe x4");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 4)")
            .Reads("Bellowing Giant 💀 killed 4 enemies")
            .Fires(new WeaponPerk.ReapersTithe(), "+4 Reaper's Tithe (15s)", "+40% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Reaper's Tithe's 2 kills at once give 2 stacks
          # Rule weapons-perks-reapers-tithe-1: This weapon 2+ kills, fewer than 3 ➜ Reaper's Tithe (enhanced) equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-enhanced-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "2 · Reaper's Tithe (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 2)"
          And it reads "Bellowing Giant 💀 killed 2 enemies"
          And "Reaper's Tithe (enhanced)" fires "+2 Reaper's Tithe (17s)" and "+20% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
          # keeping step #1: its Reaper's Tithe is what the chip shows
          When I tap the "Reaper's Tithe · Bellowing Giant" chip in card #1's WEAPONS
          Then the item details popup opens, titled "Reaper's Tithe (enhanced)"
          And its "Right then, after #1" card reads only "up to x4 · 17s"
        """)]
    public async Task EnhancedReapersTithes2KillsAtOnceGive2Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-enhanced-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 · Reaper's Tithe (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 2)")
            .Reads("Bellowing Giant 💀 killed 2 enemies")
            .Fires(new WeaponPerk.ReapersTitheEnhanced(), "+2 Reaper's Tithe (17s)", "+20% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.ReapersTitheEnhanced())
            .HasRightThen("Right then, after #1", "up to x4 · 17s");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Reaper's Tithe's 3 kills at once give 3 stacks
          # Rule weapons-perks-reapers-tithe-2: This weapon 3+ kills, fewer than 4 ➜ Reaper's Tithe (enhanced) equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-enhanced-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "3 · Reaper's Tithe (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 3)"
          And it reads "Bellowing Giant 💀 killed 3 enemies"
          And "Reaper's Tithe (enhanced)" fires "+3 Reaper's Tithe (17s)" and "+30% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
        """)]
    public async Task EnhancedReapersTithes3KillsAtOnceGive3Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-enhanced-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Reaper's Tithe (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 3)")
            .Reads("Bellowing Giant 💀 killed 3 enemies")
            .Fires(new WeaponPerk.ReapersTitheEnhanced(), "+3 Reaper's Tithe (17s)", "+30% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Reaper's Tithe's 4 kills at once give 4 stacks
          # Rule weapons-perks-reapers-tithe-3: This weapon 4+ kills ➜ Reaper's Tithe (enhanced) equipped ➜ Damage bonus and Reaper's Tithe
          Given the build "reapers-tithe-enhanced-bellowing-giant" is imported, with no steps
          When I tap "Bellowing Giant" in the trigger panel
          And I select "4 · Reaper's Tithe (enhanced) · Reaper's Tithe x4"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Bellowing Giant (💀 kill 4)"
          And it reads "Bellowing Giant 💀 killed 4 enemies"
          And "Reaper's Tithe (enhanced)" fires "+4 Reaper's Tithe (17s)" and "+40% heavy weapon damage"
          And its WEAPONS show only "Reaper's Tithe · Bellowing Giant"
        """)]
    public async Task EnhancedReapersTithes4KillsAtOnceGive4Stacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("reapers-tithe-enhanced-bellowing-giant.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BellowingGiant());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("4 · Reaper's Tithe (enhanced) · Reaper's Tithe x4");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Bellowing Giant (💀 kill 4)")
            .Reads("Bellowing Giant 💀 killed 4 enemies")
            .Fires(new WeaponPerk.ReapersTitheEnhanced(), "+4 Reaper's Tithe (17s)", "+40% heavy weapon damage")
            .Shows(new StateGroup.Weapons(), "Reaper's Tithe · Bellowing Giant");
    }
}
