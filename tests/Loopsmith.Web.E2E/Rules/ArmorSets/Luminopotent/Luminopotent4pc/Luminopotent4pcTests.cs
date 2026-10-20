using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.Luminopotent;

/// <summary>Luminopotent's 4-piece bonus, Shock and Clear (rules/armor-sets/luminopotent.yaml), one scenario per rule claim.</summary>
public sealed class Luminopotent4pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Shock and Clear's kill of a jolted target creates an Ionic Trace
          # Rule armor-sets-luminopotent-luminopotent-4pc-1: Any kill ➜ Shock and Clear equipped and Jolt on the target ➜ Ionic Trace
          Given the build "luminopotent-4pc-spark-of-shock" is imported, with no steps
          When I tap "Skip Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Jolt"
          # keeping step #1: its Jolt on the target is what #2's kill needs
          When I tap "Skip Grenade" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Grenade (💀 kill)"
          And "Shock and Clear" fires "Ionic Trace"
          # The trace homes in on you: the same step picks it up.
          And its GROUND shows none
        """)]
    public async Task ShockAndClearsKillOfAJoltedTargetCreatesAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("luminopotent-4pc-spark-of-shock.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var hit = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Jolt");

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.SkipGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // The trace homes in on you: the same step picks it up.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Grenade (💀 kill)")
            .Fires(new ArmorSetBonus.ShockAndClear(), "Ionic Trace")
            .ShowsNone(new StateGroup.Ground());
    }
}
