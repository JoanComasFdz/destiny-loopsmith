using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Keywords.Super;

/// <summary>Super's rules (rules/keywords/super.yaml), one scenario per rule claim.</summary>
public sealed class SuperTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Casting a Super puts you in it
          # Rule keywords-super-super-1: Super cast ➜ Super
          Given the build "super-storms-edge" is imported, with no steps
          When I tap "Storm's Edge" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Super (🥊 hit)"
          # A Super's first hit casts it: the card reads the cast first.
          And it reads "Super cast"
          And "Super" fires "Super"
          # "1 effect": Storm's Edge's damage resistance holds while you're in it.
          And its BUFFS show only "Super · 1 effect"
        """)]
    public async Task CastingASuperPutsYouInIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("super-storms-edge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.StormsEdge());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A Super's first hit casts it: the card reads the cast first.
        // "1 effect": Storm's Edge's damage resistance holds while you're in it.
        var cast = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(cast)
            .HasHeading("#1 Super (🥊 hit)")
            .Reads("Super cast")
            .Fires(new Keyword.Super(), "Super")
            .Shows(new StateGroup.Buffs(), "Super · 1 effect");
    }
}
