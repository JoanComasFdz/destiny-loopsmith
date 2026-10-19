using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Unrelenting's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class UnrelentingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unrelenting's 3 quick kills heal you
          # Rule weapons-perks-unrelenting-1: This weapon 3+ kills within 5s ➜ Unrelenting equipped ➜ Health
          Given the build "unrelenting-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 5 s · Unrelenting"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill 3)"
          And it reads "Ogma PR6 💀 killed 3 enemies in 5 s"
          And "Unrelenting" fires "heals you 65 HP"
        """)]
    public async Task Unrelentings3QuickKillsHealYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unrelenting-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 5 s · Unrelenting");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill 3)")
            .Reads("Ogma PR6 💀 killed 3 enemies in 5 s")
            .Fires(new WeaponPerk.Unrelenting(), "heals you 65 HP");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unrelenting's fewer kills may heal you
          # Rule weapons-perks-unrelenting-2: This weapon fewer than 3 kills ➜ Unrelenting equipped ➜ Health (chance)
          Given the build "unrelenting-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
          And it reads "Ogma PR6 💀 killed 1 enemy"
          And "Unrelenting" fires "heals you 65 HP (chance)"
        """)]
    public async Task UnrelentingsFewerKillsMayHealYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unrelenting-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill)")
            .Reads("Ogma PR6 💀 killed 1 enemy")
            .Fires(new WeaponPerk.Unrelenting(), "heals you 65 HP (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Unrelenting's 3 quick kills heal you
          # Rule weapons-perks-unrelenting-1: This weapon 3+ kills within 5s ➜ Unrelenting (enhanced) equipped ➜ Health
          Given the build "unrelenting-enhanced-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 5 s · Unrelenting (enhanced)"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill 3)"
          And it reads "Ogma PR6 💀 killed 3 enemies in 5 s"
          And "Unrelenting (enhanced)" fires "heals you 70 HP"
        """)]
    public async Task EnhancedUnrelentings3QuickKillsHealYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unrelenting-enhanced-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 5 s · Unrelenting (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill 3)")
            .Reads("Ogma PR6 💀 killed 3 enemies in 5 s")
            .Fires(new WeaponPerk.UnrelentingEnhanced(), "heals you 70 HP");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Unrelenting's fewer kills may heal you
          # Rule weapons-perks-unrelenting-2: This weapon fewer than 3 kills ➜ Unrelenting (enhanced) equipped ➜ Health (chance)
          Given the build "unrelenting-enhanced-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
          And it reads "Ogma PR6 💀 killed 1 enemy"
          And "Unrelenting (enhanced)" fires "heals you 70 HP (chance)"
        """)]
    public async Task EnhancedUnrelentingsFewerKillsMayHealYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unrelenting-enhanced-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill)")
            .Reads("Ogma PR6 💀 killed 1 enemy")
            .Fires(new WeaponPerk.UnrelentingEnhanced(), "heals you 70 HP (chance)");
    }
}
