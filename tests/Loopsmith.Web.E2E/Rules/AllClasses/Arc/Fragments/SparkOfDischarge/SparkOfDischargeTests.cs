using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>
/// Spark of Discharge's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim. The designer picks up an
/// Ionic Trace in the step that drops it (a trace homes in on you), so the pick-up claim is played with the same kills as
/// the drop, and each scenario checks its own event of that card.
/// </summary>
public sealed class SparkOfDischargeTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Discharge's three Arc weapon kills at once drop an Ionic Trace
          # Rule all-classes-arc-fragments-spark-of-discharge-2: Arc weapon 3+ kills ➜ Spark of Discharge equipped ➜ Ionic Trace
          Given the build "spark-of-discharge-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "3 · Spark of Discharge"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 3)"
          And it reads "Crisis Inverted 💀 killed 3 enemies"
          And on "Crisis Inverted 💀 killed 3 enemies" "Spark of Discharge" fires "Ionic Trace"
        """)]
    public async Task SparkOfDischargesThreeArcWeaponKillsAtOnceDropAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-discharge-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Spark of Discharge");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill 3)")
            .Reads("Crisis Inverted 💀 killed 3 enemies")
            .FiresOn("Crisis Inverted 💀 killed 3 enemies", new Fragment.SparkOfDischarge(), "Ionic Trace");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Discharge's single Arc weapon kill may drop an Ionic Trace
          # Rule all-classes-arc-fragments-spark-of-discharge-3: Arc weapon fewer than 3 kills ➜ Spark of Discharge equipped ➜ Ionic Trace
          Given the build "spark-of-discharge-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "1 enemy"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 killed 1 enemy"
          # A chance: the kills of earlier steps may have filled the counter.
          And on "Crisis Inverted 💀 killed 1 enemy" "Spark of Discharge" fires "Ionic Trace (chance)"
        """)]
    public async Task SparkOfDischargesSingleArcWeaponKillMayDropAnIonicTrace(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-discharge-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("1 enemy");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // A chance: the kills of earlier steps may have filled the counter.
        var killed = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 killed 1 enemy")
            .FiresOn("Crisis Inverted 💀 killed 1 enemy", new Fragment.SparkOfDischarge(), "Ionic Trace (chance)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Discharge's Ionic Trace gives a Bolt Charge when you pick it up
          # Rule all-classes-arc-fragments-spark-of-discharge-1: Pick up Ionic Trace ➜ Spark of Discharge equipped ➜ Bolt Charge
          Given the build "spark-of-discharge-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I select "3 · Spark of Discharge"
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill 3)"
          # The trace the kills drop homes in on you: the same step picks it up.
          And on "📍 Picked up Ionic Trace" "Spark of Discharge" fires "+1 Bolt Charge"
          And its BUFFS show only "Bolt Charge"
          And its GROUND shows none
        """)]
    public async Task SparkOfDischargesIonicTraceGivesABoltChargeWhenYouPickItUp(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-discharge-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectTargetCountAsync("3 · Spark of Discharge");
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // The trace the kills drop homes in on you: the same step picks it up.
        var pickedUp = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(pickedUp)
            .HasHeading("#1 Crisis Inverted (💀 kill 3)")
            .FiresOn("📍 Picked up Ionic Trace", new Fragment.SparkOfDischarge(), "+1 Bolt Charge")
            .Shows(new StateGroup.Buffs(), "Bolt Charge")
            .ShowsNone(new StateGroup.Ground());
    }
}
