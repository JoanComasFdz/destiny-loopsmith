using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>The rank of the enemy a kill kills (ADRs D44): the "Rank" picker on a kill row, and on the finisher when a rule reacts to it.</summary>
public sealed class EnemyRankTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The rank picker is enabled on a weapon and the grenade when a rule reacts to an enemy's rank
          Given the build "elite-oryxs-memory" is imported, with no steps
          When I tap "Flashbang Grenade" in the trigger panel
          Then its row's "Rank" picker is enabled, at "Minor"
          When I tap "Crisis Inverted" in the trigger panel
          Then its row's "Rank" picker is enabled, at "Minor"
          And none of "🥊 hit", "💀 kill", the "×" enemy count, the "Rank" picker and "🔄 reload" overlap
          And "🔄 reload" sits at the row's right end
          And the row fits inside the screen, with no sideways scroll
          When I pick "Elite" as the rank
          And I tap "💀 kill"
          And I tap "💀 kill"
          # the rank stays picked for the next steps
          Then card #2's heading is "#2 Crisis Inverted (💀 kill · Elite)"
        """)]
    public async Task TheRankPickerIsEnabledOnAWeaponAndTheGrenadeWhenARuleReactsToAnEnemysRank(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elite-oryxs-memory.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.FlashbangGrenade());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.FlashbangGrenade());

        // Then
        var grenade = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(grenade).HasTheRankPickerEnabledAt(new EnemyRank.Minor());

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());

        // Then
        var weapon = await Frontend.Designer.TriggerPalette.ReadAsync();
        var layout = await Frontend.Designer.TriggerPalette.ReadLayoutAsync();
        Asserting.That(weapon).HasTheRankPickerEnabledAt(new EnemyRank.Minor());
        Asserting.That(layout)
            .HasNoOverlap()
            .HasAtRightEnd(new StepTrigger.Reload())
            .FitsInsideTheScreen();

        // When
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        // the rank stays picked for the next steps
        var second = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(second).HasHeading("#2 Crisis Inverted (💀 kill · Elite)");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The rank picker is greyed out when no rule reacts to an enemy's rank
          Given the build "elite-crisis-inverted-alone" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          Then its row's "Rank" picker is greyed out, at "Minor"
        """)]
    public async Task TheRankPickerIsGreyedOutWhenNoRuleReactsToAnEnemysRank(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elite-crisis-inverted-alone.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());

        // Then
        var row = await Frontend.Designer.TriggerPalette.ReadAsync();
        Asserting.That(row).HasTheRankPickerGreyedOutAt(new EnemyRank.Minor());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A kill of a minor enemy grants no Ascendant Escape
          Given the build "elite-oryxs-memory" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill)"
          # only an Elite+ kill sets it off
          And it reads "nothing triggers"
          And "Ascendant Escape" fires nothing
        """)]
    public async Task AKillOfAMinorEnemyGrantsNoAscendantEscape(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elite-oryxs-memory.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        // only an Elite+ kill sets it off
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Crisis Inverted (💀 kill)")
            .ReadsNothingTriggers()
            .DoesNotFire(new ArmorSetBonus.AscendantEscape());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A boss kill sets off an Elite+ rule
          Given the build "elite-oryxs-memory" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I pick "Boss" as the rank
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill · Boss)"
          And "Ascendant Escape" fires "Ascendant Escape"
        """)]
    public async Task ABossKillSetsOffAnEliteRule(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elite-oryxs-memory.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Boss());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Crisis Inverted (💀 kill · Boss)")
            .Fires(new ArmorSetBonus.AscendantEscape(), "Ascendant Escape");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: A finisher on an elite sets off an Elite+ rule
          Given the build "elite-oryxs-memory" is imported, with no steps
          When I tap "Finisher" in the trigger panel
          And I pick "Elite" as the rank
          And I tap "Finisher (Elite)"
          Then card #1's heading is "#1 Finisher (Elite)"
          And "Ascendant Escape" fires "Ascendant Escape"
        """)]
    public async Task AFinisherOnAnEliteSetsOffAnEliteRule(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("elite-oryxs-memory.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickAbilityAsync(new Ability.Finisher());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Ability.Finisher());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.FinisherOf(new EnemyRank.Elite()));
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var finisher = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(finisher)
            .HasHeading("#1 Finisher (Elite)")
            .Fires(new ArmorSetBonus.AscendantEscape(), "Ascendant Escape");
    }
}
