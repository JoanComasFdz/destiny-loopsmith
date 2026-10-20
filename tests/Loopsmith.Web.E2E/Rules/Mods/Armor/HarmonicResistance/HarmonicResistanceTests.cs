using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Harmonic Resistance's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class HarmonicResistanceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Harmonic Resistance resists combatants of your Super's element
          # Rule mods-armor-harmonic-resistance-1: Always ➜ Damage resistance (15% | 25% | 30% vs combatants of your Super's element)
          Given the build "harmonic-resistance-arc-staff-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          # Against combatants only: nothing reduces PvP damage. It multiplies with the Guardian's 34.5%.
          And its "Arc combatants" resistance reads "44.33%" PvE and "0%" PvP
          And "Harmonic Resistance" gives "15% · PvP 0%" of it
        """)]
    public async Task HarmonicResistanceResistsCombatantsOfYourSupersElement(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("harmonic-resistance-arc-staff-skip-grenade.build.yaml");
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
            .HasResistance("Arc combatants", "44.33%", "0%")
            .HasResistanceFrom(new ArmorMod.HarmonicResistance(), "15% · PvP 0%");
    }
}
