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
}
