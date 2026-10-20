using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Kinetic Thermal Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class KineticThermalSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Thermal Siphon's two Kinetic weapon kills at once drop an Orb of Power
          # Rule mods-helmet-kinetic-thermal-siphon-1: Kinetic weapon 2+ kills within 3s ➜ Kinetic Thermal Siphon equipped ➜ Orb of Power
          Given the build "kinetic-thermal-siphon-scathelocke-ogma-pr6" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "2 in 3 s · Kinetic Thermal Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill 2)"
          And it reads "Scathelocke 💀 killed 2 enemies in 3 s"
          And "Kinetic Thermal Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task KineticThermalSiphonsTwoKineticWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-thermal-siphon-scathelocke-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Kinetic Thermal Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill 2)")
            .Reads("Scathelocke 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.KineticThermalSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Thermal Siphon's single Kinetic weapon kill may drop an Orb of Power
          # Rule mods-helmet-kinetic-thermal-siphon-2: Kinetic weapon fewer than 2 kills ➜ Kinetic Thermal Siphon equipped ➜ Orb of Power (chance)
          Given the build "kinetic-thermal-siphon-scathelocke-ogma-pr6" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Kinetic Thermal Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task KineticThermalSiphonsSingleKineticWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-thermal-siphon-scathelocke-ogma-pr6.build.yaml");
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
            .Fires(new ArmorMod.KineticThermalSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Thermal Siphon's two Solar weapon kills at once drop an Orb of Power
          # Rule mods-helmet-kinetic-thermal-siphon-3: Solar weapon 2+ kills within 3s ➜ Kinetic Thermal Siphon equipped ➜ Orb of Power
          Given the build "kinetic-thermal-siphon-scathelocke-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "2 in 3 s · Kinetic Thermal Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill 2)"
          And it reads "Ogma PR6 💀 killed 2 enemies in 3 s"
          And "Kinetic Thermal Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task KineticThermalSiphonsTwoSolarWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-thermal-siphon-scathelocke-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Kinetic Thermal Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill 2)")
            .Reads("Ogma PR6 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.KineticThermalSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Thermal Siphon's single Solar weapon kill may drop an Orb of Power
          # Rule mods-helmet-kinetic-thermal-siphon-4: Solar weapon fewer than 2 kills ➜ Kinetic Thermal Siphon equipped ➜ Orb of Power (chance)
          Given the build "kinetic-thermal-siphon-scathelocke-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
          And it reads "Ogma PR6 💀 killed 1 enemy"
          And "Kinetic Thermal Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task KineticThermalSiphonsSingleSolarWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-thermal-siphon-scathelocke-ogma-pr6.build.yaml");
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
            .Fires(new ArmorMod.KineticThermalSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
