using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Focusing Strike's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class FocusingStrikeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Focusing Strike's melee hit gives class ability energy
          # Rule mods-arms-focusing-strike-1: Melee hit ➜ Focusing Strike equipped ➜ Class ability energy (12% | 17% | 20%)
          Given the build "focusing-strike-combination-blow-and-tempest-strike" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And it reads "Melee 🥊 hit"
          And "Focusing Strike" fires "+12% class ability energy"
        """)]
    public async Task FocusingStrikesMeleeHitGivesClassAbilityEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("focusing-strike-combination-blow-and-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Melee (🥊 hit)")
            .Reads("Melee 🥊 hit")
            .Fires(new ArmorMod.FocusingStrike(), "+12% class ability energy");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Focusing Strike's sliding melee hit gives class ability energy
          # Rule mods-arms-focusing-strike-1: Sliding melee hit ➜ Focusing Strike equipped ➜ Class ability energy (12% | 17% | 20%)
          Given the build "focusing-strike-combination-blow-and-tempest-strike" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Sliding melee (🥊 hit)"
          And it reads "Sliding melee 🥊 hit"
          And "Focusing Strike" fires "+12% class ability energy"
        """)]
    public async Task FocusingStrikesSlidingMeleeHitGivesClassAbilityEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("focusing-strike-combination-blow-and-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Sliding melee (🥊 hit)")
            .Reads("Sliding melee 🥊 hit")
            .Fires(new ArmorMod.FocusingStrike(), "+12% class ability energy");
    }
}
