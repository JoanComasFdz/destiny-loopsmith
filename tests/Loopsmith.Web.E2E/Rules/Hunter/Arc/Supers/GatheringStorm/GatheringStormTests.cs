using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Supers;

/// <summary>Gathering Storm's rules and passive (rules/hunter/arc/supers.yaml), one scenario per rule claim.</summary>
public sealed class GatheringStormTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Gathering Storm's hit jolts the target
          # Rule hunter-arc-supers-gathering-storm-1: Super hit ➜ Gathering Storm equipped ➜ Jolt
          Given the build "gathering-storm" is imported, with no steps
          When I tap "Gathering Storm" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Super (🥊 hit)"
          # A super's first hit casts it: "Super cast" comes before the hit.
          And it reads "Super cast"
          And "Gathering Storm" fires "Jolt target (10s)"
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task GatheringStormsHitJoltsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("gathering-storm.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A super's first hit casts it: "Super cast" comes before the hit.
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Super (🥊 hit)")
            .Reads("Super cast")
            .Fires(new Ability.GatheringStorm(), "Jolt target (10s)")
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Gathering Storm gives damage resistance while its Super is on
          # Rule hunter-arc-supers-gathering-storm-2: Always ➜ Gathering Storm equipped and Super on you ➜ Damage resistance (90% [PvP 49%])
          Given the build "gathering-storm" is imported, with no steps
          When I tap "Gathering Storm" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Super (🥊 hit)"
          # A super's first hit casts it: Super is on you from then on.
          And its BUFFS show only "Super · 1 effect"
          And its "All damage" resistance reads "93.5%" PvE and "49%" PvP
          And "Gathering Storm" gives "90% · PvP 49%" of it
        """)]
    public async Task GatheringStormGivesDamageResistanceWhileItsSuperIsOn(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("gathering-storm.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.GatheringStorm());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.Loop.Step(1).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(1).WaitUntilResistanceSourcesShownAsync();

        // Then
        // A super's first hit casts it: Super is on you from then on.
        var cast = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(cast)
            .HasHeading("#1 Super (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect")
            .HasResistance("All damage", "93.5%", "49%")
            .HasResistanceFrom(new Ability.GatheringStorm(), "90% · PvP 49%");
    }
}
