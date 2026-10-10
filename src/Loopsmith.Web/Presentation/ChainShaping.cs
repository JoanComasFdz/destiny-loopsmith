using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One arrow of the drawn chain: its link, the lane it runs in, its SVG path(s) and arrowhead, and whether the other
/// loop of a comparison lacks it.
/// </summary>
public sealed record ChainArc(StepLink Link, int Lane, ImmutableArray<string> Paths, string Arrowhead, bool OnlyHere, string Title);

/// <summary>A step chain laid out: one row per step, the arrows in lanes of a gutter to the left of the rows.</summary>
public sealed record ChainLayout(int RowHeight, int GutterWidth, int Height, ImmutableArray<ChainArc> Arcs);

/// <summary>
/// Pure: a report's links → where to draw them. A link within a pass runs down the gutter from the step that provided
/// the need to the step that needs it; a link from the pass before leaves its step downwards, off the bottom, and comes
/// back from the top into the step that needs it — around the loop. Lanes keep overlapping arrows apart, shortest
/// nearest the steps.
/// </summary>
public static class ChainShaping
{
    public const int RowHeight = 34;

    private const int LaneWidth = 11;

    private const int Inset = 12;

    private const int Radius = 5;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ChainLayout ShapeChain(int steps, ImmutableArray<ComparedLink> links)
    {
        var intervals = links.Select(link => ListIntervals(link.Link, steps)).ToImmutableArray();
        var order = Enumerable.Range(0, links.Length)
            .OrderBy(index => intervals[index].Sum(interval => interval.End - interval.Start))
            .ThenBy(index => links[index].Link.To)
            .ToImmutableArray();
        var lanes = order.Aggregate(ImmutableDictionary<int, int>.Empty, (assigned, index) =>
            assigned.Add(index, FindFreeLane(assigned, intervals, index)));
        var laneCount = lanes.Values.DefaultIfEmpty(-1).Max() + 1;
        var gutter = Inset + Math.Max(laneCount, 1) * LaneWidth + 6;
        var height = Math.Max(steps, 1) * RowHeight;
        var arcs = links.Select((link, index) => ShapeArc(link, lanes[index], gutter, height)).ToImmutableArray();
        return new ChainLayout(RowHeight, gutter, height, arcs);
    }

    public static ImmutableArray<ComparedLink> ListPlainLinks(ImmutableArray<StepLink> links) =>
        [.. links.Select(link => new ComparedLink(link, false))];

    /// <summary>The y of a step's row centre.</summary>
    public static int ReadRowCentre(int step) => step * RowHeight + RowHeight / 2;

    private sealed record Interval(double Start, double End);

    /// <summary>The rows an arrow spans; one from the pass before spans from its step to the bottom and from the top to its target.</summary>
    private static ImmutableArray<Interval> ListIntervals(StepLink link, int steps) =>
        link.FromPreviousPass
            ? [new Interval(link.From, steps), new Interval(-1, link.To)]
            : [new Interval(link.From, link.To)];

    private static int FindFreeLane(ImmutableDictionary<int, int> assigned, ImmutableArray<ImmutableArray<Interval>> intervals, int index) =>
        Enumerable.Range(0, assigned.Count + 1).First(lane => !assigned
            .Where(other => other.Value == lane)
            .Any(other => Overlaps(intervals[other.Key], intervals[index])));

    private static bool Overlaps(ImmutableArray<Interval> a, ImmutableArray<Interval> b) =>
        a.Any(x => b.Any(y => x.Start <= y.End && y.Start <= x.End));

    private static ChainArc ShapeArc(ComparedLink compared, int lane, int gutter, int height)
    {
        var link = compared.Link;
        var x = gutter - Inset - lane * LaneWidth;
        var end = gutter - 4;
        var from = ReadRowCentre(link.From);
        var to = ReadRowCentre(link.To);
        var paths = link.FromPreviousPass
            ? ImmutableArray.Create(
                $"M {end} {from} H {x + Radius} Q {x} {from} {x} {from + Radius} V {height}",
                $"M {x} 0 V {to - Radius} Q {x} {to} {x + Radius} {to} H {end - 5}")
            : ImmutableArray.Create($"M {end} {from} H {x + Radius} Q {x} {from} {x} {from + Radius} V {to - Radius} Q {x} {to} {x + Radius} {to} H {end - 5}");
        var arrowhead = string.Create(Invariant, $"{end - 6},{to - 4} {end},{to} {end - 6},{to + 4}");
        return new ChainArc(link, lane, paths, arrowhead, compared.OnlyHere, link.DescribeLink());
    }
}
