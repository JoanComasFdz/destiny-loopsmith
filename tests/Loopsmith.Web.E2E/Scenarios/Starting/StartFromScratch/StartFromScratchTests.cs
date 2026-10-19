using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Starting;

/// <summary>Starting a loop from scratch on Home: an Arc Hunter with nothing equipped and no loop steps, so nothing in the trigger panel can be tapped yet.</summary>
public sealed class StartFromScratchTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Start from scratch opens an Arc Hunter with nothing equipped
          Given the app is open on Home
          When I tap "Start from scratch"
          Then the Subclass box shows a Hunter with the Arc subclass
          And the Weapons box has nothing equipped
          And the Armor box has nothing equipped
          And the Artifact box has no artifact set
          And the loop has no steps
        """)]
    public async Task StartFromScratchOpensAnArcHunterWithNothingEquipped(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ClickStartFromScratchAsync();
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var subclass = await Frontend.Designer.Subclass.ReadAsync();
        Asserting.That(subclass).HasClass(new GuardianClass.Hunter()).HasSubclass(new Subclass.Arc());

        var weapons = await Frontend.Designer.Weapons.ReadAsync();
        Asserting.That(weapons).HasNothingEquipped();

        var armor = await Frontend.Designer.Armor.ReadAsync();
        Asserting.That(armor).HasNothingEquipped();

        var artifact = await Frontend.Designer.Artifact.ReadAsync();
        Asserting.That(artifact).IsNotSet();

        var loop = await Frontend.Designer.Loop.ReadAsync();
        Asserting.That(loop).HasNoSteps();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A build started from scratch keeps the trigger panel greyed out
          Given the app is open on Home
          When I tap "Start from scratch"
          Then the trigger panel is greyed out
          And it reads "Equip something first: ⋯ on the Subclass box picks its abilities."
        """)]
    public async Task ABuildStartedFromScratchKeepsTheTriggerPanelGreyedOut(Screen screen)
    {
        // Given
        await Frontend.Home.WaitUntilShownAsync();

        // When
        await Frontend.Home.ClickStartFromScratchAsync();
        await Frontend.Designer.WaitUntilShownAsync();

        // Then
        var panel = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(panel)
            .IsGreyedOut()
            .Reads("Equip something first: ⋯ on the Subclass box picks its abilities.");
    }
}
