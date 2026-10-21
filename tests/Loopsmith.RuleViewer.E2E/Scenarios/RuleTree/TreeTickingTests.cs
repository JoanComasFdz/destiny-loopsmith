using JoanComasFdz.AssertingThat;
using Loopsmith.RuleViewer.E2E.Infrastructure;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

namespace Loopsmith.RuleViewer.E2E.Scenarios.RuleTree;

/// <summary>The tree's checkboxes: an item shows or hides its flows, ticks or unticks all under it, and dashes the items above.</summary>
public sealed class TreeTickingTests : BaseE2E
{
    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unticking an element hides its flows and dashes the items above it
          Given the rules page of the fixture rules is open
          Then every checkbox in the tree is ticked
          When I untick "Combination Blow"
          Then "combination-blow-1" and "combination-blow-2" are unticked
          And "melees.yaml", "arc/", "hunter/" and "rules/" show a dash
          And the flows of "combination-blow-1" and "combination-blow-2" are hidden
          And the flows of "disorienting-blow-1" still show, with its wires
          When I tick "melees.yaml"
          Then every item under "melees.yaml" is ticked
          And "melees.yaml", "arc/", "hunter/" and "rules/" are ticked, with no dash
          And the flows of "combination-blow-1" and "combination-blow-2" show again, each with its wires
        """)]
    public async Task UntickingAnElementHidesItsFlowsAndDashesTheItemsAboveIt(Screen screen)
    {
        // Then
        var opened = await RulesPage.Tree.ReadAsync();
        Asserting.That(opened).HasEveryItemTicked();

        // When
        await RulesPage.Tree.UntickAsync("Combination Blow");

        // Then
        var unticked = await RulesPage.Tree.ReadAsync();
        Asserting.That(unticked)
            .HasUnticked("combination-blow-1", "combination-blow-2")
            .HasDashed("melees.yaml", "arc/", "hunter/", "rules/");
        var hidden = await RulesPage.Flows.ReadAsync("disorienting-blow-1");
        Asserting.That(hidden)
            .Hides("combination-blow-1", "combination-blow-2")
            .ShowsWithItsWires("disorienting-blow-1");

        // When
        await RulesPage.Tree.TickAsync("melees.yaml");

        // Then
        var ticked = await RulesPage.Tree.ReadAsync();
        Asserting.That(ticked)
            .HasEveryItemUnderTicked("melees.yaml")
            .HasTicked("melees.yaml", "arc/", "hunter/", "rules/");
        var shown = await RulesPage.Flows.ReadAsync("combination-blow-1");
        Asserting.That(shown).ShowsWithItsWires("combination-blow-1", "combination-blow-2");
    }

    [Theory, PlayInDesktop, PlayInMobile]
    [TestDescription("""
        @desktop @mobile
        Scenario: Unticking a folder hides every flow under it
          Given the rules page of the fixture rules is open
          When I untick "arc/"
          Then every item under "arc/" is unticked
          And "hunter/" is unticked
          And "rules/" shows a dash
          And no flow of "melees.yaml" or "supers.yaml" shows
          And the flows of "arms.yaml" still show, with its wires
        """)]
    public async Task UntickingAFolderHidesEveryFlowUnderIt(Screen screen)
    {
        // When
        await RulesPage.Tree.UntickAsync("arc/");

        // Then
        var tree = await RulesPage.Tree.ReadAsync();
        Asserting.That(tree)
            .HasEveryItemUnderUnticked("arc/")
            .HasUnticked("hunter/")
            .HasDashed("rules/");
        var flows = await RulesPage.Flows.ReadAsync("arms.yaml");
        Asserting.That(flows)
            .Hides("melees.yaml", "supers.yaml")
            .ShowsWithItsWires("arms.yaml");
    }
}
