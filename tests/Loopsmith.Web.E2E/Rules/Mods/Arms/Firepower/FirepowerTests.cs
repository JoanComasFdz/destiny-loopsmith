using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Firepower's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class FirepowerTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Firepower's grenade kill drops an Orb of Power
          # Rule mods-arms-firepower-1: Grenade kill ➜ Firepower equipped ➜ Orb of Power
          Given the build "firepower-skip-grenade" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          And it reads "Grenade 💀 kill"
          And "Firepower" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task FirepowersGrenadeKillDropsAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("firepower-skip-grenade.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Grenade (💀 kill)")
            .Reads("Grenade 💀 kill")
            .Fires(new ArmorMod.Firepower(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }
}
