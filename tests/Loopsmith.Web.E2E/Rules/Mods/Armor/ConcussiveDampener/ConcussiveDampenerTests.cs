using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Concussive Dampener's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class ConcussiveDampenerTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Concussive Dampener resists combatants' area, blast and explosive damage
          # Rule mods-armor-concussive-dampener-1: Always ➜ Damage resistance (15% | 25% | 30% vs combatants' area, blast and explosive damage)
          Given the build "concussive-dampener-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          # Against combatants only: nothing reduces PvP damage. It multiplies with the Guardian's 34.5%.
          And its "Combatants' area, blast and explosive damage" resistance reads "44.33%" PvE and "0%" PvP
          And "Concussive Dampener" gives "15% · PvP 0%" of it
        """)]
    public async Task ConcussiveDampenerResistsCombatantsAreaBlastAndExplosiveDamage(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("concussive-dampener-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // Against combatants only: nothing reduces PvP damage. It multiplies with the Guardian's 34.5%.
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .HasResistance("Combatants' area, blast and explosive damage", "44.33%", "0%")
            .HasResistanceFrom(new ArmorMod.ConcussiveDampener(), "15% · PvP 0%");
    }
}
