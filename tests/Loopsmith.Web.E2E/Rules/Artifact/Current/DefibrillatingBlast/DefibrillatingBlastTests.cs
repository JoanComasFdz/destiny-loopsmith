using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Defibrillating Blast's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class DefibrillatingBlastTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Defibrillating Blast's Bolt Charge strike jolts and heals
          # Rule artifact-current-defibrillating-blast-1: Bolt Charge strike hit ➜ Defibrillating Blast equipped ➜ Health (?), Jolt
          Given the build "defibrillating-blast-disorienting-blow-hits" is imported, with no steps
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Melee (🥊 hit)"
          And its BUFFS show only "Amplified" and "Bolt Charge"
          # keeping step #1: its Bolt Charge is what #2 declares at max
          When I tap "at max (x10)" on "Bolt Charge" under "On you" in the trigger panel
          Then card #2's heading is "#2 Bolt Charge at max (x10)"
          And its BUFFS show only "Amplified" and "Bolt Charge (at max)"
          # keeping step #2: its Bolt Charge at max is what #3's hit discharges as a strike
          When I tap "Disorienting Blow" in the trigger panel
          And I tap "🥊 hit"
          Then card #3's heading is "#3 Melee (🥊 hit)"
          # The heal's amount is unknown ("?" in the rules), so its line names no number.
          And "Defibrillating Blast" fires "Jolt target (10s)" and "heals you"
          And its DEBUFFS show only "Blind" and "Jolt"
        """)]
    public async Task DefibrillatingBlastsBoltChargeStrikeJoltsAndHeals(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("defibrillating-blast-disorienting-blow-hits.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var charged = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(charged)
            .HasHeading("#1 Melee (🥊 hit)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.BoltCharge(), "at max (x10)");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var maxed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(maxed)
            .HasHeading("#2 Bolt Charge at max (x10)")
            .Shows(new StateGroup.Buffs(), "Amplified", "Bolt Charge (at max)");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.DisorientingBlow());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        // The heal's amount is unknown ("?" in the rules), so its line names no number.
        var struck = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(struck)
            .HasHeading("#3 Melee (🥊 hit)")
            .Fires(new ArtifactPerk.DefibrillatingBlast(), "Jolt target (10s)", "heals you")
            .Shows(new StateGroup.Debuffs(), "Blind", "Jolt");
    }
}
