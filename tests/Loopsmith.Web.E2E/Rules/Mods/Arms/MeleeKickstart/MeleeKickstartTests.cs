using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Melee Kickstart's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class MeleeKickstartTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Melee Kickstart's last melee charge spends Armor Charge for melee energy
          # Rule mods-arms-melee-kickstart-1: Melee (last charge) ➜ Melee Kickstart equipped and Armor Charge on you ➜ Melee energy (16.2% | 21.7% | 28.4% | 34.4% | 38.4% | 41.3% | 43.4% | 45% by charges from 2), ends Armor Charge (spends all)
          Given the build "melee-kickstart-shield-break-charge-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I tick "🛡 shield"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Crisis Inverted (🥊 hit · 🛡 shield)"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #1: its Armor Charge is what #2 spends
          # Combination Blow has one charge: every use is its last, and 1 copy with 1 stack is the table's 2 charges.
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Melee (🥊 hit)"
          And it reads "Melee energy fully expended"
          And "Melee Kickstart" fires "spends Armor Charge x1 → +16.2% melee energy"
          And its BUFFS show none
        """)]
    public async Task MeleeKickstartsLastMeleeChargeSpendsArmorChargeForMeleeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("melee-kickstart-shield-break-charge-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Shield());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Crisis Inverted (🥊 hit · 🛡 shield)")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");

        // When
        // Combination Blow has one charge: every use is its last, and 1 copy with 1 stack is the table's 2 charges.
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var punched = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(punched)
            .HasHeading("#2 Melee (🥊 hit)")
            .Reads("Melee energy fully expended")
            .Fires(new ArmorMod.MeleeKickstart(), "spends Armor Charge x1 → +16.2% melee energy")
            .ShowsNone(new StateGroup.Buffs());
    }
}
