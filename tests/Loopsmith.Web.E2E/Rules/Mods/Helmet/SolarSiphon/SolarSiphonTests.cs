using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Solar Siphon's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class SolarSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Solar Siphon's two Solar weapon kills at once drop an Orb of Power
          # Rule mods-helmet-solar-siphon-1: Solar weapon 2+ kills within 3s ➜ Solar Siphon equipped ➜ Orb of Power
          Given the build "solar-siphon-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "2 in 3 s · Solar Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill 2)"
          And it reads "Ogma PR6 💀 killed 2 enemies in 3 s"
          And "Solar Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task SolarSiphonsTwoSolarWeaponKillsAtOnceDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("solar-siphon-ogma-pr6.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Solar Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Ogma PR6 (💀 kill 2)")
            .Reads("Ogma PR6 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.SolarSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Solar Siphon's single Solar weapon kill may drop an Orb of Power
          # Rule mods-helmet-solar-siphon-2: Solar weapon fewer than 2 kills ➜ Solar Siphon equipped ➜ Orb of Power (chance)
          Given the build "solar-siphon-ogma-pr6" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Ogma PR6 (💀 kill)"
          And it reads "Ogma PR6 💀 killed 1 enemy"
          And "Solar Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task SolarSiphonsSingleSolarWeaponKillMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("solar-siphon-ogma-pr6.build.yaml");
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
            .Fires(new ArmorMod.SolarSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
