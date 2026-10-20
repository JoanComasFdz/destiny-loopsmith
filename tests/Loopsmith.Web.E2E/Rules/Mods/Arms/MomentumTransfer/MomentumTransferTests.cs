using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Momentum Transfer's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class MomentumTransferTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Momentum Transfer's grenade hit gives melee energy
          # Rule mods-arms-momentum-transfer-1: Grenade hit ➜ Momentum Transfer equipped ➜ Melee energy (12% | 17% | 20%)
          Given the build "momentum-transfer-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And it reads "Grenade 🥊 hit"
          And "Momentum Transfer" fires "+12% melee energy"
        """)]
    public async Task MomentumTransfersGrenadeHitGivesMeleeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("momentum-transfer-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Reads("Grenade 🥊 hit")
            .Fires(new ArmorMod.MomentumTransfer(), "+12% melee energy");
    }
}
