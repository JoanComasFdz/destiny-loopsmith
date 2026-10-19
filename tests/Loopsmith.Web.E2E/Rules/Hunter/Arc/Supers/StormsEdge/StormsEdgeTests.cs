using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Supers;

/// <summary>Storm's Edge's passive (rules/hunter/arc/supers.yaml), one scenario per rule claim.</summary>
public sealed class StormsEdgeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Storm's Edge gives damage resistance while its Super is on
          # Rule hunter-arc-supers-storms-edge-1: Always ➜ Storm's Edge equipped and Super on you ➜ Damage resistance (90% [PvP 45%])
          Given the build "storms-edge" is imported, with no steps
          When I tap "Storm's Edge" in the trigger panel
          And I tap "🥊 hit"
          And I tap "▸ DAMAGE RESISTANCE" on card #1
          Then card #1's heading is "#1 Super (🥊 hit)"
          # A super's first hit casts it: Super is on you from then on.
          And its BUFFS show only "Super · 1 effect"
          And its "All damage" resistance reads "93.45%" PvE and "45%" PvP
          And "Storm's Edge" gives "90% · PvP 45%" of it
        """)]
    public async Task StormsEdgeGivesDamageResistanceWhileItsSuperIsOn(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("storms-edge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.StormsEdge());
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
            .HasResistance("All damage", "93.45%", "45%")
            .HasResistanceFrom(new Ability.StormsEdge(), "90% · PvP 45%");
    }
}
