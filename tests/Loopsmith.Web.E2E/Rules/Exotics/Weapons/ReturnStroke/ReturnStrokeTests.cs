using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Exotics.Weapons;

/// <summary>Return Stroke's claims (rules/exotics/weapons.yaml): each lightning strike refills Thunderlord, no reload (ADRs D42).</summary>
public sealed class ReturnStrokeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Return Stroke's kill strike refills Thunderlord
          # Rule exotics-weapons-thunderlord-catalyst-1: Power weapon kill ➜ Return Stroke equipped ➜ Refill
          Given the build "return-stroke-thunderlord" is imported, with no steps
          When I tap "Thunderlord" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Thunderlord (💀 kill)"
          And "Return Stroke" fires "refills your heavy weapon"
        """)]
    public async Task ReturnStrokesKillStrikeRefillsThunderlord(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("return-stroke-thunderlord.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Thunderlord (💀 kill)")
            .Fires(new Catalyst.ReturnStroke(), "refills your heavy weapon");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Return Stroke's 10th-hit strike refills Thunderlord
          # Rule exotics-weapons-thunderlord-catalyst-2: Power weapon sustained hit ➜ Return Stroke equipped ➜ Refill
          Given the build "return-stroke-thunderlord" is imported, with no steps
          When I tap "Thunderlord" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Thunderlord (🥊 hit)"
          And "Return Stroke" fires "refills your heavy weapon"
        """)]
    public async Task ReturnStrokes10thHitStrikeRefillsThunderlord(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("return-stroke-thunderlord.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.Thunderlord());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Thunderlord (🥊 hit)")
            .Fires(new Catalyst.ReturnStroke(), "refills your heavy weapon");
    }
}
