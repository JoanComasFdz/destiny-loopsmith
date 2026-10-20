using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Chest;

/// <summary>Arc Resistance's passive (rules/mods/chest.yaml), one scenario per rule claim.</summary>
public sealed class ArcResistanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Arc Resistance gives Arc combatant damage resistance
          # Rule mods-chest-arc-resistance-1: Always ➜ Arc Resistance equipped ➜ Damage resistance (15% | 25% | 30% vs Arc combatants)
          Given the build "arc-resistance-only" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Finisher"
          # 34.5% is every Guardian's own against combatants; one Arc Resistance's 15% adds to it, 1 − (1 − 34.5%) × (1 − 15%). It does nothing in PvP.
          And its "Arc combatants" resistance reads "44.33%" PvE and "0%" PvP
          And "Arc Resistance" gives "15% · PvP 0%" of it
        """)]
    public async Task ArcResistanceGivesArcCombatantDamageResistance(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("arc-resistance-only.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // 34.5% is every Guardian's own against combatants; one Arc Resistance's 15% adds to it, 1 − (1 − 34.5%) × (1 − 15%). It does nothing in PvP.
        var finished = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finished)
            .HasHeading("#1 Finisher")
            .HasResistance("Arc combatants", "44.33%", "0%")
            .HasResistanceFrom(new ArmorMod.ArcResistance(), "15% · PvP 0%");
    }
}
