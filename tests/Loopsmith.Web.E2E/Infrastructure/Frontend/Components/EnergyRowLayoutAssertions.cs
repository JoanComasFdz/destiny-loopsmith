using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on where a step card's ENERGY row is on the screen (layout, from its box).</summary>
public static class EnergyRowLayoutAssertions
{
    // Less than a pixel over is inside: boxes are measured in fractions of a pixel.
    private const double Tolerance = 1;

    /// <summary>The ENERGY row fits inside the screen, and the page doesn't scroll sideways.</summary>
    public static AssertingThat<EnergyRowLayout> FitsInsideTheScreen(this AssertingThat<EnergyRowLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.Row.Left >= -Tolerance && shown.Row.Right <= shown.ScreenWidth + Tolerance,
            $"The ENERGY row spans {shown.Row.Left:0}–{shown.Row.Right:0} px on a screen {shown.ScreenWidth:0} px wide, but it was expected inside it.");
        Assert.True(
            shown.PageWidth <= shown.ScreenWidth + Tolerance,
            $"The page is {shown.PageWidth:0} px wide on a screen {shown.ScreenWidth:0} px wide: it scrolls sideways, but it was expected not to.");
        return that;
    }
}
