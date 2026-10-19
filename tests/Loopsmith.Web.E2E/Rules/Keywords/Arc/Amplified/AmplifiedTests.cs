using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Arc;

/// <summary>Amplified's passive (rules/keywords/arc.yaml), one scenario per rule claim.</summary>
public sealed class AmplifiedTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Amplified makes combatants deal less damage to you
          # Rule keywords-arc-amplified-1: Always ➜ Amplified on you ➜ Damage resistance (15% vs combatants)
          Given the build "amplified-disorienting-blow" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # Against combatants only: "All damage" counts it in PvE, with the Guardian's own 34.5%, and not in PvP.
          And its "All damage" resistance reads "44.33%" PvE and "0%" PvP
          And "Amplified" gives "15% · PvP 0%" of it
        """)]
    public async Task AmplifiedMakesCombatantsDealLessDamageToYou(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("amplified-disorienting-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // Against combatants only: "All damage" counts it in PvE, with the Guardian's own 34.5%, and not in PvP.
        var amplified = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(amplified)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge")
            .HasResistance("All damage", "44.33%", "0%")
            .HasResistanceFrom(new Keyword.Amplified(), "15% · PvP 0%");
    }
}
