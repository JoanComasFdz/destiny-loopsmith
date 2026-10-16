using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the notice banner.</summary>
public static class NoticeBannerSnapshotAssertions
{
    /// <summary>The notice says exactly <paramref name="text"/>.</summary>
    public static AssertingThat<NoticeBannerSnapshot> Reads(this AssertingThat<NoticeBannerSnapshot> that, string text)
    {
        Assert.True(that.InstanceToAssert.IsShown, $"No notice is shown, but one reading \"{text}\" was expected.");
        var shown = that.InstanceToAssert.Text;
        Assert.True(shown == text, $"The notice reads \"{shown}\", but \"{text}\" was expected.");
        return that;
    }

    /// <summary>The notice is not shown.</summary>
    public static AssertingThat<NoticeBannerSnapshot> IsNotShown(this AssertingThat<NoticeBannerSnapshot> that)
    {
        Assert.True(!that.InstanceToAssert.IsShown, $"The notice at the top of the page reads \"{that.InstanceToAssert.Text}\", but none was expected.");
        return that;
    }
}
