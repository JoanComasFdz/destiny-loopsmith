using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Mods.Armor;

/// <summary>Impact Induction's rules (rules/mods/armor.yaml), one scenario per rule claim.</summary>
public sealed class ImpactInductionTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Impact Induction's melee hit gives grenade energy
          # Rule mods-armor-impact-induction-1: Melee hit ➜ Impact Induction equipped ➜ Grenade energy
          Given the build "impact-induction-combination-blow-hits" is imported, with no steps
          When I tap "Combination Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And "Impact Induction" fires "+12% grenade energy"
        """)]
    public async Task ImpactInductionsMeleeHitGivesGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("impact-induction-combination-blow-hits.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.CombinationBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var punched = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(punched)
            .HasHeading("#1 Melee (🥊 hit)")
            .Fires(new ArmorMod.ImpactInduction(), "+12% grenade energy");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Impact Induction's sliding melee hit gives grenade energy
          # Rule mods-armor-impact-induction-1: Sliding melee hit ➜ Impact Induction equipped ➜ Grenade energy
          Given the build "impact-induction-tempest-strike-combination-blow-slides" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Sliding melee (🥊 hit)"
          And "Impact Induction" fires "+12% grenade energy"
        """)]
    public async Task ImpactInductionsSlidingMeleeHitGivesGrenadeEnergy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("impact-induction-tempest-strike-combination-blow-slides.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Sliding melee (🥊 hit)")
            .Fires(new ArmorMod.ImpactInduction(), "+12% grenade energy");
    }
}
