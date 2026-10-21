using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>A status's details opened from its name in a step's card: the perk the build equips, and what else interacts with it.</summary>
public sealed class KeywordDetailsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stats for All named in a step opens the enhanced trait that gave it
          Given the build "ogma-pr6-stats-for-all-enhanced" is imported, with no steps
          When I tap "Ogma PR6" in the trigger panel
          And I select "3 in 3 s · Stats for All (enhanced)"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Ogma PR6 (🥊 hit 3)"
          And "Stats for All (enhanced)" fires "Stats for All (11s)"
          When I tap "Stats for All" in card #1's "Stats for All (11s)"
          Then the item details popup opens, titled "Stats for All (enhanced)"
          And it has no "In this build" section
        """)]
    public async Task StatsForAllNamedInAStepOpensTheEnhancedTraitThatGaveIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("ogma-pr6-stats-for-all-enhanced.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.OgmaPr6());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All (enhanced)");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Ogma PR6 (🥊 hit 3)")
            .Fires(new WeaponPerk.StatsForAllEnhanced(), "Stats for All (11s)");

        // When
        await Frontend.Designer.Loop.Step(1).ClickKeywordAsync(new Keyword.StatsForAll(), "Stats for All (11s)");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.StatsForAllEnhanced())
            .HasNoInBuild();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Stats for All's details leave its enhanced trait on another weapon out of "In this build"
          Given the build "festival-flight-stats-for-all-ogma-pr6-enhanced" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I select "3 in 3 s · Stats for All"
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit 3)"
          And "Stats for All" fires "Stats for All (10s)"
          When I tap "Stats for All" in card #1's "Stats for All (10s)"
          Then the item details popup opens, titled "Stats for All"
          And it has no "In this build" section
        """)]
    public async Task StatsForAllsDetailsLeaveItsEnhancedTraitOnAnotherWeaponOutOfInThisBuild(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("festival-flight-stats-for-all-ogma-pr6-enhanced.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 in 3 s · Stats for All");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Festival Flight (🥊 hit 3)")
            .Fires(new WeaponPerk.StatsForAll(), "Stats for All (10s)");

        // When
        await Frontend.Designer.Loop.Step(1).ClickKeywordAsync(new Keyword.StatsForAll(), "Stats for All (10s)");
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new WeaponPerk.StatsForAll())
            .HasNoInBuild();
    }
}
