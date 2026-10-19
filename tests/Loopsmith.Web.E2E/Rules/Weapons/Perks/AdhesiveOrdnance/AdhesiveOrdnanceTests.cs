using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Adhesive Ordnance's rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class AdhesiveOrdnanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Adhesive Ordnance's ammo brick pickup loads a Sticky Bomb
          # Rule weapons-perks-adhesive-ordnance-1: Pick up an ammo brick ➜ Adhesive Ordnance equipped ➜ Sticky Bomb
          Given the build "adhesive-ordnance-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "special"
          Then card #1's heading is "#1 Pick up a Special ammo brick"
          And "Adhesive Ordnance" fires "Sticky Bomb"
          And its WEAPONS show only "Sticky Bomb · Battle Scar"
        """)]
    public async Task AdhesiveOrdnancesAmmoBrickPickupLoadsAStickyBomb(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("adhesive-ordnance-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Special ammo brick")
            .Fires(new WeaponPerk.AdhesiveOrdnance(), "Sticky Bomb")
            .Shows(new StateGroup.Weapons(), "Sticky Bomb · Battle Scar");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Adhesive Ordnance's Sticky Bomb strikes and Exhausts on the next hit
          # Rule weapons-perks-adhesive-ordnance-2: This weapon hit ➜ Adhesive Ordnance equipped and Sticky Bomb on you ➜ Extra strike, Exhaust and Sticky Bomb ends
          Given the build "adhesive-ordnance-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "special"
          And I tap "Battle Scar" in the trigger panel
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Battle Scar (🥊 hit)"
          And "Adhesive Ordnance" fires "Exhaust target", "consumes Sticky Bomb" and "kinetic weapon strike, Kinetic damage"
          And its DEBUFFS show only "Exhaust"
          And its WEAPONS show none
        """)]
    public async Task AdhesiveOrdnancesStickyBombStrikesAndExhaustsOnTheNextHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("adhesive-ordnance-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Battle Scar (🥊 hit)")
            .Fires(new WeaponPerk.AdhesiveOrdnance(), "Exhaust target", "consumes Sticky Bomb", "kinetic weapon strike, Kinetic damage")
            .Shows(new StateGroup.Debuffs(), "Exhaust")
            .ShowsNone(new StateGroup.Weapons());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Adhesive Ordnance's ammo brick pickup loads a Sticky Bomb
          # Rule weapons-perks-adhesive-ordnance-1: Pick up an ammo brick ➜ Adhesive Ordnance (enhanced) equipped ➜ Sticky Bomb
          Given the build "adhesive-ordnance-enhanced-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "special"
          Then card #1's heading is "#1 Pick up a Special ammo brick"
          And "Adhesive Ordnance (enhanced)" fires "Sticky Bomb"
          And its WEAPONS show only "Sticky Bomb · Battle Scar"
        """)]
    public async Task EnhancedAdhesiveOrdnancesAmmoBrickPickupLoadsAStickyBomb(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("adhesive-ordnance-enhanced-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Special ammo brick")
            .Fires(new WeaponPerk.AdhesiveOrdnanceEnhanced(), "Sticky Bomb")
            .Shows(new StateGroup.Weapons(), "Sticky Bomb · Battle Scar");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Adhesive Ordnance's Sticky Bomb strikes and Exhausts on the next hit
          # Rule weapons-perks-adhesive-ordnance-2: This weapon hit ➜ Adhesive Ordnance (enhanced) equipped and Sticky Bomb on you ➜ Extra strike, Exhaust and Sticky Bomb ends
          Given the build "adhesive-ordnance-enhanced-battle-scar" is imported, with no steps
          When I tap "📦 Ammo brick" in the trigger panel
          And I tap "special"
          Then card #1's heading is "#1 Pick up a Special ammo brick"
          And its WEAPONS show only "Sticky Bomb · Battle Scar"
          # keeping step #1: its Sticky Bomb is what #2's hit sticks
          When I tap "Battle Scar" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #2's heading is "#2 Battle Scar (🥊 hit)"
          And "Adhesive Ordnance (enhanced)" fires "Exhaust target", "consumes Sticky Bomb" and "kinetic weapon strike, Kinetic damage"
          And its DEBUFFS show only "Exhaust"
          And its WEAPONS show none
        """)]
    public async Task EnhancedAdhesiveOrdnancesStickyBombStrikesAndExhaustsOnTheNextHit(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("adhesive-ordnance-enhanced-battle-scar.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAmmoBrickAsync();
        await Frontend.Designer.TriggerPalette.WaitUntilAmmoBrickRowShownAsync();
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.PickUp(new Brick.Special()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var picked = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(picked)
            .HasHeading("#1 Pick up a Special ammo brick")
            .Shows(new StateGroup.Weapons(), "Sticky Bomb · Battle Scar");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.BattleScar());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var hit = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#2 Battle Scar (🥊 hit)")
            .Fires(new WeaponPerk.AdhesiveOrdnanceEnhanced(), "Exhaust target", "consumes Sticky Bomb", "kinetic weapon strike, Kinetic damage")
            .Shows(new StateGroup.Debuffs(), "Exhaust")
            .ShowsNone(new StateGroup.Weapons());
    }
}
