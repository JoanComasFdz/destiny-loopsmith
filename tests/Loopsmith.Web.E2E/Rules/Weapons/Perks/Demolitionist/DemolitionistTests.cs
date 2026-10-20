using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Demolitionist's claims (rules/weapons/perks.yaml): its grenade refills the weapon, no reload (ADRs D42).</summary>
public sealed class DemolitionistTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Demolitionist's grenade refills its weapon
          # Rule weapons-perks-demolitionist-2: Grenade cast ➜ Demolitionist equipped ➜ Refill
          Given the build "demolitionist-crisis-inverted" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          And "Demolitionist" fires "refills your energy weapon"
        """)]
    public async Task DemolitionistsGrenadeRefillsItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("demolitionist-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var grenade = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(grenade)
            .HasHeading("#1 Grenade (💀 kill)")
            .Fires(new WeaponPerk.Demolitionist(), "refills your energy weapon");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Demolitionist's kill gives grenade energy
          # Rule weapons-perks-demolitionist-1: This weapon kill ➜ Demolitionist equipped ➜ Grenade energy
          Given the build "demolitionist-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 kill"
          # Crisis Inverted is a Hand Cannon: 10%, not the Fusion Rifles', Glaives', Shotguns' and Sniper Rifles' 20%.
          And "Demolitionist" fires "+10% grenade energy"
        """)]
    public async Task DemolitionistsKillGivesGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("demolitionist-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Crisis Inverted is a Hand Cannon: 10%, not the Fusion Rifles', Glaives', Shotguns' and Sniper Rifles' 20%.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 kill")
            .Fires(new WeaponPerk.Demolitionist(), "+10% grenade energy");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Demolitionist's kill gives grenade energy
          # Rule weapons-perks-demolitionist-1: This weapon kill ➜ Demolitionist (enhanced) equipped ➜ Grenade energy
          Given the build "demolitionist-enhanced-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 kill"
          # Crisis Inverted is a Hand Cannon: 11%, not the Fusion Rifles', Glaives', Shotguns' and Sniper Rifles' 22%.
          And "Demolitionist (enhanced)" fires "+11% grenade energy"
        """)]
    public async Task EnhancedDemolitionistsKillGivesGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("demolitionist-enhanced-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Crisis Inverted is a Hand Cannon: 11%, not the Fusion Rifles', Glaives', Shotguns' and Sniper Rifles' 22%.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 kill")
            .Fires(new WeaponPerk.DemolitionistEnhanced(), "+11% grenade energy");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Demolitionist's grenade refills its weapon
          # Rule weapons-perks-demolitionist-2: Grenade cast ➜ Demolitionist (enhanced) equipped ➜ Refill
          Given the build "demolitionist-enhanced-crisis-inverted" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          And "Demolitionist (enhanced)" fires "refills your energy weapon"
        """)]
    public async Task EnhancedDemolitionistsGrenadeRefillsItsWeapon(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("demolitionist-enhanced-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var grenade = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(grenade)
            .HasHeading("#1 Grenade (💀 kill)")
            .Fires(new WeaponPerk.DemolitionistEnhanced(), "refills your energy weapon");
    }
}
