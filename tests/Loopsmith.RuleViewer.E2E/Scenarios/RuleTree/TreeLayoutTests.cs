using JoanComasFdz.AssertingThat;
using Loopsmith.RuleViewer.E2E.Infrastructure;
using Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

namespace Loopsmith.RuleViewer.E2E.Scenarios.RuleTree;

/// <summary>Where the tree sits: beside the flows on a desktop, above them on a phone.</summary>
public sealed class TreeLayoutTests : BaseE2E
{
    [Theory, PlayInDesktop]
    [TestDescription("""
        @desktop
        Scenario: On a desktop the tree sits left of the flows
          # Desktop only: a phone stacks the tree above the flows (the scenario after this one).
          Given the rules page of the fixture rules is open
          Then the tree is left of the flows, their tops in line
          And the tree and the flows don't overlap
        """)]
    public async Task OnADesktopTheTreeSitsLeftOfTheFlows(Screen screen)
    {
        // Then
        var layout = await RulesPage.ReadLayoutAsync();
        Asserting.That(layout)
            .HasTheTreeLeftOfTheFlows()
            .HasNothingOverlapping();
    }

    [Theory, PlayInMobile]
    [TestDescription("""
        @mobile
        Scenario: On a phone the tree sits above the flows
          # Phone only: below 760 px the page stacks the tree above the flows; a desktop puts it beside them.
          Given the rules page of the fixture rules is open
          Then the tree is above the flows, as wide as they are
          And the tree and the flows don't overlap
          And the page doesn't scroll sideways
        """)]
    public async Task OnAPhoneTheTreeSitsAboveTheFlows(Screen screen)
    {
        // Then
        var layout = await RulesPage.ReadLayoutAsync();
        Asserting.That(layout)
            .HasTheTreeAboveTheFlowsFullWidth()
            .HasNothingOverlapping()
            .DoesNotScrollSideways();
    }
}
