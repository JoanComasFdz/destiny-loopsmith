using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.ReportComparison;

/// <summary>A <see cref="LoopComparison"/> as a two-column table; the better value of each row is marked ✓.</summary>
public static class ComparisonRendering
{
    private const string BetterMark = " ✓";
    private const int Gap = 3;

    public static ImmutableArray<StyledLine> RenderComparison(LoopComparison comparison)
    {
        var left = comparison.Left;
        var right = comparison.Right;
        var metricWidth = comparison.Rows.Select(row => row.Metric.Length).Append("Build".Length).Max() + Gap;
        var leftWidth = comparison.Rows.Select(row => row.Left.Length + BetterMark.Length)
            .Append(left.LoopName.Length)
            .Append(left.BuildName.Length)
            .Max() + Gap;
        var heading = StyledText.ToLine(0,
            left.LoopName.ToSpan(Tone.Strong), "  vs  ".ToSpan(Tone.Muted), right.LoopName.ToSpan(Tone.Strong));
        var names = StyledText.ToLine(1,
            Pad("", metricWidth).ToSpan(),
            Pad(left.LoopName, leftWidth).ToSpan(Tone.Strong),
            right.LoopName.ToSpan(Tone.Strong));
        var builds = StyledText.ToLine(1,
            Pad("Build", metricWidth).ToSpan(Tone.Muted),
            Pad(left.BuildName, leftWidth).ToSpan(Tone.Muted),
            right.BuildName.ToSpan(Tone.Muted));
        var rows = comparison.Rows.Select(row => RenderRow(row, metricWidth, leftWidth));
        return [heading, names, builds, .. rows, RenderTally(comparison)];
    }

    private static StyledLine RenderRow(ComparisonRow row, int metricWidth, int leftWidth)
    {
        var leftText = row.Better == Advantage.Left ? row.Left + BetterMark : row.Left;
        var rightText = row.Better == Advantage.Right ? row.Right + BetterMark : row.Right;
        return StyledText.ToLine(1,
            Pad(row.Metric, metricWidth).ToSpan(Tone.Muted),
            Pad(leftText, leftWidth).ToSpan(row.Better == Advantage.Left ? Tone.Strong : Tone.Plain),
            rightText.ToSpan(row.Better == Advantage.Right ? Tone.Strong : Tone.Plain));
    }

    private static StyledLine RenderTally(LoopComparison comparison)
    {
        var leftWins = comparison.Rows.Count(row => row.Better == Advantage.Left);
        var rightWins = comparison.Rows.Count(row => row.Better == Advantage.Right);
        var even = comparison.Rows.Length - leftWins - rightWins;
        return StyledText.ToLine(0,
            $"✓ better: {comparison.Left.LoopName} on {DescribeCount(leftWins)} · {comparison.Right.LoopName} on {DescribeCount(rightWins)} · {even} even or not judged"
                .ToSpan(Tone.Muted));
    }

    private static string DescribeCount(int metrics) => metrics == 1 ? "1 metric" : $"{metrics} metrics";

    private static string Pad(string text, int width) => text.PadRight(width);
}
