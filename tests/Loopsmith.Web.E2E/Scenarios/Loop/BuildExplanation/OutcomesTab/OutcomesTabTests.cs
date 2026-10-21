using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>"What this build does" by trigger (its Triggers tab) and by outcome (its Outcomes tab): every way to get each outcome.</summary>
public sealed class OutcomesTabTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: "What this build does" opens on its Triggers tab, the tree by trigger
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          Then the panel shows the tabs "Triggers" and "Outcomes", "Triggers" selected
          And its "Class ability in the air" heading is followed by the row "Jolt target (10s) and Amplified (15s) [Ascension]"
        """)]
    public async Task WhatThisBuildDoesOpensOnItsTriggersTabTheTreeByTrigger(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .HasTabs("Triggers", "Outcomes")
            .HasSelectedTab("Triggers")
            .Shows("Class ability in the air", "Jolt target (10s) and Amplified (15s) [Ascension]");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The Outcomes tab lists every way to get Jolt under "Jolt"
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          Then the "Outcomes" tab is selected
          And under "Jolt · Arc debuff", "Is given by" lists only "Class ability in the air → Jolt target (10s) [Ascension]" and "Melee damage → Jolt target (10s) (while Lethal Current) [Lethal Current]"
        """)]
    public async Task TheOutcomesTabListsEveryWayToGetJoltUnderJolt(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .HasSelectedTab("Outcomes")
            .ListsOnlyUnder("Jolt · Arc debuff", "Is given by",
                "Class ability in the air → Jolt target (10s) [Ascension]",
                "Melee damage → Jolt target (10s) (while Lethal Current) [Lethal Current]");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The Outcomes tab lists what ends a buff under "Is ended or used up by"
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          Then under "Lethal Current · Arc buff", "Is given by" lists only "Class ability → Lethal Current (10s) [Lethal Current]"
          And under "Lethal Current · Arc buff", "Is ended or used up by" lists only "Melee damage → consumes Lethal Current (while Lethal Current) [Lethal Current]"
        """)]
    public async Task TheOutcomesTabListsWhatEndsABuffUnderIsEndedOrUsedUpBy(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .ListsOnlyUnder("Lethal Current · Arc buff", "Is given by", "Class ability → Lethal Current (10s) [Lethal Current]")
            .ListsOnlyUnder("Lethal Current · Arc buff", "Is ended or used up by", "Melee damage → consumes Lethal Current (while Lethal Current) [Lethal Current]");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The Outcomes tab orders its outcomes by group, then by name
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          # Buffs on you, then debuffs on the target, then strikes, then damage resistance; by name within each group.
          Then its outcomes are, in order, "Amplified · Arc buff", "Lethal Current · Arc buff", "Blind · Arc debuff", "Jolt · Arc debuff", "Jolt strike · Strike", "Lethal Current strike · Strike" and "Damage resistance"
        """)]
    public async Task TheOutcomesTabOrdersItsOutcomesByGroupThenByName(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        // Buffs on you, then debuffs on the target, then strikes, then damage resistance; by name within each group.
        Asserting.That(explained)
            .HasOutcomesInOrder(
                "Amplified · Arc buff", "Lethal Current · Arc buff", "Blind · Arc debuff", "Jolt · Arc debuff",
                "Jolt strike · Strike", "Lethal Current strike · Strike", "Damage resistance");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Closing and reopening "What this build does" opens it on Triggers again
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          Then the "Outcomes" tab is selected
          When I tap "What this build does" to close it
          And I tap "What this build does" to open it again
          Then the "Triggers" tab is selected
        """)]
    public async Task ClosingAndReopeningWhatThisBuildDoesOpensItOnTriggersAgain(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();

        // Then
        var byOutcome = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(byOutcome).HasSelectedTab("Outcomes");

        // When
        await Frontend.Designer.ExplainPanel.ClickCloseAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilClosedAsync();
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var reopened = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(reopened).HasSelectedTab("Triggers");
    }

    [Theory, PlayInMobile]
    [TestDescription("""
        @mobile
        Scenario: On a phone, the tabs and the Outcomes rows fit inside the screen
          # a phone only: on a desktop every row fits on one line
          Given the build "outcomes-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          Then the tabs "Triggers" and "Outcomes" sit side by side, inside the panel
          And no line of the panel reaches past its right edge
          And the page doesn't scroll sideways
        """)]
    public async Task OnAPhoneTheTabsAndTheOutcomesRowsFitInsideTheScreen(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("outcomes-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();

        // Then
        var layout = await Frontend.Designer.ExplainPanel.ReadLayoutAsync();
        Asserting.That(layout)
            .HasTabsSideBySideInsideThePanel("Triggers", "Outcomes")
            .HasEveryLineInsideThePanel()
            .FitsInsideTheScreen();
    }
}
