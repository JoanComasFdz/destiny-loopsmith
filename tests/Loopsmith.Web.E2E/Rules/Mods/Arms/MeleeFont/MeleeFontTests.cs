using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Melee Font's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class MeleeFontTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Melee Font raises the Melee stat while you hold Armor Charge
          # Rule mods-arms-melee-font-1: Always ➜ Melee Font equipped and Armor Charge on you ➜ Melee stat (+20 | +40 | +50)
          Given the build "melee-font-shield-break-charge-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I tick "🛡 shield"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Crisis Inverted (🥊 hit · 🛡 shield)"
          And its BUFFS show only "Armor Charge x1 · 1 effect"
          When I tap the "Armor Charge x1 · 1 effect" chip in card #1's BUFFS
          Then the item details popup opens, titled "Armor Charge"
          And its "Right then, after #1" card reads only "Melee Font: +20 Melee"
        """)]
    public async Task MeleeFontRaisesTheMeleeStatWhileYouHoldArmorCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("melee-font-shield-break-charge-crisis-inverted.build.yaml");
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
            .Shows(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Keyword.ArmorCharge())
            .HasRightThen("Right then, after #1", "Melee Font: +20 Melee");
    }
}
