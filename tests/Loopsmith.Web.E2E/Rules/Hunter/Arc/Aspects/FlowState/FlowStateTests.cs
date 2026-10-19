using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Aspects;

/// <summary>Flow State's rules and passive (rules/hunter/arc/aspects.yaml), one scenario per rule claim.</summary>
public sealed class FlowStateTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Flow State amplifies you on a jolted target's kill
          # Rule hunter-arc-aspects-flow-state-1: Any kill ➜ Flow State equipped and Jolt on the target ➜ Amplified
          Given the build "flow-state-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          # One step: the grenade's hit jolts the target (Spark of Shock) before its kill reads it.
          And "Flow State" fires "Amplified (15s)"
          And its BUFFS show only "Amplified"
        """)]
    public async Task FlowStateAmplifiesYouOnAJoltedTargetsKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("flow-state-spark-of-shock.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // One step: the grenade's hit jolts the target (Spark of Shock) before its kill reads it.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Grenade (💀 kill)")
            .Fires(new Aspect.FlowState(), "Amplified (15s)")
            .Shows(new StateGroup.Buffs(), "Amplified");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Flow State gives damage resistance while you dodge Amplified
          # Rule hunter-arc-aspects-flow-state-2: Always ➜ Flow State equipped, Amplified on you and Class ability under way ➜ Damage resistance (66% [PvP 32%])
          Given the build "flow-state-spark-of-shock-marksmans-dodge" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          And its BUFFS show only "Amplified"
          # keeping step #1: its Amplified is what #2 dodges with
          When I tap "Marksman's Dodge" in the trigger panel
          And I tap "▸ DAMAGE RESISTANCE" on card #2
          Then card #2's heading is "#2 Class ability"
          And its "All damage" resistance reads "81.07%" PvE and "32%" PvP
          And "Flow State" gives "66% · PvP 32%" of it
        """)]
    public async Task FlowStateGivesDamageResistanceWhileYouDodgeAmplified(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("flow-state-spark-of-shock-marksmans-dodge.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var amplified = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(amplified)
            .HasHeading("#1 Grenade (💀 kill)")
            .Shows(new StateGroup.Buffs(), "Amplified");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.MarksmansDodge());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);
        await Frontend.Designer.Loop.Step(2).ClickDamageResistanceAsync();
        await Frontend.Designer.Loop.Step(2).WaitUntilResistanceSourcesShownAsync();

        // Then
        var dodged = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(dodged)
            .HasHeading("#2 Class ability")
            .HasResistance("All damage", "81.07%", "32%")
            .HasResistanceFrom(new Aspect.FlowState(), "66% · PvP 32%");
    }
}
