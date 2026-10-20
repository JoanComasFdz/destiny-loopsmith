using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Legs;

/// <summary>Arc Weapon Surge's rules (rules/mods/legs.yaml), one scenario per rule claim.</summary>
public sealed class ArcWeaponSurgeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc Weapon Surge makes Arc weapons hit harder while Armor Charge is on you
          # Rule mods-legs-arc-weapon-surge-1: Always ➜ Arc Weapon Surge equipped and Armor Charge on you ➜ Damage bonus (+10% | +17% | +22% | +25% vs Arc weapon)
          Given the build "arc-weapon-surge-spark-of-amplitude" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I select "2 in 4 s · Spark of Amplitude"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          And I tap "▸ DAMAGE BONUS" on card #2
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Amplified", "Bolt Charge" and "Armor Charge x1 · 1 effect"
          And its "Arc weapon" damage bonus reads "+10%"
          And "Arc Weapon Surge" gives "+10%" of it
        """)]
    public async Task ArcWeaponSurgeMakesArcWeaponsHitHarderWhileArmorChargeIsOnYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-weapon-surge-spark-of-amplitude.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("2 in 4 s · Spark of Amplitude");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Melee (💀 kill 2)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);
        await Frontend.Designer.Loop.Step(2).ClickDamageBonusAsync();
        await Frontend.Designer.Loop.Step(2).WaitUntilDamageBonusSourcesShownAsync();

        // Then
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge", "Armor Charge x1 · 1 effect")
            .HasDamageBonus("Arc weapon", "+10%")
            .HasDamageBonusFrom(new ArmorMod.ArcWeaponSurge(), "+10%");
    }
}
