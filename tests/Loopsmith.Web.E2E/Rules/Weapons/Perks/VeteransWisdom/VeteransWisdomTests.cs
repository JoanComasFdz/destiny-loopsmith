using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Veteran's Wisdom's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class VeteransWisdomTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Veteran's Wisdom's 3 quick kills set off a shockwave
          # Rule weapons-perks-veterans-wisdom-1: This weapon 3+ kills within 3s ➜ Veteran's Wisdom equipped ➜ Extra strike and Exhaust
          Given the build "veterans-wisdom-scathelocke" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "3 in 3 s · Veteran's Wisdom"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill 3)"
          And it reads "Scathelocke 💀 killed 3 enemies in 3 s"
          And "Veteran's Wisdom" fires "Exhaust target (10s)" and "kinetic weapon strike, Kinetic damage"
          And its DEBUFFS show only "Exhaust"
        """)]
    public async Task VeteransWisdoms3QuickKillsSetOffAShockwave(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("veterans-wisdom-scathelocke.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Veteran's Wisdom");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill 3)")
            .Reads("Scathelocke 💀 killed 3 enemies in 3 s")
            .Fires(new WeaponPerk.VeteransWisdom(), "Exhaust target (10s)", "kinetic weapon strike, Kinetic damage")
            .Shows(new StateGroup.Debuffs(), "Exhaust");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Veteran's Wisdom's precision kill sets off a shockwave
          # Rule weapons-perks-veterans-wisdom-3: This weapon precision fewer than 3 kills ➜ Veteran's Wisdom equipped ➜ Extra strike and Exhaust
          Given the build "veterans-wisdom-scathelocke" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tick "🎯 precision"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill · 🎯 precision)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Veteran's Wisdom" fires "Exhaust target (10s)" and "kinetic weapon strike, Kinetic damage"
          And its DEBUFFS show only "Exhaust"
        """)]
    public async Task VeteransWisdomsPrecisionKillSetsOffAShockwave(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("veterans-wisdom-scathelocke.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Precision());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill · 🎯 precision)")
            .Reads("Scathelocke 💀 killed 1 enemy")
            .Fires(new WeaponPerk.VeteransWisdom(), "Exhaust target (10s)", "kinetic weapon strike, Kinetic damage")
            .Shows(new StateGroup.Debuffs(), "Exhaust");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Veteran's Wisdom's body kills may set off a shockwave
          # Rule weapons-perks-veterans-wisdom-2: This weapon body fewer than 3 kills ➜ Veteran's Wisdom equipped ➜ Extra strike and Exhaust (chance)
          Given the build "veterans-wisdom-scathelocke" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Veteran's Wisdom" fires "Exhaust target (10s) (chance)" and "kinetic weapon strike, Kinetic damage (chance)"
          And its DEBUFFS show only "Exhaust"
        """)]
    public async Task VeteransWisdomsBodyKillsMaySetOffAShockwave(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("veterans-wisdom-scathelocke.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill)")
            .Reads("Scathelocke 💀 killed 1 enemy")
            .Fires(new WeaponPerk.VeteransWisdom(), "Exhaust target (10s) (chance)", "kinetic weapon strike, Kinetic damage (chance)")
            .Shows(new StateGroup.Debuffs(), "Exhaust");
    }
}
