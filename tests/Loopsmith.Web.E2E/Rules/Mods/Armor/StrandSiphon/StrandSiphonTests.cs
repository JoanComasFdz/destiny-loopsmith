using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Strand Siphon's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class StrandSiphonTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Strand Siphon's two kills at once with a Strand weapon drop an Orb of Power
          # Rule mods-armor-strand-siphon-1: Strand weapon 2+ kills ➜ Strand Siphon equipped ➜ Orb of Power
          Given the build "strand-siphon-crux-celeritas" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 in 3 s · Strand Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And it reads "Crux Celeritas IV 💀 killed 2 enemies in 3 s"
          And "Strand Siphon" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task StrandSiphonsTwoKillsAtOnceWithAStrandWeaponDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("strand-siphon-crux-celeritas.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Strand Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crux Celeritas IV (💀 kill 2)")
            .Reads("Crux Celeritas IV 💀 killed 2 enemies in 3 s")
            .Fires(new ArmorMod.StrandSiphon(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Strand Siphon's single kill with a Strand weapon may drop an Orb of Power
          # Rule mods-armor-strand-siphon-2: Strand weapon fewer than 2 kills ➜ Strand Siphon equipped ➜ Orb of Power (chance)
          Given the build "strand-siphon-crux-celeritas" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill)"
          And it reads "Crux Celeritas IV 💀 killed 1 enemy"
          And "Strand Siphon" fires "Orb of Power (chance)"
          And its GROUND shows only "Orb of Power (chance)"
        """)]
    public async Task StrandSiphonsSingleKillWithAStrandWeaponMayDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("strand-siphon-crux-celeritas.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CruxCeleritasIv());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crux Celeritas IV (💀 kill)")
            .Reads("Crux Celeritas IV 💀 killed 1 enemy")
            .Fires(new ArmorMod.StrandSiphon(), "Orb of Power (chance)")
            .Shows(new StateGroup.Ground(), "Orb of Power (chance)");
    }
}
