using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Kinetic Charge Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class KineticChargeSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Charge Siphon's two Kinetic weapon kills at once drop an Orb of Power
          # Rule mods-helmet-kinetic-charge-siphon-1: Kinetic weapon 2+ kills within 3s ➜ Kinetic Charge Siphon equipped ➜ Orb of Power
          Given the build "kinetic-charge-siphon-scathelocke-crisis-inverted" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "2 in 3 s · Kinetic Charge Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill 2)"
          And it reads "Scathelocke 💀 killed 2 enemies in 3 s"
          And "Kinetic Charge Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task KineticChargeSiphonsTwoKineticWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-charge-siphon-scathelocke-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Kinetic Charge Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill 2)")
            .Reads("Scathelocke 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.KineticChargeSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Charge Siphon's single Kinetic weapon kill may drop an Orb of Power
          # Rule mods-helmet-kinetic-charge-siphon-2: Kinetic weapon fewer than 2 kills ➜ Kinetic Charge Siphon equipped ➜ Orb of Power (chance)
          Given the build "kinetic-charge-siphon-scathelocke-crisis-inverted" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Kinetic Charge Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task KineticChargeSiphonsSingleKineticWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-charge-siphon-scathelocke-crisis-inverted.build.yaml");
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
            .Fires(new ArmorMod.KineticChargeSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Charge Siphon's two Arc weapon kills at once drop an Orb of Power
          # Rule mods-helmet-kinetic-charge-siphon-3: Arc weapon 2+ kills within 3s ➜ Kinetic Charge Siphon equipped ➜ Orb of Power
          Given the build "kinetic-charge-siphon-scathelocke-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Kinetic Charge Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And it reads "Crisis Inverted 💀 killed 2 enemies in 3 s"
          And "Kinetic Charge Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task KineticChargeSiphonsTwoArcWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-charge-siphon-scathelocke-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Kinetic Charge Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 2)")
            .Reads("Crisis Inverted 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.KineticChargeSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Charge Siphon's single Arc weapon kill may drop an Orb of Power
          # Rule mods-helmet-kinetic-charge-siphon-4: Arc weapon fewer than 2 kills ➜ Kinetic Charge Siphon equipped ➜ Orb of Power (chance)
          Given the build "kinetic-charge-siphon-scathelocke-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 killed 1 enemy"
          And "Kinetic Charge Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task KineticChargeSiphonsSingleArcWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-charge-siphon-scathelocke-crisis-inverted.build.yaml");
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
            .Fires(new ArmorMod.KineticChargeSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
