using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Grenade Kickstart's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class GrenadeKickstartTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Grenade Kickstart's last grenade charge spends Armor Charge for grenade energy
          # Rule mods-armor-grenade-kickstart-1: Grenade (last charge) ➜ Grenade Kickstart equipped and Armor Charge on you ➜ Grenade energy, ends Armor Charge
          Given the build "grenade-kickstart-skip-grenade-crux-celeritas-strand-siphon" is imported, with no steps
          When I tap "Crux Celeritas IV" in the trigger panel
          And I select "2 in 3 s · Strand Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crux Celeritas IV (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #2: its Armor Charge is what #3 spends
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Grenade (🥊 hit)"
          # Skip Grenade has one charge: every throw is its last. One mod and one stack are two charges: 16.2%.
          And on "Grenade energy fully expended" "Grenade Kickstart" fires "spends Armor Charge x1 → +16.2% grenade energy"
          And its BUFFS show none
        """)]
    public async Task GrenadeKickstartsLastGrenadeChargeSpendsArmorChargeForGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("grenade-kickstart-skip-grenade-crux-celeritas-strand-siphon.build.yaml");
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
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        // Skip Grenade has one charge: every throw is its last. One mod and one stack are two charges: 16.2%.
        var thrown = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(thrown)
            .HasHeading("#3 Grenade (🥊 hit)")
            .FiresOn("Grenade energy fully expended", new ArmorMod.GrenadeKickstart(), "spends Armor Charge x1 → +16.2% grenade energy")
            .ShowsNone(new StateGroup.Buffs());
    }
}
