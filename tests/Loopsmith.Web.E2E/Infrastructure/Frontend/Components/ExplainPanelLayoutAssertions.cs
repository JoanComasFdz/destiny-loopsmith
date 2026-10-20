using System.Text.RegularExpressions;
using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on where the "What this build does" panel's rows are on the screen (layout, from their boxes).</summary>
public static class ExplainPanelLayoutAssertions
{
    // Less than a pixel apart is touching: boxes are measured in fractions of a pixel.
    private const double Tolerance = 1;

    /// <summary>
    /// The row reading <paramref name="effect"/> ("Sidearms: … [Mechaneer's Tricksleeves]", its branch and alignment left
    /// out) is drawn on several lines, and its source ends on the last of them.
    /// </summary>
    public static AssertingThat<ExplainPanelLayout> WrapsWithItsSourceOnTheLastLine(this AssertingThat<ExplainPanelLayout> that, string effect)
    {
        var rows = that.InstanceToAssert.Lines.Where(line => ReadEffect(line.Text) == effect).ToArray();
        Assert.True(rows.Length == 1, $"The panel shows {rows.Length} rows reading \"{effect}\", but one was expected.");
        Assert.True(rows[0].LineCount > 1, $"The row \"{effect}\" is drawn on {rows[0].LineCount} line, but it was expected to wrap onto several.");
        Assert.True(rows[0].EndsOnLastLine, $"The row \"{effect}\" wraps, but its source doesn't end on its last line.");
        return that;
    }

    /// <summary>Every line of the panel ends inside it, so nothing in it scrolls sideways.</summary>
    public static AssertingThat<ExplainPanelLayout> HasEveryLineInsideThePanel(this AssertingThat<ExplainPanelLayout> that)
    {
        var shown = that.InstanceToAssert;
        var past = shown.Lines.Where(line => line.TextBox.Right > shown.Panel.Right + Tolerance).Select(line => $"\"{line.Text}\"").ToArray();
        Assert.True(past.Length == 0, $"The lines {string.Join(", ", past)} reach past the panel's right edge ({shown.Panel.Right:0} px), but every line was expected inside it.");
        Assert.False(shown.ScrollsSideways, "The panel's lines scroll sideways, but they were expected to fit.");
        return that;
    }

    /// <summary>The page doesn't scroll sideways.</summary>
    public static AssertingThat<ExplainPanelLayout> FitsInsideTheScreen(this AssertingThat<ExplainPanelLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.PageWidth <= shown.ScreenWidth + Tolerance,
            $"The page is {shown.PageWidth:0} px wide on a screen {shown.ScreenWidth:0} px wide (it scrolls sideways), but it was expected to fit.");
        return that;
    }

    // "└ Sidearms: …  [Mechaneer's Tricksleeves]" → "Sidearms: … [Mechaneer's Tricksleeves]"; a heading as it is.
    private static string ReadEffect(string line) =>
        line.StartsWith("├ ", StringComparison.Ordinal) || line.StartsWith("└ ", StringComparison.Ordinal)
            ? Regex.Replace(line[2..], "\\s+", " ")
            : line;
}
