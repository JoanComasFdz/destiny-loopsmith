using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Weapons.Perks;

/// <summary>Attrition Orbs' rules (rules/weapons/perks.yaml), one scenario per rule claim.</summary>
public sealed class AttritionOrbsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Attrition Orbs' kept-up hits drop an Orb of Power
          # Rule weapons-perks-attrition-orbs-1: This weapon sustained hit ➜ Attrition Orbs equipped ➜ Orb of Power
          Given the build "attrition-orbs-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit"
          And "Attrition Orbs" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task AttritionOrbsKeptUpHitsDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("attrition-orbs-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit")
            .Fires(new WeaponPerk.AttritionOrbs(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Enhanced Attrition Orbs' kept-up hits drop an Orb of Power
          # Rule weapons-perks-attrition-orbs-1: This weapon sustained hit ➜ Attrition Orbs (enhanced) equipped ➜ Orb of Power
          Given the build "attrition-orbs-enhanced-festival-flight" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "1 enemy"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And it reads "Festival Flight 🥊 hit"
          And "Attrition Orbs (enhanced)" fires "Orb of Power"
          And its GROUND shows only "Orb of Power"
        """)]
    public async Task EnhancedAttritionOrbsKeptUpHitsDropAnOrbOfPower(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("attrition-orbs-enhanced-festival-flight.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Reads("Festival Flight 🥊 hit")
            .Fires(new WeaponPerk.AttritionOrbsEnhanced(), "Orb of Power")
            .Shows(new StateGroup.Ground(), "Orb of Power");
    }
}
