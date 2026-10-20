using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Kinetic Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class KineticSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Siphon's two Kinetic weapon kills at once drop an Orb of Power
          # Rule mods-helmet-kinetic-siphon-1: Kinetic weapon 2+ kills within 3s ➜ Kinetic Siphon equipped ➜ Orb of Power
          Given the build "kinetic-siphon-scathelocke" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "2 in 3 s · Kinetic Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill 2)"
          And it reads "Scathelocke 💀 killed 2 enemies in 3 s"
          And "Kinetic Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task KineticSiphonsTwoKineticWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-siphon-scathelocke.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Scathelocke());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Kinetic Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Scathelocke (💀 kill 2)")
            .Reads("Scathelocke 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.KineticSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Kinetic Siphon's single Kinetic weapon kill may drop an Orb of Power
          # Rule mods-helmet-kinetic-siphon-2: Kinetic weapon fewer than 2 kills ➜ Kinetic Siphon equipped ➜ Orb of Power (chance)
          Given the build "kinetic-siphon-scathelocke" is imported, with no steps
          When I tap "Scathelocke" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Scathelocke (💀 kill)"
          And it reads "Scathelocke 💀 killed 1 enemy"
          And "Kinetic Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task KineticSiphonsSingleKineticWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("kinetic-siphon-scathelocke.build.yaml");
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
            .Fires(new ArmorMod.KineticSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
