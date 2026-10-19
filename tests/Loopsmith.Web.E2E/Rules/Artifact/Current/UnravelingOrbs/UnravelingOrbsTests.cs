using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Unraveling Orbs's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class UnravelingOrbsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unraveling Orbs turns a picked-up Orb of Power into Unraveling Rounds
          # Rule artifact-current-unraveling-orbs-1: Pick up Orb of Power ➜ Unraveling Orbs equipped ➜ Unraveling Rounds (14s)
          Given the build "unraveling-orbs-attrition-orbs" is imported, with no steps
          When I tap "Festival Flight" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Festival Flight (🥊 hit)"
          And its GROUND shows only "Orb of Power"
          # keeping step #1: its Orb of Power is what #2 picks up
          When I tap "Pick up Orb of Power" under "On the ground" in the trigger panel
          Then card #2's heading is "#2 Pick up Orb of Power"
          And it reads "📍 Picked up Orb of Power"
          And "Unraveling Orbs" fires "Unraveling Rounds (14s)"
          And its WEAPONS show only "Unraveling Rounds"
        """)]
    public async Task UnravelingOrbsTurnsAPickedUpOrbOfPowerIntoUnravelingRounds(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("unraveling-orbs-attrition-orbs.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.FestivalFlight());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var dropped = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(dropped)
            .HasHeading("#1 Festival Flight (🥊 hit)")
            .Shows(new StateGroup.Ground(), "Orb of Power");

        // When
        await Frontend.Designer.TriggerPalette.ClickPickupAsync(new Keyword.OrbOfPower());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var pickedUp = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#2 Pick up Orb of Power")
            .Reads("📍 Picked up Orb of Power")
            .Fires(new ArtifactPerk.UnravelingOrbs(), "Unraveling Rounds (14s)")
            .Shows(new StateGroup.Weapons(), "Unraveling Rounds");
    }
}
