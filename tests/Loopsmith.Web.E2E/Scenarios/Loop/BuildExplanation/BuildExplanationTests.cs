using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>"What this build does" above the loop: every trigger of the build and what it sets off, as a tree.</summary>
public sealed class BuildExplanationTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: "What this build does" shows each trigger with one outcome per line, and no Note/Tree choice
          Given the build "build-explanation-mechaneers-tricksleeves" is imported, with no steps
          When I tap "What this build does" above the loop
          Then the panel shows no "Note" or "Tree" choice
          And its "Always" heading is followed by the line "├ Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed  [Mechaneer's Tricksleeves]"
        """)]
    public async Task WhatThisBuildDoesShowsEachTriggerWithOneOutcomePerLineAndNoNoteTreeChoice(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("build-explanation-mechaneers-tricksleeves.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .OffersNoStyleChoice()
            .HasRowUnder("Always", "├ Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed  [Mechaneer's Tricksleeves]");
    }

    [Theory, PlayInMobile]
    [TestDescription("""
        @mobile
        Scenario: On a phone, a long row of "What this build does" wraps inside the panel
          # a phone only: on a desktop the row fits on one line
          Given the build "build-explanation-mechaneers-tricksleeves" is imported, with no steps
          When I tap "What this build does" above the loop
          Then its row "Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed [Mechaneer's Tricksleeves]" wraps onto several lines, its source on the last one
          And no line of the panel reaches past its right edge
          And the page doesn't scroll sideways
        """)]
    public async Task OnAPhoneALongRowOfWhatThisBuildDoesWrapsInsideThePanel(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("build-explanation-mechaneers-tricksleeves.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var layout = await Frontend.Designer.ExplainPanel.ReadLayoutAsync();
        Asserting.That(layout)
            .WrapsWithItsSourceOnTheLastLine("Sidearms: +50 Airborne Effectiveness, +100 Handling, +100 Reload Speed [Mechaneer's Tricksleeves]")
            .HasEveryLineInsideThePanel()
            .FitsInsideTheScreen();
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: "What this build does" lists no keyword the build doesn't use
          Given the build "build-explanation-mechaneers-tricksleeves" is imported, with no steps
          When I tap "What this build does" above the loop
          Then the panel shows no row whose source is "[Bolt Charge]", "[Jolt]", "[Ionic Trace]", "[Amplified]", "[Guardian]", "[Orb of Power]", "[Unraveling Rounds]", "[Woven Mail]" or "[Super]"
        """)]
    public async Task WhatThisBuildDoesListsNoKeywordTheBuildDoesntUse(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("build-explanation-mechaneers-tricksleeves.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .HasNoRowFrom("[Bolt Charge]", "[Jolt]", "[Ionic Trace]", "[Amplified]", "[Guardian]", "[Orb of Power]", "[Unraveling Rounds]", "[Woven Mail]", "[Super]");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: "What this build does" lists a keyword the build gives
          Given the build "build-explanation-shinobus-vow" is imported, with no steps
          When I tap "What this build does" above the loop
          Then the panel has a row whose source is "[Bolt Charge]" under "Gain Bolt Charge"
          And it shows no row whose source is "[Jolt]" or "[Woven Mail]"
        """)]
    public async Task WhatThisBuildDoesListsAKeywordTheBuildGives(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("build-explanation-shinobus-vow.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .HasRowFromUnder("Gain Bolt Charge", "[Bolt Charge]")
            .HasNoRowFrom("[Jolt]", "[Woven Mail]");
    }
}
