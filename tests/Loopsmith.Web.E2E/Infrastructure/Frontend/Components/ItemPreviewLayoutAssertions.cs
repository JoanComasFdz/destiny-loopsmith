using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on a name's preview: its title, and that it shows next to the name, inside the screen.</summary>
public static class ItemPreviewLayoutAssertions
{
    // The preview sits 6 px under its name; a little more allows for rounding and a name drawn on two lines.
    private const double NextTo = 12;
    private const double Tolerance = 1;

    /// <summary>The preview is titled with <paramref name="keyword"/>'s name.</summary>
    public static AssertingThat<ItemPreviewLayout> IsTitled(this AssertingThat<ItemPreviewLayout> that, Keyword keyword)
    {
        var title = that.InstanceToAssert.Title;
        Assert.True(title == keyword.ToShownText(), $"The preview is titled \"{title}\", but \"{keyword.ToShownText()}\" was expected.");
        return that;
    }

    /// <summary>The preview's edge is at most a few pixels from its name's, under or over it.</summary>
    public static AssertingThat<ItemPreviewLayout> ShowsNextToItsName(this AssertingThat<ItemPreviewLayout> that)
    {
        var shown = that.InstanceToAssert;
        var gap = Math.Min(Math.Abs(shown.Box.Top - shown.Name.Bottom), Math.Abs(shown.Name.Top - shown.Box.Bottom));
        var overlapsAcross = shown.Box.Left < shown.Name.Right && shown.Box.Right > shown.Name.Left;
        Assert.True(gap <= NextTo && overlapsAcross, $"The preview is {gap:0} px from its name (or beside it), but it was expected right next to it.");
        return that;
    }

    /// <summary>The whole preview is inside the window.</summary>
    public static AssertingThat<ItemPreviewLayout> FitsInsideTheScreen(this AssertingThat<ItemPreviewLayout> that)
    {
        var shown = that.InstanceToAssert;
        var box = shown.Box;
        Assert.True(
            box.Left >= -Tolerance && box.Top >= -Tolerance && box.Right <= shown.ScreenWidth + Tolerance && box.Bottom <= shown.ScreenHeight + Tolerance,
            $"The preview reaches ({box.Left:0}, {box.Top:0})–({box.Right:0}, {box.Bottom:0}) on a {shown.ScreenWidth:0}×{shown.ScreenHeight:0} screen, but it was expected inside it.");
        return that;
    }
}
