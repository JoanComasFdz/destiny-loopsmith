using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Artifact.Current;

/// <summary>Dielectric's rules (rules/artifact/current.yaml), one scenario per rule claim.</summary>
public sealed class DielectricTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Dielectric's kill of a jolted target gives Bolt Charge
          # Rule artifact-current-dielectric-1: Any kill ➜ Dielectric equipped and Jolt on the target ➜ Bolt Charge
          Given the build "dielectric-skip-grenade-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2 kills
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Grenade (💀 kill)"
          And "Dielectric" fires "+1 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task DielectricsKillOfAJoltedTargetGivesBoltCharge(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("dielectric-skip-grenade-spark-of-shock.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var jolted = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(jolted)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Grenade (💀 kill)")
            .Fires(new ArtifactPerk.Dielectric(), "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }
}
