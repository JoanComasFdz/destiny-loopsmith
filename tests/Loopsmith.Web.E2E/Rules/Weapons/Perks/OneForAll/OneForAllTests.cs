using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>One for All's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class OneForAllTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: One for All's 3 quick hits power up its weapon
          # Rule weapons-perks-one-for-all-1: This weapon 3+ hits within 3s ➜ One for All equipped ➜ Damage bonus and One for All
          Given the build "one-for-all-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · One for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit 3)"
          And it reads "Festival Flight 🥊 hit 3 enemies in 3 s"
          And "One for All" fires "One for All (10s)" and "+35% kinetic weapon damage"
          And its WEAPONS show only "One for All · Festival Flight"
        """)]
    public async Task OneForAlls3QuickHitsPowerUpItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("one-for-all-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit 3)")
            .Reads("Festival Flight 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.OneForAll(), "One for All (10s)", "+35% kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: One for All's fewer hits may power up its weapon
          # Rule weapons-perks-one-for-all-2: This weapon fewer than 3 hits ➜ One for All equipped ➜ Damage bonus and One for All (chance)
          Given the build "one-for-all-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit 1 enemy"
          And "One for All" fires "One for All (10s) (chance)" and "+35% kinetic weapon damage (chance)"
          And its WEAPONS show only "One for All · Festival Flight"
        """)]
    public async Task OneForAllsFewerHitsMayPowerUpItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("one-for-all-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.OneForAll(), "One for All (10s) (chance)", "+35% kinetic weapon damage (chance)")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced One for All's 3 quick hits power up its weapon
          # Rule weapons-perks-one-for-all-1: This weapon 3+ hits within 3s ➜ One for All (enhanced) equipped ➜ Damage bonus and One for All
          Given the build "one-for-all-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · One for All (enhanced)"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit 3)"
          And it reads "Festival Flight 🥊 hit 3 enemies in 3 s"
          And "One for All (enhanced)" fires "One for All (11s)" and "+35% kinetic weapon damage"
          And its WEAPONS show only "One for All · Festival Flight"
          # keeping step #1: its One for All is what the chip shows
          When I tap the "One for All · Festival Flight" chip in card #1's WEAPONS
          Then the item details popup opens, titled "One for All (enhanced)"
          And its "Right then, after #1" card reads only "11s"
          When I close the item details popup
          And I tap "One for All" in "One for All (11s)", which "One for All (enhanced)" fires in card #1
          Then the item details popup opens, titled "One for All (enhanced)"
          And its "Right then, after #1" card reads only "11s"
        """)]
    public async Task EnhancedOneForAlls3QuickHitsPowerUpItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("one-for-all-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · One for All (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit 3)")
            .Reads("Festival Flight 🥊 hit 3 enemies in 3 s")
            .Fires(new WeaponPerk.OneForAllEnhanced(), "One for All (11s)", "+35% kinetic weapon damage")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "One for All · Festival Flight");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.OneForAllEnhanced())
            .HasRightThen("Right then, after #1", "11s");

        // When
        await Frontend.Designer.ItemDetails.ClickCloseAsync();
        await Frontend.Designer.ItemDetails.WaitUntilClosedAsync();
        await Frontend.Designer.Loop.Step(1).ClickFiredKeywordAsync(new WeaponPerk.OneForAll(), "One for All (11s)", new WeaponPerk.OneForAllEnhanced());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var keywordDetails = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(keywordDetails)
            .IsTitled(new WeaponPerk.OneForAllEnhanced())
            .HasRightThen("Right then, after #1", "11s");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced One for All's fewer hits may power up its weapon
          # Rule weapons-perks-one-for-all-2: This weapon fewer than 3 hits ➜ One for All (enhanced) equipped ➜ Damage bonus and One for All (chance)
          Given the build "one-for-all-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit 1 enemy"
          And "One for All (enhanced)" fires "One for All (11s) (chance)" and "+35% kinetic weapon damage (chance)"
          And its WEAPONS show only "One for All · Festival Flight"
        """)]
    public async Task EnhancedOneForAllsFewerHitsMayPowerUpItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("one-for-all-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit 1 enemy")
            .Fires(new WeaponPerk.OneForAllEnhanced(), "One for All (11s) (chance)", "+35% kinetic weapon damage (chance)")
            .Shows(new StateGroup.Weapons(), "One for All · Festival Flight");
    }
}
