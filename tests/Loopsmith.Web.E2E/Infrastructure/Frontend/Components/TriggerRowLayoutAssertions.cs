using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on where the picked row's parts are on the screen (layout, from their boxes).</summary>
public static class TriggerRowLayoutAssertions
{
    // Less than a pixel apart is touching, not overlapping or apart: boxes are measured in fractions of a pixel.
    private const double Tolerance = 1;

    private const string CountName = "the \"×\" enemy count";

    private const string RankName = "the \"Rank\" picker";

    /// <summary>No two of the row's buttons, its option boxes, its "×" enemy count and its "Rank" picker overlap.</summary>
    public static AssertingThat<TriggerRowLayout> HasNoOverlap(this AssertingThat<TriggerRowLayout> that)
    {
        var shown = that.InstanceToAssert;
        var parts = shown.Buttons.Concat(shown.Options).Select(button => (Name: $"\"{button.Caption}\"", button.Box))
            .Concat(shown.Count is { } count ? [(Name: CountName, Box: count)] : [])
            .Concat(shown.Rank is { } rank ? [(Name: RankName, Box: rank)] : [])
            .ToArray();
        var overlapping = parts
            .SelectMany((first, i) => parts.Skip(i + 1).Where(second => Overlap(first.Box, second.Box)).Select(second => $"{first.Name} and {second.Name}"))
            .ToArray();
        Assert.True(overlapping.Length == 0, $"On the picked row {string.Join("; ", overlapping)} overlap, but nothing was expected to.");
        return that;
    }

    /// <summary><paramref name="trigger"/> sits at the row's right end: its right edge is the row content's.</summary>
    public static AssertingThat<TriggerRowLayout> HasAtRightEnd(this AssertingThat<TriggerRowLayout> that, StepTrigger trigger)
    {
        var shown = that.InstanceToAssert;
        var caption = trigger.ToShownText();
        var found = shown.Buttons.Where(button => button.Caption == caption).ToArray();
        Assert.True(found.Length == 1, $"The picked row shows no \"{caption}\" button, but one was expected at its right end.");
        var gap = shown.ContentRight - found[0].Box.Right;
        Assert.True(Math.Abs(gap) <= Tolerance, $"\"{caption}\" ends {gap:0} px from the row's right end, but it was expected at it.");
        return that;
    }

    /// <summary>The row wraps: its buttons and its "×" enemy count sit on more than one line.</summary>
    public static AssertingThat<TriggerRowLayout> WrapsOntoSeveralLines(this AssertingThat<TriggerRowLayout> that)
    {
        var shown = that.InstanceToAssert;
        var parts = shown.Buttons.Select(button => button.Box).Concat(shown.Count is { } count ? [count] : []).ToArray();
        // Two parts are on different lines when one starts below the other's bottom.
        var wraps = parts.Any(first => parts.Any(second => second.Top >= first.Bottom - Tolerance));
        Assert.True(wraps, $"The picked row's {parts.Length} parts sit on one line, but the row was expected to wrap.");
        return that;
    }

    /// <summary>The row fits inside the screen, and the page doesn't scroll sideways.</summary>
    public static AssertingThat<TriggerRowLayout> FitsInsideTheScreen(this AssertingThat<TriggerRowLayout> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.Row.Left >= -Tolerance && shown.Row.Right <= shown.ScreenWidth + Tolerance,
            $"The picked row spans {shown.Row.Left:0}–{shown.Row.Right:0} px on a screen {shown.ScreenWidth:0} px wide, but it was expected inside it.");
        Assert.True(
            shown.PageWidth <= shown.ScreenWidth + Tolerance,
            $"The page is {shown.PageWidth:0} px wide on a screen {shown.ScreenWidth:0} px wide (it scrolls sideways), but it was expected to fit.");
        return that;
    }

    private static bool Overlap(ScreenBox first, ScreenBox second) =>
        Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left) > Tolerance
        && Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top) > Tolerance;
}
