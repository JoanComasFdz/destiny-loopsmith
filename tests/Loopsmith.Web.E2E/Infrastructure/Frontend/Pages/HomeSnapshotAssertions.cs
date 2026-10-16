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
}
