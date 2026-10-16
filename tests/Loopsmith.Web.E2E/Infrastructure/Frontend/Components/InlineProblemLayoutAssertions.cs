using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on where a problem shown where the player acted is on the screen (layout, from its box).</summary>
public static class InlineProblemLayoutAssertions
{
    // Less than a pixel apart is touching: boxes are measured in fractions of a pixel.
    private const double Tolerance = 1;

    // "Right under": no further below the link than the rest of its paragraph (a line or two on a phone) and the gap.
    private const double RightUnder = 64;

    /// <summary>The problem starts below the link, no further than a line or two under it, and spans the link's left edge.</summary>
    public static AssertingThat<InlineProblemLayout> SitsRightUnderTheLink(this AssertingThat<InlineProblemLayout> that)
    {
        var shown = that.InstanceToAssert;
        var below = shown.Problem.Top - shown.Link.Bottom;
        Assert.True(
            below >= -Tolerance && below <= RightUnder,
            $"The import problem starts {below:0} px under the \"import a build.yaml\" link, but it was expected right under it (0–{RightUnder:0} px).");
        Assert.True(
            shown.Problem.Left <= shown.Link.Left + Tolerance && shown.Problem.Right >= shown.Link.Left - Tolerance,
            $"The import problem spans {shown.Problem.Left:0}–{shown.Problem.Right:0} px across, the link starts at {shown.Link.Left:0} px, but the problem was expected under it.");
        return that;
    }

    /// <summary>The problem fits inside the screen, and the page doesn't scroll sideways.</summary>
    public static AssertingThat<InlineProblemLayout> FitsInsideTheScreen(this AssertingThat<InlineProblemLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.Problem.Left >= -Tolerance && shown.Problem.Right <= shown.ScreenWidth + Tolerance
            && shown.Problem.Top >= -Tolerance && shown.Problem.Bottom <= shown.ScreenHeight + Tolerance,
            $"The import problem spans {shown.Problem.Left:0}–{shown.Problem.Right:0} px across and {shown.Problem.Top:0}–{shown.Problem.Bottom:0} px down on a screen {shown.ScreenWidth:0}×{shown.ScreenHeight:0} px, but it was expected inside it.");
        Assert.True(
            shown.PageWidth <= shown.ScreenWidth + Tolerance,
            $"The page is {shown.PageWidth:0} px wide on a screen {shown.ScreenWidth:0} px wide (it scrolls sideways), but it was expected to fit.");
        return that;
    }
}
