using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Guardian;

/// <summary>The Guardian's own passive (rules/keywords/guardian.yaml), one scenario per rule claim.</summary>
public sealed class GuardianTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Every Guardian resists combatants' damage
          # Rule keywords-guardian-guardian-1: Always ➜ Damage resistance (34.5% vs combatants)
          Given the build "guardian-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          # Against combatants only: nothing reduces PvP damage.
          And its "All damage" resistance reads "34.5%" PvE and "0%" PvP
          And "Guardian" gives "34.5% · PvP 0%" of it
        """)]
    public async Task EveryGuardianResistsCombatantsDamage(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("guardian-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // Against combatants only: nothing reduces PvP damage.
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .HasResistance("All damage", "34.5%", "0%")
            .HasResistanceFrom(new Keyword.Guardian(), "34.5% · PvP 0%");
    }
}
