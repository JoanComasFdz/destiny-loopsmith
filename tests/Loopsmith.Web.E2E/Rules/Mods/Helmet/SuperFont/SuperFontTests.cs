using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Super Font's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class SuperFontTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Super Font raises your Super stat while you hold Armor Charge
          # Rule mods-helmet-super-font-1: Always ➜ Super Font equipped and Armor Charge on you ➜ Super stat (+20 | +40 | +50)
          Given the build "super-font-arc-siphon-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Arc Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          # "1 effect": Super Font's Super stat holds while Armor Charge is on you.
          And its BUFFS show only "Armor Charge x1 · 1 effect"
          When I tap the "Armor Charge x1 · 1 effect" chip in card #2's BUFFS
          Then the item details popup opens, titled "Armor Charge"
          And its "Right then, after #2" card reads only "Super Font: +20 Super"
        """)]
    public async Task SuperFontRaisesYourSuperStatWhileYouHoldArmorCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("super-font-arc-siphon-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 3 s · Arc Siphon");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 2)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // "1 effect": Super Font's Super stat holds while Armor Charge is on you.
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");

        // When
        await Frontend.Designer.Loop.Step(2).ClickStateChipAsync(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Keyword.ArmorCharge())
            .HasRightThen("Right then, after #2", "Super Font: +20 Super");
    }
}
