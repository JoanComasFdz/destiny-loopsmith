using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the notice banner.</summary>
public static class NoticeBannerSnapshotAssertions
{
    /// <summary>The notice says exactly <paramref name="text"/>.</summary>
    public static AssertingThat<NoticeBannerSnapshot> Reads(this AssertingThat<NoticeBannerSnapshot> that, string text)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.Shown > 0, $"No notice is shown, but one reading \"{text}\" was expected.");
        Assert.True(shown.Text == text, $"The notice reads \"{shown.Text}\", but \"{text}\" was expected.");
        return that;
    }

    /// <summary>Exactly one notice is shown.</summary>
    public static AssertingThat<NoticeBannerSnapshot> IsShownOnce(this AssertingThat<NoticeBannerSnapshot> that)
    {
        var shown = that.InstanceToAssert.Shown;
        Assert.True(shown == 1, $"{shown} notices are shown, but exactly one was expected.");
        return that;
    }

    /// <summary>The notice is not shown.</summary>
    public static AssertingThat<NoticeBannerSnapshot> IsNotShown(this AssertingThat<NoticeBannerSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.Shown == 0, $"The notice reads \"{shown.Text}\", but none was expected.");
        return that;
    }
}
