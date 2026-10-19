using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Hatchling's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class HatchlingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hatchling's 3 quick kills hatch a Threadling
          # Rule weapons-perks-hatchling-1: This weapon 3+ kills within 3s ➜ Hatchling equipped ➜ Threadling
          Given the build "hatchling-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          And "Hatchling" fires "Threadling"
        """)]
    public async Task Hatchlings3QuickKillsHatchAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.Hatchling(), "Threadling");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hatchling's precision kill hatches a Threadling
          # Rule weapons-perks-hatchling-5: This weapon precision fewer than 3 kills ➜ Hatchling equipped ➜ Threadling
          Given the build "hatchling-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tick "🎯 precision"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill · 🎯 precision)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Hatchling" fires "Threadling"
        """)]
    public async Task HatchlingsPrecisionKillHatchesAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Precision());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill · 🎯 precision)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.Hatchling(), "Threadling");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hatchling's body kills may hatch a Threadling
          # Rule weapons-perks-hatchling-2: This weapon body fewer than 3 kills ➜ Hatchling equipped ➜ Threadling (chance)
          Given the build "hatchling-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Hatchling" fires "Threadling (chance)"
        """)]
    public async Task HatchlingsBodyKillsMayHatchAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.Hatchling(), "Threadling (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling may hatch a second Threadling on 3 quick kills
          # Rule weapons-perks-hatchling-3: This weapon 3+ kills within 3s ➜ Hatchling (enhanced) equipped ➜ Threadling (chance)
          Given the build "hatchling-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          And "Hatchling (enhanced)" fires "Threadling" and "Threadling (chance)"
        """)]
    public async Task EnhancedHatchlingMayHatchASecondThreadlingOn3QuickKills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling", "Threadling (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling may hatch a second Threadling on fewer kills
          # Rule weapons-perks-hatchling-4: This weapon fewer than 3 kills ➜ Hatchling (enhanced) equipped ➜ Threadling (chance)
          Given the build "hatchling-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tick "🎯 precision"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill · 🎯 precision)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          And "Hatchling (enhanced)" fires "Threadling" and "Threadling (chance)"
        """)]
    public async Task EnhancedHatchlingMayHatchASecondThreadlingOnFewerKills(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Precision());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill · 🎯 precision)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling", "Threadling (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling's 3 quick kills hatch a Threadling
          # Rule weapons-perks-hatchling-1: This weapon 3+ kills within 3s ➜ Hatchling (enhanced) equipped ➜ Threadling
          Given the build "hatchling-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Hatchling (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill 3)"
          And it reads "Festival Flight 💀 killed 3 enemies in 3 s"
          # Two Threadlings: the one every Hatchling gives, and the extra one only the enhanced trait may give.
          And "Hatchling (enhanced)" fires "Threadling" and "Threadling (chance)"
        """)]
    public async Task EnhancedHatchlings3QuickKillsHatchAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Hatchling (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill 3)")
            .Reads("Festival Flight 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling", "Threadling (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling's precision kill hatches a Threadling
          # Rule weapons-perks-hatchling-5: This weapon precision fewer than 3 kills ➜ Hatchling (enhanced) equipped ➜ Threadling
          Given the build "hatchling-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tick "🎯 precision"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill · 🎯 precision)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          # Two Threadlings: the one every Hatchling gives, and the extra one only the enhanced trait may give.
          And "Hatchling (enhanced)" fires "Threadling" and "Threadling (chance)"
        """)]
    public async Task EnhancedHatchlingsPrecisionKillHatchesAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Precision());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill · 🎯 precision)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling", "Threadling (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Hatchling's body kills may hatch a Threadling
          # Rule weapons-perks-hatchling-2: This weapon body fewer than 3 kills ➜ Hatchling (enhanced) equipped ➜ Threadling (chance)
          Given the build "hatchling-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Festival Flight (💀 kill)"
          And it reads "Festival Flight 💀 killed 1 enemy"
          # Two Threadlings: the one every Hatchling gives, and the extra one only the enhanced trait may give.
          And "Hatchling (enhanced)" fires "Threadling (chance)" and "Threadling (chance)"
        """)]
    public async Task EnhancedHatchlingsBodyKillsMayHatchAThreadling(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hatchling-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Festival Flight (💀 kill)")
            .Reads("Festival Flight 💀 killed 1 enemy")
            .Fires(new WeaponPerk.HatchlingEnhanced(), "Threadling (chance)", "Threadling (chance)");
    }
}
