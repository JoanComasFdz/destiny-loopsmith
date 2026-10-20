using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Bolstering Detonation's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class BolsteringDetonationTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Bolstering Detonation's grenade hit gives class ability energy
          # Rule mods-arms-bolstering-detonation-1: Grenade hit ➜ Bolstering Detonation equipped ➜ Class ability energy (12% | 17% | 20%)
          Given the build "bolstering-detonation-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And it reads "Grenade 🥊 hit"
          And "Bolstering Detonation" fires "+12% class ability energy"
        """)]
    public async Task BolsteringDetonationsGrenadeHitGivesClassAbilityEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("bolstering-detonation-skip-grenade.build.yaml");
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
            .Fires(new ArmorMod.BolsteringDetonation(), "+12% class ability energy");
    }
}
