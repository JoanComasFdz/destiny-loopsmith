using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Arc/Stasis Dual Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class ArcStasisDualSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Stasis Dual Siphon's two Arc weapon kills at once drop an Orb of Power
          # Rule mods-helmet-arc-stasis-dual-siphon-1: Arc weapon 2+ kills within 3s ➜ Arc/Stasis Dual Siphon equipped ➜ Orb of Power
          Given the build "arc-stasis-dual-siphon-crisis-inverted-stay-frosty" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Arc/Stasis Dual Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And it reads "Crisis Inverted 💀 killed 2 enemies in 3 s"
          And "Arc/Stasis Dual Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task ArcStasisDualSiphonsTwoArcWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-stasis-dual-siphon-crisis-inverted-stay-frosty.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Arc/Stasis Dual Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 2)")
            .Reads("Crisis Inverted 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.ArcStasisDualSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Stasis Dual Siphon's single Arc weapon kill may drop an Orb of Power
          # Rule mods-helmet-arc-stasis-dual-siphon-2: Arc weapon fewer than 2 kills ➜ Arc/Stasis Dual Siphon equipped ➜ Orb of Power (chance)
          Given the build "arc-stasis-dual-siphon-crisis-inverted-stay-frosty" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 killed 1 enemy"
          And "Arc/Stasis Dual Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task ArcStasisDualSiphonsSingleArcWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-stasis-dual-siphon-crisis-inverted-stay-frosty.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 killed 1 enemy")
            .Fires(new ArmorMod.ArcStasisDualSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Stasis Dual Siphon's two Stasis weapon kills at once drop an Orb of Power
          # Rule mods-helmet-arc-stasis-dual-siphon-3: Stasis weapon 2+ kills within 3s ➜ Arc/Stasis Dual Siphon equipped ➜ Orb of Power
          Given the build "arc-stasis-dual-siphon-crisis-inverted-stay-frosty" is imported, with no steps
          When I tap "Stay Frosty" in the trigger panel
          And I select "2 in 3 s · Arc/Stasis Dual Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Stay Frosty (💀 kill 2)"
          And it reads "Stay Frosty 💀 killed 2 enemies in 3 s"
          And "Arc/Stasis Dual Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task ArcStasisDualSiphonsTwoStasisWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-stasis-dual-siphon-crisis-inverted-stay-frosty.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.StayFrosty());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.StayFrosty());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Arc/Stasis Dual Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Stay Frosty (💀 kill 2)")
            .Reads("Stay Frosty 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.ArcStasisDualSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc/Stasis Dual Siphon's single Stasis weapon kill may drop an Orb of Power
          # Rule mods-helmet-arc-stasis-dual-siphon-4: Stasis weapon fewer than 2 kills ➜ Arc/Stasis Dual Siphon equipped ➜ Orb of Power (chance)
          Given the build "arc-stasis-dual-siphon-crisis-inverted-stay-frosty" is imported, with no steps
          When I tap "Stay Frosty" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Stay Frosty (💀 kill)"
          And it reads "Stay Frosty 💀 killed 1 enemy"
          And "Arc/Stasis Dual Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task ArcStasisDualSiphonsSingleStasisWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-stasis-dual-siphon-crisis-inverted-stay-frosty.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.StayFrosty());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.StayFrosty());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Stay Frosty (💀 kill)")
            .Reads("Stay Frosty 💀 killed 1 enemy")
            .Fires(new ArmorMod.ArcStasisDualSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
