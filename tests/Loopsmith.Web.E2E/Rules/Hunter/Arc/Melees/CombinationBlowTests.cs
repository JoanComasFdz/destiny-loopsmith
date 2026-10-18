using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Melees;

/// <summary>
/// Combination Blow's rules (rules/hunter/arc/melees.yaml), one scenario per rule claim. Its passive (the damage bonus
/// while Combination Blow is on you, hunter-arc-melees-combination-blow-2) has none: the designer doesn't show it yet.
/// </summary>
public sealed class CombinationBlowTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Combination Blow's melee kill refills the class ability, heals you and stacks
          # Rule hunter-arc-melees-combination-blow-1: Melee kill ➜ Combination Blow equipped ➜ Class ability energy (100%), Health (?), Combination Blow
          Given the build "combination-blow" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill)"
          And it reads "Melee 💀 kill"
          And "Combination Blow" fires "+1 Combination Blow (20s)", "+100% class ability energy" and "heals you"
          And its BUFFS show only "Combination Blow"
        """)]
    public async Task CombinationBlowsMeleeKillRefillsTheClassAbilityHealsYouAndStacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Melee (💀 kill)")
            .Reads("Melee 💀 kill")
            .Fires(new Ability.CombinationBlow(), "+1 Combination Blow (20s)", "+100% class ability energy", "heals you")
            .Shows(new StateGroup.Buffs(), "Combination Blow");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Combination Blow's sliding melee kill refills the class ability, heals you and stacks
          # Rule hunter-arc-melees-combination-blow-1: Sliding melee kill ➜ Combination Blow equipped ➜ Class ability energy (100%), Health (?), Combination Blow
          # Tempest Strike is in the build because the sliding melee is its secondary use.
          Given the build "combination-blow-tempest-strike" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Sliding melee (💀 kill)"
          And "Combination Blow" fires "+1 Combination Blow (20s)", "+100% class ability energy" and "heals you"
        """)]
    public async Task CombinationBlowsSlidingMeleeKillRefillsTheClassAbilityHealsYouAndStacks(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("combination-blow-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Sliding melee (💀 kill)")
            .Fires(new Ability.CombinationBlow(), "+1 Combination Blow (20s)", "+100% class ability energy", "heals you");
    }
}
