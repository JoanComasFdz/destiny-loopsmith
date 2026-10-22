using JoanComasFdz.AssertingThat;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>What a test checks on where the rules page's parts are.</summary>
public static class RulesPageLayoutAssertions
{
    /// <summary>The tree sits left of the flows, their tops in line.</summary>
    public static AssertingThat<RulesPageLayout> HasTheTreeLeftOfTheFlows(this AssertingThat<RulesPageLayout> that)
    {
        var (tree, flows) = (that.InstanceToAssert.Tree, that.InstanceToAssert.Flows);
        Assert.True(tree.Right <= flows.Left, $"The tree ends at x={tree.Right:0}, past the flows' left edge at x={flows.Left:0}: it should sit left of them.");
        Assert.True(Math.Abs(tree.Top - flows.Top) <= 2, $"The tree's top is at y={tree.Top:0} and the flows' at y={flows.Top:0}: they should be in line.");
        return that;
    }

    /// <summary>The tree sits above the flows, as wide as they are.</summary>
    public static AssertingThat<RulesPageLayout> HasTheTreeAboveTheFlowsFullWidth(this AssertingThat<RulesPageLayout> that)
    {
        var (tree, flows) = (that.InstanceToAssert.Tree, that.InstanceToAssert.Flows);
        Assert.True(tree.Bottom <= flows.Top, $"The tree ends at y={tree.Bottom:0}, below the flows' top at y={flows.Top:0}: it should sit above them.");
        Assert.True(
            Math.Abs(tree.Left - flows.Left) <= 1 && Math.Abs(tree.Right - flows.Right) <= 1,
            $"The tree spans x={tree.Left:0}–{tree.Right:0} and the flows x={flows.Left:0}–{flows.Right:0}: the tree should be as wide as the flows.");
        return that;
    }

    /// <summary>The tree and the flows don't overlap.</summary>
    public static AssertingThat<RulesPageLayout> HasNothingOverlapping(this AssertingThat<RulesPageLayout> that)
    {
        Assert.False(that.InstanceToAssert.Tree.Overlaps(that.InstanceToAssert.Flows), $"The tree ({that.InstanceToAssert.Tree}) overlaps the flows ({that.InstanceToAssert.Flows}).");
        return that;
    }

    /// <summary>The page is no wider than its window: it doesn't scroll sideways.</summary>
    public static AssertingThat<RulesPageLayout> DoesNotScrollSideways(this AssertingThat<RulesPageLayout> that)
    {
        Assert.True(
            that.InstanceToAssert.PageWidth <= that.InstanceToAssert.WindowWidth,
            $"The page is {that.InstanceToAssert.PageWidth:0} px wide in a {that.InstanceToAssert.WindowWidth:0} px window: it scrolls sideways.");
        return that;
    }
}
