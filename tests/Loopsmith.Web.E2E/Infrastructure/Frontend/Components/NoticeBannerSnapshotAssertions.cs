using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the notice banner.</summary>
public static class NoticeBannerSnapshotAssertions
{
    /// <summary>The notice says exactly <paramref name="text"/>.</summary>
    public static AssertingThat<NoticeBannerSnapshot> Reads(this AssertingThat<NoticeBannerSnapshot> that, string text)
    {
        var shown = that.InstanceToAssert.Text;
        Assert.True(shown == text, $"The notice reads \"{shown}\", but \"{text}\" was expected.");
        return that;
    }
}
