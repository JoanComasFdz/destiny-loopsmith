using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Rules.ArmorSets.OryxsMemory;

/// <summary>Oryx's Memory's 4-piece bonus, Ascendant Escape (rules/armor-sets/oryxs-memory.yaml), one scenario per rule claim.</summary>
public sealed class OryxsMemory4pcTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ascendant Escape's Elite+ kill grants Ascendant Escape
          # Rule armor-sets-oryxs-memory-oryxs-memory-4pc-1: Any kill (elite+) ➜ Ascendant Escape equipped ➜ Ascendant Escape
          Given the build "oryxs-memory-4pc-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I pick "Elite" as the rank
          And I tap "💀 kill"
          Then card #1's heading is "#1 Crisis Inverted (💀 kill · Elite)"
          And "Ascendant Escape" fires "Ascendant Escape"
          And its BUFFS show only "Ascendant Escape"
        """)]
    public async Task AscendantEscapesEliteKillGrantsAscendantEscape(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("oryxs-memory-4pc-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill)
            .HasHeading("#1 Crisis Inverted (💀 kill · Elite)")
            .Fires(new ArmorSetBonus.AscendantEscape(), "Ascendant Escape")
            .Shows(new StateGroup.Buffs(), "Ascendant Escape");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ascendant Escape's critical health makes you invisible
          # Rule armor-sets-oryxs-memory-oryxs-memory-4pc-2: Critical health ➜ Ascendant Escape equipped and Ascendant Escape on you ➜ Invisible
          Given the build "oryxs-memory-4pc-crisis-inverted" is imported, with no steps
          When I tap "Crisis Inverted" in the trigger panel
          And I pick "Elite" as the rank
          And I tap "💀 kill"
          Then card #1's BUFFS show only "Ascendant Escape"
          # keeping step #1: its Ascendant Escape is what #2 needs
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #2's heading is "#2 Critical health"
          # the invisibility uses Ascendant Escape up: "the next time your shields are broken" (Bungie)
          And "Ascendant Escape" fires "consumes Ascendant Escape" and "Invisible (6s)"
          And its BUFFS show only "Critical health" and "Invisible"
        """)]
    public async Task AscendantEscapesCriticalHealthMakesYouInvisible(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("oryxs-memory-4pc-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var kill = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(kill).Shows(new StateGroup.Buffs(), "Ascendant Escape");

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var critical = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#2 Critical health")
            .Fires(new ArmorSetBonus.AscendantEscape(), "consumes Ascendant Escape", "Invisible (6s)")
            .Shows(new StateGroup.Buffs(), "Critical health", "Invisible");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Ascendant Escape's hit at critical health makes you invisible
          # Rule armor-sets-oryxs-memory-oryxs-memory-4pc-3: Hit at critical health ➜ Ascendant Escape equipped and Ascendant Escape on you ➜ Invisible
          Given the build "oryxs-memory-4pc-crisis-inverted" is imported, with no steps
          When I tap "Critical health" under "On you" in the trigger panel
          Then card #1's heading is "#1 Critical health"
          And "Ascendant Escape" fires nothing
          # keeping step #1: Ascendant Escape comes after critical health, so only the hit can use it
          When I tap "Crisis Inverted" in the trigger panel
          And I pick "Elite" as the rank
          And I tap "💀 kill"
          Then card #2's BUFFS show only "Critical health" and "Ascendant Escape"
          When I tap "hit" on "Critical health" under "On you" in the trigger panel
          Then card #3's heading is "#3 Hit at critical health"
          And "Ascendant Escape" fires "consumes Ascendant Escape" and "Invisible (6s)"
          And its BUFFS show only "Critical health" and "Invisible"
        """)]
    public async Task AscendantEscapesHitAtCriticalHealthMakesYouInvisible(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("oryxs-memory-4pc-crisis-inverted.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.TriggerPalette.ClickStateAsync(new Keyword.CriticalHealth());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(1);

        // Then
        var critical = await Frontend.Designer.Loop.Step(1).ReadAsync();
        Asserting.That(critical)
            .HasHeading("#1 Critical health")
            .DoesNotFire(new ArmorSetBonus.AscendantEscape());

        // When
        await Frontend.Designer.TriggerPalette.ClickWeaponAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.WaitUntilRowShownAsync(new Weapon.CrisisInverted());
        await Frontend.Designer.TriggerPalette.SelectRankAsync(new EnemyRank.Elite());
        await Frontend.Designer.TriggerPalette.ClickAsync(new StepTrigger.Kill());
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(2);

        // Then
        var kill = await Frontend.Designer.Loop.Step(2).ReadAsync();
        Asserting.That(kill).Shows(new StateGroup.Buffs(), "Critical health", "Ascendant Escape");

        // When
        await Frontend.Designer.TriggerPalette.ClickDeclarationAsync(new Keyword.CriticalHealth(), "hit");
        await Frontend.Designer.Loop.WaitUntilStepShownAsync(3);

        // Then
        var hit = await Frontend.Designer.Loop.Step(3).ReadAsync();
        Asserting.That(hit)
            .HasHeading("#3 Hit at critical health")
            .Fires(new ArmorSetBonus.AscendantEscape(), "consumes Ascendant Escape", "Invisible (6s)")
            .Shows(new StateGroup.Buffs(), "Critical health", "Invisible");
    }
}
