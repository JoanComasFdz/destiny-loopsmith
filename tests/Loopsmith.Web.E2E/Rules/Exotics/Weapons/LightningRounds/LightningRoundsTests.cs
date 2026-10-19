using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Exotics.Weapons;

/// <summary>Lightning Rounds's rules (rules/exotics/weapons.yaml): Thunderlord's trait, one scenario per rule claim.</summary>
public sealed class LightningRoundsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Lightning Rounds's 10th-hit strike gives Bolt Charge
          # Rule exotics-weapons-lightning-rounds-1: Power weapon sustained hit ➜ Lightning Rounds equipped ➜ Bolt Charge
          Given the build "lightning-rounds-thunderlord" is imported, with no steps
          When I tap "Thunderlord" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Thunderlord (🥊 hit)"
          And "Lightning Rounds" fires "+1 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task LightningRoundss10thHitStrikeGivesBoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("lightning-rounds-thunderlord.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Thunderlord (🥊 hit)")
            .Fires(new ExoticWeaponPerk.LightningRounds(), "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }
}
