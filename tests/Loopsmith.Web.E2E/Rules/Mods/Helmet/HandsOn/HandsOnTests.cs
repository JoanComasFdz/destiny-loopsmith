using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Helmet;

/// <summary>Hands-On's rules (rules/mods/helmet.yaml), one scenario per rule claim.</summary>
public sealed class HandsOnTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hands-On's melee kill gives Super energy
          # Rule mods-helmet-hands-on-1: Melee kill ➜ Hands-On equipped ➜ Super energy (?)
          Given the build "hands-on-combination-blow" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Melee (💀 kill)"
          And it reads "Melee 💀 kill"
          # The amount per enemy is unknown, so "?".
          And "Hands-On" fires "+?% super energy"
        """)]
    public async Task HandsOnsMeleeKillGivesSuperEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hands-on-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The amount per enemy is unknown, so "?".
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Melee (💀 kill)")
            .Reads("Melee 💀 kill")
            .Fires(new ArmorMod.HandsOn(), "+?% super energy");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Hands-On's sliding melee kill gives Super energy
          # Rule mods-helmet-hands-on-1: Sliding melee kill ➜ Hands-On equipped ➜ Super energy (?)
          Given the build "hands-on-combination-blow" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Sliding melee (💀 kill)"
          And it reads "Sliding melee 💀 kill"
          # The amount per enemy is unknown, so "?".
          And "Hands-On" fires "+?% super energy"
        """)]
    public async Task HandsOnsSlidingMeleeKillGivesSuperEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("hands-on-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The amount per enemy is unknown, so "?".
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Sliding melee (💀 kill)")
            .Reads("Sliding melee 💀 kill")
            .Fires(new ArmorMod.HandsOn(), "+?% super energy");
    }
}
