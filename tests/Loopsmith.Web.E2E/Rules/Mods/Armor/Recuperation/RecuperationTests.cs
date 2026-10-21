using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Recuperation's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class RecuperationTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Recuperation's Orb of Power pickup heals you 16 HP
          # Rule mods-armor-recuperation-1: Pick up Orb of Power ➜ Recuperation equipped ➜ Health (16 | 21 | 21 HP)
          Given the build "recuperation-crux-celeritas-strand-siphon" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 in 3 s · Strand Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          # One Recuperation is equipped: its 16 HP of 16 | 21 | 21
          And on "📍 Picked up Orb of Power" "Recuperation" fires "heals you 16 HP"
        """)]
    public async Task RecuperationsOrbOfPowerPickupHealsYou16HP(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("recuperation-crux-celeritas-strand-siphon.build.yaml");
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
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // One Recuperation is equipped: its 16 HP of 16 | 21 | 21
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .FiresOn("📍 Picked up Orb of Power", new ArmorMod.Recuperation(), "heals you 16 HP");
    }
}
