using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop.Weapons;

/// <summary>A weapon step's precision kills (ADRs D36): the "🎯 precision" box, and what needs one.</summary>
public sealed class PrecisionTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The precision box shows on a weapon when a rule needs a precision kill
          Given the build "precision-spark-of-brilliance" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          Then its row shows "🎯 precision"
          And none of "🥊 hit", "💀 kill", the "×" enemy count, "🎯 precision" and "🔄 reload" overlap
          And "🔄 reload" sits at the row's right end
          And the row fits inside the screen, with no sideways scroll
          When I tick "🎯 precision"
          And I tap "💀 kill"
          And I tap "💀 kill"
          # the box stays ticked for the next steps
          Then card #2's heading is "#2 Crisis Inverted (💀 kill · 🎯 precision)"
        """)]
    public async Task ThePrecisionBoxShowsOnAWeaponWhenARuleNeedsAPrecisionKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("precision-spark-of-brilliance.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        var layout = await Frontend.Designer.TriggerPalette.ReadLayoutAsync();
        Asserting.That(row).ShowsTheOption(new RowOption.Precision());
        Asserting.That(layout)
            .HasNoOverlap()
            .HasAtRightEnd(new StepTrigger.Reload())
            .FitsInsideTheScreen();

        // When
        await Frontend.Designer.TriggerPalette.TickOptionAsync(new RowOption.Precision());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // the box stays ticked for the next steps
        var second = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(second).HasHeading("#2 Crisis Inverted (💀 kill · 🎯 precision)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: No precision box on a weapon when no rule needs a precision kill
          Given the build "precision-crisis-inverted-alone" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          Then its row shows no "🎯 precision"
        """)]
    public async Task NoPrecisionBoxOnAWeaponWhenNoRuleNeedsAPrecisionKill(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("precision-crisis-inverted-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).ShowsNoOption(new RowOption.Precision());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A body kill on a blinded target sets off no Spark of Brilliance
          Given the build "precision-spark-of-brilliance" is imported, with no steps
          When I tap "Flashbang Grenade" in the trigger panel
          And I tap "🥊 hit"
          Then card #1's heading is "#1 Grenade (🥊 hit)"
          And its DEBUFFS show only "Blind"
          # keeping step #1: its Blind on the target is what #2 kills
          When I tap "Crisis Inverted" in the trigger panel
          And I tap "💀 kill"
          Then card #2's heading is "#2 Crisis Inverted (💀 kill)"
          And it reads "Crisis Inverted 💀 kill on Blinded target"
          # only a precision kill sets it off
          And "Spark of Brilliance" fires nothing
        """)]
    public async Task ABodyKillOnABlindedTargetSetsOffNoSparkOfBrilliance(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("precision-spark-of-brilliance.build.yaml");
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
        // only a precision kill sets it off
        var killed = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(killed)
            .HasHeading("#2 Crisis Inverted (💀 kill)")
            .Reads("Crisis Inverted 💀 kill on Blinded target")
            .DoesNotFire(new Fragment.SparkOfBrilliance());
    }
}
