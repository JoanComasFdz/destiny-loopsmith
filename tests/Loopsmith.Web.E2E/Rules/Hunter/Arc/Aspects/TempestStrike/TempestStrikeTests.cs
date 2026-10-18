using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.Hunter.Arc.Aspects;

/// <summary>Tempest Strike's rules (rules/hunter/arc/aspects.yaml), one scenario per rule claim.</summary>
public sealed class TempestStrikeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tempest Strike's sliding melee hit jolts the target
          # Rule hunter-arc-aspects-tempest-strike-2: Sliding melee hit ➜ Tempest Strike equipped ➜ Jolt
          Given the build "tempest-strike-combination-blow" is imported, with no steps
          When I tap "SLIDE" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Sliding melee (🥊 hit)"
          And it reads "Sliding melee 🥊 hit"
          And "Tempest Strike" fires "Jolt target (10s)"
          And its DEBUFFS show only "Jolt"
        """)]
    public async Task TempestStrikesSlidingMeleeHitJoltsTheTarget(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("tempest-strike-combination-blow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickSecondaryAsync(new SecondaryUse.Slide());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync("Sliding melee · Tempest Strike");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var slid = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(slid)
            .HasHeading("#1 Sliding melee (🥊 hit)")
            .Reads("Sliding melee 🥊 hit")
            .Fires(new Aspect.TempestStrike(), "Jolt target (10s)")
            .Shows(new StateGroup.Debuffs(), "Jolt");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tempest Strike gives Bolt Charge on a jolted target's kill
          # Rule hunter-arc-aspects-tempest-strike-1: Any kill ➜ Tempest Strike equipped and Jolt on the target ➜ Bolt Charge
          Given the build "tempest-strike-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Grenade (💀 kill)"
          # One step: the grenade's hit jolts the target (Spark of Shock) before its kill reads it.
          And "Tempest Strike" fires "+1 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
        """)]
    public async Task TempestStrikeGivesBoltChargeOnAJoltedTargetsKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("tempest-strike-spark-of-shock.build.yaml");
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
            .Fires(new Aspect.TempestStrike(), "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge");
    }
}
