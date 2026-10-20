using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Radiant Light's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class RadiantLightTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Radiant Light's Super cast with Armor Charge gives one more
          # Rule mods-helmet-radiant-light-1: Super cast ➜ Radiant Light equipped and Armor Charge on you ➜ Armor Charge
          Given the build "radiant-light-arc-siphon-crisis-inverted-storms-edge" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "2 in 3 s · Arc Siphon"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 2)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And its BUFFS show only "Armor Charge x1"
          # keeping step #2: its Armor Charge is what #3's Super adds to
          When I tap "Storm's Edge" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Super (🥊 hit)"
          And "Radiant Light" fires "+1 Armor Charge"
          And its BUFFS show only "Armor Charge x2" and "Super · 1 effect"
        """)]
    public async Task RadiantLightsSuperCastWithArmorChargeGivesOneMore(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("radiant-light-arc-siphon-crisis-inverted-storms-edge.build.yaml");
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
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Shows(new StateGroup.Buffs(), "Armor Charge x1");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var cast = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(cast)
            .HasHeading("#3 Super (🥊 hit)")
            .Fires(new ArmorMod.RadiantLight(), "+1 Armor Charge")
            .Shows(new StateGroup.Buffs(), "Armor Charge x2", "Super · 1 effect");
    }
}
