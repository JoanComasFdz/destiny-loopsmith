using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on where the notice is on the screen (layout, from its box).</summary>
public static class NoticeBannerLayoutAssertions
{
    // Less than a pixel apart is touching: boxes are measured in fractions of a pixel.
    private const double Tolerance = 1;

    // "Right under": no further below the header than a small gap.
    private const double RightUnder = 24;

    /// <summary>The notice starts right under the header: not under it, and no more than a small gap below it.</summary>
    public static AssertingThat<NoticeBannerLayout> SitsRightUnderTheHeader(this AssertingThat<NoticeBannerLayout> that)
    {
        var shown = that.InstanceToAssert;
        var below = shown.Notice.Top - shown.Header.Bottom;
        Assert.True(
            below >= -Tolerance && below <= RightUnder,
            $"The notice starts {below:0} px under the header, but it was expected right under it (0–{RightUnder:0} px).");
        return that;
    }

    /// <summary>The notice fits inside the screen, and the page doesn't scroll sideways.</summary>
    public static AssertingThat<NoticeBannerLayout> FitsInsideTheScreen(this AssertingThat<NoticeBannerLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.Notice.Left >= -Tolerance && shown.Notice.Right <= shown.ScreenWidth + Tolerance
            && shown.Notice.Top >= -Tolerance && shown.Notice.Bottom <= shown.ScreenHeight + Tolerance,
            $"The notice spans {shown.Notice.Left:0}–{shown.Notice.Right:0} px across and {shown.Notice.Top:0}–{shown.Notice.Bottom:0} px down on a screen {shown.ScreenWidth:0}×{shown.ScreenHeight:0} px, but it was expected inside it.");
        Assert.True(
            shown.PageWidth <= shown.ScreenWidth + Tolerance,
            $"The page is {shown.PageWidth:0} px wide on a screen {shown.ScreenWidth:0} px wide (it scrolls sideways), but it was expected to fit.");
        return that;
    }

    /// <summary>Where the player acted is still in view: below the notice and above the screen's bottom.</summary>
    public static AssertingThat<NoticeBannerLayout> KeepsTheImportLinkInView(this AssertingThat<NoticeBannerLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.ActedOn.Top >= shown.Notice.Bottom - Tolerance && shown.ActedOn.Bottom <= shown.ScreenHeight + Tolerance,
            $"The \"import a build.yaml\" link spans {shown.ActedOn.Top:0}–{shown.ActedOn.Bottom:0} px down, the notice ends at {shown.Notice.Bottom:0} px on a screen {shown.ScreenHeight:0} px high, but the link was expected in view below the notice.");
        return that;
    }
}
