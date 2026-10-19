using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.AllClasses.Arc.Fragments;

/// <summary>Spark of Brilliance's rules (rules/all-classes/arc/fragments.yaml), one scenario per rule claim.</summary>
public sealed class SparkOfBrillianceTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Spark of Brilliance's kill on a blinded target may blind the enemies around it
          # Rule all-classes-arc-fragments-spark-of-brilliance-1: Any kill ➜ Spark of Brilliance equipped and Blind on the target ➜ Blind
          Given the build "spark-of-brilliance-flashbang-grenade-crisis-inverted" is imported, with no steps
          When I tap "Flashbang Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Blind"
          # keeping step #1: its Blind on the target is what #2 kills
          When I tap "Crisis Inverted" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 kill on Blinded target"
          # A chance: only a precision kill sets it off, and Loopsmith doesn't tell precision kills from body kills.
          And "Spark of Brilliance" fires "Blind target (10s) (chance)"
          And its DEBUFFS show only "Blind"
        """)]
    public async Task SparkOfBrilliancesKillOnABlindedTargetMayBlindTheEnemiesAroundIt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("spark-of-brilliance-flashbang-grenade-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.FlashbangGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.FlashbangGrenade());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Hit());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var blinded = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(blinded)
            .HasHeading("#1 Grenade (🥊 hit)")
            .Shows(new StateGroup.Debuffs(), "Blind");

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // A chance: only a precision kill sets it off, and Loopsmith doesn't tell precision kills from body kills.
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 kill on Blinded target")
            .Fires(new Fragment.SparkOfBrilliance(), "Blind target (10s) (chance)")
            .Shows(new StateGroup.Debuffs(), "Blind");
    }
}
