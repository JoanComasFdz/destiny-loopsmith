using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure;
using Loopsmith.Web.E2E.Infrastructure.Frontend.Components;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Scenarios.Loop;

/// <summary>A keyword or a source named in "What this build does", in either tab, opens its details as in a step's card.</summary>
public sealed class ExplanationKeywordsTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tapping a keyword in the Triggers tab opens its details
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap "Amplified" in the row "Jolt target (10s) and Amplified (15s) [Ascension]"
          Then the item details popup opens, titled "Amplified"
        """)]
    public async Task TappingAKeywordInTheTriggersTabOpensItsDetails(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickNameAsync("Jolt target (10s) and Amplified (15s) [Ascension]", new Keyword.Amplified());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details).IsTitled(new Keyword.Amplified());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tapping an outcome's heading keyword in the Outcomes tab opens its details
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          And I tap "Jolt" in the heading "Jolt · Arc debuff"
          Then the item details popup opens, titled "Jolt"
          And its "In this build" section names "Ascension" and "Lethal Current"
        """)]
    public async Task TappingAnOutcomesHeadingKeywordInTheOutcomesTabOpensItsDetails(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();
        await Frontend.Designer.ExplainPanel.ClickNameAsync("Jolt · Arc debuff", new Keyword.Jolt());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details)
            .IsTitled(new Keyword.Jolt())
            .NamesInBuild(new Aspect.Ascension(), new Aspect.LethalCurrent());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tapping a keyword inside an Outcomes row opens its details
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          And under "Jolt · Arc debuff" I tap "Jolt" in "Class ability in the air → Jolt target (10s) [Ascension]"
          Then the item details popup opens, titled "Jolt"
        """)]
    public async Task TappingAKeywordInsideAnOutcomesRowOpensItsDetails(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();
        await Frontend.Designer.ExplainPanel.ClickNameUnderAsync("Jolt · Arc debuff", "Class ability in the air → Jolt target (10s) [Ascension]", new Keyword.Jolt());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details).IsTitled(new Keyword.Jolt());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tapping a row's source in "What this build does" opens that element's details
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap "Ascension" in the row "Jolt target (10s) and Amplified (15s) [Ascension]"
          Then the item details popup opens, titled "Ascension"
        """)]
    public async Task TappingARowsSourceInWhatThisBuildDoesOpensThatElementsDetails(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickNameAsync("Jolt target (10s) and Amplified (15s) [Ascension]", new Aspect.Ascension());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();

        // Then
        var details = await Frontend.Designer.ItemDetails.ReadAsync();
        Asserting.That(details).IsTitled(new Aspect.Ascension());
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Closing a keyword's details leaves the panel on the tab it was on
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I tap its "Outcomes" tab
          And I tap "Jolt" in the heading "Jolt · Arc debuff"
          And I close the item details popup
          Then "What this build does" is still open, its "Outcomes" tab selected
        """)]
    public async Task ClosingAKeywordsDetailsLeavesThePanelOnTheTabItWasOn(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.ClickOutcomesTabAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOutcomesShownAsync();
        await Frontend.Designer.ExplainPanel.ClickNameAsync("Jolt · Arc debuff", new Keyword.Jolt());
        await Frontend.Designer.ItemDetails.WaitUntilShownAsync();
        await Frontend.Designer.ItemDetails.ClickCloseAsync();
        await Frontend.Designer.ItemDetails.WaitUntilClosedAsync();

        // Then
        var explained = await Frontend.Designer.ExplainPanel.ReadAsync();
        Asserting.That(explained)
            .IsOpen()
            .HasSelectedTab("Outcomes");
    }

    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        # Hover exists only with a mouse.
        Scenario: Hovering a keyword in "What this build does" shows its preview
          Given the build "explanation-keywords-ascension-lethal-current" is imported, with no steps
          When I tap "What this build does" above the loop
          And I hover "Amplified" in the row "Jolt target (10s) and Amplified (15s) [Ascension]"
          Then a preview titled "Amplified" shows next to it, inside the screen
        """)]
    public async Task HoveringAKeywordInWhatThisBuildDoesShowsItsPreview(Screen screen)
    {
        // Given
        await Frontend.Home.ImportBuildAsync("explanation-keywords-ascension-lethal-current.build.yaml");
        await Frontend.Designer.WaitUntilShownAsync();

        // When
        await Frontend.Designer.ExplainPanel.ClickOpenAsync();
        await Frontend.Designer.ExplainPanel.WaitUntilOpenAsync();
        await Frontend.Designer.ExplainPanel.HoverNameAsync("Jolt target (10s) and Amplified (15s) [Ascension]", new Keyword.Amplified());
        await Frontend.Designer.ExplainPanel.WaitUntilPreviewShownAsync();

        // Then
        var preview = await Frontend.Designer.ExplainPanel.ReadPreviewLayoutAsync();
        Asserting.That(preview)
            .IsTitled(new Keyword.Amplified())
            .ShowsNextToItsName()
            .FitsInsideTheScreen();
    }
}
