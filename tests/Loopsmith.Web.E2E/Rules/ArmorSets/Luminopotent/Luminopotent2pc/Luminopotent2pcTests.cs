using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.Luminopotent;

/// <summary>Luminopotent's 2-piece bonus, Ionic Overclock (rules/armor-sets/luminopotent.yaml), one scenario per rule claim.</summary>
public sealed class Luminopotent2pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ionic Overclock raises Fusion Rifles' stats while you're Amplified
          # Rule armor-sets-luminopotent-luminopotent-2pc-1: Always ➜ Ionic Overclock equipped and Amplified on you ➜ Weapon stats
          Given the build "luminopotent-2pc-techeun-force" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified · 1 effect" and "Bolt Charge"
          And its WEAPONS show only "Ionic Overclock · Techeun Force"
          # keeping step #1: its Amplified is what the stats need
          When I tap the "Ionic Overclock · Techeun Force" chip in card #1's WEAPONS
          Then the item details popup opens, titled "Ionic Overclock"
          And its "Right then, after #1" card reads only "Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload Speed"
          When I close the item details popup
          And I tap the "Amplified · 1 effect" chip in card #1's BUFFS
          Then the item details popup opens, titled "Amplified"
          And its "Right then, after #1" card reads only "15s" and "Ionic Overclock: Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload Speed"
        """)]
    public async Task IonicOverclockRaisesFusionRiflesStatsWhileYoureAmplified(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("luminopotent-2pc-techeun-force.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified · 1 effect", "Bolt Charge")
            .Shows(new StateGroup.Weapons(), "Ionic Overclock · Techeun Force");

        // When
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Weapons(), "Ionic Overclock · Techeun Force");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var overclock = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(overclock)
            .IsTitled(new ArmorSetBonus.IonicOverclock())
            .HasRightThen("Right then, after #1", "Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload Speed");

        // When
        await Frontend.Designer.ItemDetails.ClickCloseAsync();
        await Frontend.Designer.ItemDetails.WaitUntilClosedAsync();
        await Frontend.Designer.Loop.Step(1).ClickStateChipAsync(new StateGroup.Buffs(), "Amplified · 1 effect");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var amplified = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(amplified)
            .IsTitled(new Keyword.Amplified())
            .HasRightThen("Right then, after #1", "15s", "Ionic Overclock: Linear Fusion Rifles, Fusion Rifles and Heat Weapons: +? Handling, +? Reload Speed");
    }
}
