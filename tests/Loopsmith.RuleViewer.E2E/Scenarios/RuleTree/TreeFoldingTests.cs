using JoanComasFdz.AssertingThat;
using Loopsmith.RuleViewer.E2E.Infrastructure;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

namespace Loopsmith.RuleViewer.E2E.Scenarios.RuleTree;

/// <summary>The tree's arrows: an item folds or opens in the tree, and the flows beside it stay as they are.</summary>
public sealed class TreeFoldingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: The arrow folds an item in the tree and hides none of its flows
          Given the rules page of the fixture rules is open
          Then "rules/", "hunter/", "arc/" and "melees.yaml" are open in the tree
          And "Combination Blow" is folded
          When I tap the arrow of "Combination Blow"
          Then "combination-blow-1" and "combination-blow-2" show under it
          When I tap the arrow of "melees.yaml"
          Then "Combination Blow" and "Disorienting Blow" are hidden in the tree
          And the flows of "combination-blow-1", "combination-blow-2" and "disorienting-blow-1" still show, each with its wires
        """)]
    public async Task TheArrowFoldsAnItemInTheTreeAndHidesNoneOfItsFlows(Screen screen)
    {
        // Then
        var opened = await RulesPage.Tree.ReadAsync();
        Asserting.That(opened)
            .HasOpen("rules/", "hunter/", "arc/", "melees.yaml")
            .HasFolded("Combination Blow");

        // When
        await RulesPage.Tree.ClickArrowAsync("Combination Blow");

        // Then
        var unfolded = await RulesPage.Tree.ReadAsync();
        Asserting.That(unfolded).ShowsUnder("Combination Blow", "combination-blow-1", "combination-blow-2");

        // When
        await RulesPage.Tree.ClickArrowAsync("melees.yaml");

        // Then
        var folded = await RulesPage.Tree.ReadAsync();
        Asserting.That(folded).HidesInTheTree("Combination Blow", "Disorienting Blow");
        var flows = await RulesPage.Flows.ReadAsync("combination-blow-1");
        Asserting.That(flows).ShowsWithItsWires("combination-blow-1", "combination-blow-2", "disorienting-blow-1");
    }
}
