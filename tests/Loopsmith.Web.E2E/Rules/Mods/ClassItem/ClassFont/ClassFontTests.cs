using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.ClassItem;

/// <summary>Class Font's passive (rules/mods/class-item.yaml), one scenario per rule claim.</summary>
public sealed class ClassFontTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Class Font raises your Class stat while you hold Armor Charge
          # Rule mods-class-item-class-font-1: Always ➜ Class Font equipped and Armor Charge on you ➜ Class stat (+20 | +40 | +50)
          Given the build "class-font-empowered-finish" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          Then card #1's heading is "#1 Finisher"
          And its BUFFS show only "Armor Charge x1 · 1 effect"
          When I tap the "Armor Charge x1 · 1 effect" chip in card #1's BUFFS
          Then the item details popup is titled "Armor Charge"
          And under "While Armor Charge" "Class Font" reads "+20 Class"
        """)]
    public async Task ClassFontRaisesYourClassStatWhileYouHoldArmorCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("class-font-empowered-finish.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Finisher")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Buffs(), "Armor Charge x1 · 1 effect");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Keyword.ArmorCharge())
            .ReadsUnder("While Armor Charge", new ArmorMod.ClassFont(), "+20 Class");
    }
}
