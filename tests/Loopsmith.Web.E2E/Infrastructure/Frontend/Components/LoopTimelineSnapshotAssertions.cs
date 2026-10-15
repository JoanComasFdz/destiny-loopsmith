using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the loop.</summary>
public static class LoopTimelineSnapshotAssertions
{
    /// <summary>The loop has no step after the fresh spawn.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasNoSteps(this AssertingThat<LoopTimelineSnapshot> that)
    {
        var shown = that.InstanceToAssert.Steps.Length;
        Assert.True(shown == 0, $"The loop shows {shown} step{(shown == 1 ? "" : "s")}, but none was expected.");
        return that;
    }

    /// <summary>Step <paramref name="number"/> (#1 is the first after the fresh spawn) is <paramref name="expected"/>.</summary>
    public static AssertingThat<LoopTimelineSnapshot> HasStep(this AssertingThat<LoopTimelineSnapshot> that, int number, LoopStep expected)
    {
        var steps = that.InstanceToAssert.Steps;
        Assert.True(steps.Length >= number, $"The loop shows {steps.Length} steps, but step #{number} was expected to be \"{expected.ToShownText()}\".");
        var shown = steps[number - 1];
        Assert.True(shown == expected, $"Step #{number} shows \"{shown.ToShownText()}\", but \"{expected.ToShownText()}\" was expected.");
        return that;
    }
}
