using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the loop.</summary>
public static class LoopTimelineSnapshotAssertions
{
    /// <summary>The loop has no step after the fresh spawn.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasNoSteps(this AssertingThat<LoopTimelineSnapshot> that)
    {
        var shown = that.InstanceToAssert.StepCount;
        Assert.True(shown == 0, $"The loop shows {shown} step{(shown == 1 ? "" : "s")}, but none was expected.");
        return that;
    }
}
