using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A point of the drawn chain, in pixels.</summary>
public sealed record ChainPoint(int X, int Y);

/// <summary>The part of a step's line that goes into one step it feeds; a branch off the trunk has a junction dot.</summary>
public sealed record ChainBranch(StepLink Link, string Path, Optional<ChainPoint> Junction, bool OnlyHere, string Title);

/// <summary>
/// The arrows out of one step: a trunk from the step's egress (bottom-left) down its own lane, and a branch into the
/// ingress (top-left) of each step it feeds.
/// </summary>
public sealed record ChainBus(int From, int Lane, ChainPoint Egress, string Trunk, ImmutableArray<ChainBranch> Branches, string Title);

/// <summary>An arrowhead on a fed step's ingress: every line into that step ends, merged, in this one arrowhead.</summary>
public sealed record ChainArrowhead(int Step, string Points);

/// <summary>A step chain laid out: one row per step, the lines in lanes of a gutter to the left of the rows.</summary>
public sealed record ChainLayout(
    int RowHeight,
    int GutterWidth,
    int Height,
    ImmutableArray<ChainBus> Buses,
    ImmutableArray<ChainArrowhead> Arrowheads);

/// <summary>
/// Pure: a report's links → where to draw them. Every step has two connectors on its left: the lines into it end at its
/// ingress (top), the line out of it starts at its egress (bottom). A step's line runs down its own lane and branches
/// into each step it feeds; lines into the same step merge on its ingress, as close to it as the lanes allow. Lines of
/// different steps never share a lane where they overlap; the shortest lie nearest the steps. Only links within a pass
/// are drawn: each arrow goes down, from a step to a later one.
/// </summary>
public static class ChainShaping
{
    public const int RowHeight = 34;

    private const int IngressOffset = 10;

    private const int EgressOffset = 24;

    private const int LaneWidth = 10;

    private const int Inset = 10;

    private const int Radius = 5;

    private const int ArrowLength = 6;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The links of one loop to draw: those within a pass.</summary>
    public static ImmutableArray<ComparedLink> ListDrawnLinks(ImmutableArray<StepLink> links) =>
        [.. links.Where(link => !link.FromPreviousPass).Select(link => new ComparedLink(link, false))];

    /// <summary>The links of a compared loop to draw: those within a pass that the other loop lacks.</summary>
    public static ImmutableArray<ComparedLink> ListDifferingLinks(ImmutableArray<ComparedLink> links) =>
        [.. links.Where(compared => compared.OnlyHere && !compared.Link.FromPreviousPass)];

    public static ChainLayout ShapeChain(int steps, ImmutableArray<ComparedLink> links)
    {
        var bySource = links
            .GroupBy(compared => compared.Link.From)
            .Select(group => (From: group.Key, Links: group.OrderBy(compared => compared.Link.To).ToImmutableArray()))
            .ToImmutableArray();
        var lanes = AssignLanes(bySource.Select(source => (source.From, Last: source.Links[^1].Link.To)).ToImmutableArray());
        var laneCount = lanes.Values.DefaultIfEmpty(-1).Max() + 1;
        var gutter = Inset + Math.Max(laneCount, 1) * LaneWidth + 4;
        var buses = bySource.Select(source => ShapeBus(source.From, source.Links, lanes[source.From], gutter)).ToImmutableArray();
        var arrowheads = links
            .Select(compared => compared.Link.To)
            .Distinct()
            .Order()
            .Select(step => new ChainArrowhead(step, ShapeArrowhead(gutter, ReadIngress(step))))
            .ToImmutableArray();
        return new ChainLayout(RowHeight, gutter, Math.Max(steps, 1) * RowHeight, buses, arrowheads);
    }

    /// <summary>The y of a step's ingress, where the lines into it end.</summary>
    public static int ReadIngress(int step) => step * RowHeight + IngressOffset;

    /// <summary>The y of a step's egress, where the line out of it starts.</summary>
    public static int ReadEgress(int step) => step * RowHeight + EgressOffset;

    /// <summary>
    /// A lane per step that feeds others, shortest span first and nearest the steps; two steps share a lane only when their
    /// lines don't overlap (one ends on an ingress above the other's egress).
    /// </summary>
    private static ImmutableDictionary<int, int> AssignLanes(ImmutableArray<(int From, int Last)> spans) =>
        spans
            .OrderBy(span => span.Last - span.From)
            .ThenBy(span => span.From)
            .Aggregate(ImmutableDictionary<int, int>.Empty, (assigned, span) =>
                assigned.Add(span.From, Enumerable.Range(0, assigned.Count + 1).First(lane => !assigned
                    .Where(other => other.Value == lane)
                    .Any(other => Overlaps(span, spans.First(candidate => candidate.From == other.Key))))));

    private static bool Overlaps((int From, int Last) a, (int From, int Last) b) =>
        ReadEgress(a.From) < ReadIngress(b.Last) && ReadEgress(b.From) < ReadIngress(a.Last);

    private static ChainBus ShapeBus(int from, ImmutableArray<ComparedLink> links, int lane, int gutter)
    {
        var x = gutter - Inset - lane * LaneWidth;
        var port = gutter - 1;
        var egress = ReadEgress(from);
        var last = ReadIngress(links[^1].Link.To);
        var trunk = string.Create(Invariant, $"M {port} {egress} H {x + Radius} Q {x} {egress} {x} {egress + Radius} V {last - Radius}");
        var branches = links.Select((compared, index) => index == links.Length - 1
            ? ShapeLastBranch(compared, x, port, last)
            : ShapeBranch(compared, x, port, ReadIngress(compared.Link.To)));
        return new ChainBus(
            from, lane, new ChainPoint(port, egress), trunk, [.. branches], string.Join("\n", links.Select(compared => compared.Link.DescribeLink())));
    }

    /// <summary>A branch off the trunk into a step it passes on the way down, with a dot where it leaves the trunk.</summary>
    private static ChainBranch ShapeBranch(ComparedLink compared, int x, int port, int ingress) =>
        new(
            compared.Link,
            string.Create(Invariant, $"M {x} {ingress} H {port - ArrowLength}"),
            Optional.Some(new ChainPoint(x, ingress)),
            compared.OnlyHere,
            compared.Link.DescribeLink());

    /// <summary>Where the trunk ends: it turns into the last step it feeds.</summary>
    private static ChainBranch ShapeLastBranch(ComparedLink compared, int x, int port, int ingress) =>
        new(
            compared.Link,
            string.Create(Invariant, $"M {x} {ingress - Radius} Q {x} {ingress} {x + Radius} {ingress} H {port - ArrowLength}"),
            Optional.None<ChainPoint>(),
            compared.OnlyHere,
            compared.Link.DescribeLink());

    private static string ShapeArrowhead(int gutter, int ingress) =>
        string.Create(Invariant, $"{gutter - 1 - ArrowLength},{ingress - 4} {gutter - 1},{ingress} {gutter - 1 - ArrowLength},{ingress + 4}");
}
