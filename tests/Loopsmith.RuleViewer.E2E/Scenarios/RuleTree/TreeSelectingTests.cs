using JoanComasFdz.AssertingThat;
using Loopsmith.RuleViewer.E2E.Infrastructure;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

namespace Loopsmith.RuleViewer.E2E.Scenarios.RuleTree;

/// <summary>The tree's names: an item is selected on its own and its flows are brought into view, ticking nothing.</summary>
public sealed class TreeSelectingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Tapping a name selects that item alone and brings its flows into view
          Given the rules page of the fixture rules is open
          When I tap the name "Firepower"
          Then the flows of "Firepower" are at the top of the view
          And "Firepower" is the only item highlighted
          And every checkbox in the tree is still ticked
          When I tap the name "hunter/"
          # A folder has no flows of its own: its first file's stand for it.
          Then the flows of "melees.yaml" are at the top of the view
          And "hunter/" is the only item highlighted
        """)]
    public async Task TappingANameSelectsThatItemAloneAndBringsItsFlowsIntoView(Screen screen)
    {
        // When
        await RulesPage.Tree.ClickNameAsync("Firepower");

        // Then
        var element = await RulesPage.Flows.ReadInPlaceAsync();
        Asserting.That(element).HasAtTheTopOfTheView("Firepower");
        var selected = await RulesPage.Tree.ReadAsync();
        Asserting.That(selected)
            .HasOnlySelected("Firepower")
            .HasEveryItemTicked();

        // When
        await RulesPage.Tree.ClickNameAsync("hunter/");

        // Then
        var folder = await RulesPage.Flows.ReadInPlaceAsync();
        Asserting.That(folder).HasAtTheTopOfTheView("melees.yaml");
        var reselected = await RulesPage.Tree.ReadAsync();
        Asserting.That(reselected).HasOnlySelected("hunter/");
    }
}
