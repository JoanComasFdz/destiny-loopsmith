using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Arms;

/// <summary>Heavy Handed's rules (rules/mods/arms.yaml), one scenario per rule claim.</summary>
public sealed class HeavyHandedTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Heavy Handed's melee kill drops an Orb of Power
          # Rule mods-arms-heavy-handed-1: Melee kill ➜ Heavy Handed equipped ➜ Orb of Power
          Given the build "heavy-handed-combination-blow-and-tempest-strike" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill)"
          And it reads "Melee 💀 kill"
          And "Heavy Handed" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task HeavyHandedsMeleeKillDropsAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("heavy-handed-combination-blow-and-tempest-strike.build.yaml");
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
            .Fires(new ArmorMod.HeavyHanded(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Heavy Handed's sliding melee kill drops an Orb of Power
          # Rule mods-arms-heavy-handed-1: Sliding melee kill ➜ Heavy Handed equipped ➜ Orb of Power
          Given the build "heavy-handed-combination-blow-and-tempest-strike" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "💀 kill"
          # Tempest Strike's hit jolts the target first, so the kill reads "Sliding melee 💀 kill on Jolted target".
          Then card #1's heading is "#1 Sliding melee (💀 kill)"
          And "Heavy Handed" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task HeavyHandedsSlidingMeleeKillDropsAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("heavy-handed-combination-blow-and-tempest-strike.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // Tempest Strike's hit jolts the target first, so the kill reads "Sliding melee 💀 kill on Jolted target".
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Sliding melee (💀 kill)")
            .Fires(new ArmorMod.HeavyHanded(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }
}
