using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Chest;

/// <summary>Sniper Damage Resistance's passive (rules/mods/chest.yaml), one scenario per rule claim.</summary>
public sealed class SniperDamageResistanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Sniper Damage Resistance gives long-range combatant damage resistance
          # Rule mods-chest-sniper-damage-resistance-1: Always ➜ Sniper Damage Resistance equipped ➜ Damage resistance (15% | 25% | 30% vs combatants beyond 30 m)
          Given the build "sniper-damage-resistance-only" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Finisher"
          # 34.5% is every Guardian's own against combatants; one Sniper Damage Resistance's 15% adds to it, 1 − (1 − 34.5%) × (1 − 15%). It does nothing in PvP.
          And its "Combatants beyond 30 m" resistance reads "44.33%" PvE and "0%" PvP
          And "Sniper Damage Resistance" gives "15% · PvP 0%" of it
        """)]
    public async Task SniperDamageResistanceGivesLongRangeCombatantDamageResistance(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("sniper-damage-resistance-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // 34.5% is every Guardian's own against combatants; one Sniper Damage Resistance's 15% adds to it, 1 − (1 − 34.5%) × (1 − 15%). It does nothing in PvP.
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .HasResistance("Combatants beyond 30 m", "44.33%", "0%")
            .HasResistanceFrom(new ArmorMod.SniperDamageResistance(), "15% · PvP 0%");
    }
}
