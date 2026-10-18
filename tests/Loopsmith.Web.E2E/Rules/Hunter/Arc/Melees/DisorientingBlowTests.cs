using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Melees;

/// <summary>Disorienting Blow's rules (rules/hunter/arc/melees.yaml), one scenario per rule claim.</summary>
public sealed class DisorientingBlowTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Disorienting Blow's melee hit blinds the target, amplifies you and gives 2 Bolt Charge
          # Rule hunter-arc-melees-disorienting-blow-1: Melee hit ➜ Disorienting Blow equipped ➜ Amplified, Bolt Charge (×2), Blind (8.5s)
          Given the build "disorienting-blow" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And it reads "Melee 🥊 hit"
          And "Disorienting Blow" fires "Blind target (8.5s)", "Amplified (15s)" and "+2 Bolt Charge"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task DisorientingBlowsMeleeHitBlindsTheTargetAmplifiesYouAndGives2BoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("disorienting-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Melee (🥊 hit)")
            .Reads("Melee 🥊 hit")
            .Fires(new Ability.DisorientingBlow(), "Blind target (8.5s)", "Amplified (15s)", "+2 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }
}
