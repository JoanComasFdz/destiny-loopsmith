using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>What a test checks on Home.</summary>
public static class HomeSnapshotAssertions
{
    /// <summary>Home is the page shown: the designer didn't open.</summary>
    public static AssertingThat<HomeSnapshot> IsShown(this AssertingThat<HomeSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.IsShown, "The designer is shown, but Home was expected.");
        return that;
    }

    /// <summary>Home shows exactly one import problem.</summary>
    public static AssertingThat<HomeSnapshot> ShowsOneImportProblem(this AssertingThat<HomeSnapshot> that)
    {
        var shown = that.InstanceToAssert.ImportProblems;
        Assert.True(shown == 1, $"Home shows {shown} import problems, but exactly one was expected.");
        return that;
    }

    /// <summary>Home shows no import problem.</summary>
    public static AssertingThat<HomeSnapshot> ShowsNoImportProblem(this AssertingThat<HomeSnapshot> that)
    {
        var shown = that.InstanceToAssert.ImportProblems;
        Assert.True(shown == 0, $"Home shows {shown} import problem(s), but none was expected.");
        return that;
    }
}
