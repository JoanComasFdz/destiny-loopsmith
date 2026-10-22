using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the "Loop analysis" popup.</summary>
public static class LoopAnalysisSnapshotAssertions
{
    /// <summary>The popup's verdict badge reads <paramref name="expected"/> ("✗ Breaks at #1").</summary>
    public static AssertingThat<LoopAnalysisSnapshot> HasVerdict(this AssertingThat<LoopAnalysisSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.Verdict;
        Assert.True(shown == expected, $"The analysis's verdict reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>The popup's verdict sentence reads <paramref name="expected"/>.</summary>
    public static AssertingThat<LoopAnalysisSnapshot> SaysVerdict(this AssertingThat<LoopAnalysisSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.VerdictText;
        Assert.True(shown == expected, $"The analysis says \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>Step <paramref name="number"/> of the analysis reads "not ready" <paramref name="expected"/>.</summary>
    public static AssertingThat<LoopAnalysisSnapshot> SaysNotReady(this AssertingThat<LoopAnalysisSnapshot> that, int number, string expected)
    {
        var steps = that.InstanceToAssert.Steps;
        var shown = number <= steps.Length ? steps[number - 1].NotReady : null;
        Assert.True(shown == expected, $"Step #{number} of the analysis reads \"not ready\" {(shown is null ? "nowhere" : $"\"{shown}\"")}, but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>Step <paramref name="number"/> of the analysis reads "may not be ready" <paramref name="expected"/>.</summary>
    public static AssertingThat<LoopAnalysisSnapshot> SaysMayNotBeReady(this AssertingThat<LoopAnalysisSnapshot> that, int number, string expected)
    {
        var steps = that.InstanceToAssert.Steps;
        var shown = number <= steps.Length ? steps[number - 1].MayNotBeReady : null;
        Assert.True(shown == expected, $"Step #{number} of the analysis reads \"may not be ready\" {(shown is null ? "nowhere" : $"\"{shown}\"")}, but \"{expected}\" was expected.");
        return that;
    }
}
