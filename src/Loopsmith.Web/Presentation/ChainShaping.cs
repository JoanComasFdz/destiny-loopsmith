using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>A point of the drawn chain, in pixels.</summary>
public sealed record ChainPoint(int X, int Y);

/// <summary>The left edge of a step's card: a notch cut in at the top (ingress) and a square slot cut in at the bottom (egress).</summary>
public sealed record ChainCard(int Step, string Edge);

/// <summary>
/// One link drawn: from inside the source card's slot, left along the source's first stroke, down its own lane, right
/// into the target card's notch. <see cref="Junction"/> marks where it splits off the source's stroke (none for the outermost).
/// </summary>
public sealed record ChainLine(StepLink Link, int Lane, string Path, Optional<ChainPoint> Junction, bool OnlyHere, string Title);

/// <summary>The arrowhead in a fed step's notch: every line into that step ends, merged, in this one arrowhead.</summary>
public sealed record ChainArrowhead(int Step, string Points);

/// <summary>A step chain laid out: one card per step, the lines in lanes of a gutter to the left of the cards.</summary>
public sealed record ChainLayout(
    int RowPitch,
    int CardHeight,
    int CardLeft,
    int Width,
    int Height,
    ImmutableArray<ChainCard> Cards,
    ImmutableArray<ChainLine> Lines,
    ImmutableArray<ChainArrowhead> Arrowheads);

/// <summary>
/// Pure: a report's links → where to draw them. Each step is a card whose left edge has a notch at the top, shaped like
/// the arrow that lands there, and a square slot at the bottom, shaped like the line that leaves: its lines start inside
/// the slot. A step's lines leave as one stroke and split along it, one lane each: the nearest target splits off first,
/// nearest the cards. Lines into the same
/// step merge on its notch. Lines never share a lane where they overlap. Only links within a pass are drawn: each line
/// goes down, from a step to a later one.
/// </summary>
public static class ChainShaping
{
    public const int RowPitch = 56;

    public const int CardHeight = 46;

    public const int CardMargin = 5;

    private const int IngressOffset = 11;

    private const int EgressOffset = 35;

    /// <summary>How far the notch and the slot cut into the card.</summary>
    private const int Depth = 10;

    /// <summary>Half the height of the notch.</summary>
    private const int Half = 6;

    /// <summary>Half the height of the slot: a little wider than a line.</summary>
    private const int SlotHalf = 5;

    private const int CardRadius = 5;

    private const int LaneWidth = 9;

    private const int Inset = 7;

    private const int Margin = 4;

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
        var lanes = AssignLanes(links);
        var laneCount = lanes.DefaultIfEmpty(-1).Max() + 1;
        var edge = Margin + Math.Max(laneCount - 1, 0) * LaneWidth + Inset;   // x of the cards' left edge
        var outermost = links
            .Select((compared, index) => (compared.Link.From, Lane: lanes[index]))
            .GroupBy(x => x.From)
            .ToImmutableDictionary(group => group.Key, group => group.Max(x => x.Lane));
        var lines = links
            .Select((compared, index) => ShapeLine(compared, lanes[index], lanes[index] == outermost[compared.Link.From], edge))
            .ToImmutableArray();
        var arrowheads = links
            .Select(compared => compared.Link.To)
            .Distinct()
            .Order()
            .Select(step => new ChainArrowhead(step, ShapeArrowhead(edge, ReadIngress(step))))
            .ToImmutableArray();
        var cards = Enumerable.Range(0, steps).Select(step => new ChainCard(step, ShapeCardEdge(edge, step))).ToImmutableArray();
        return new ChainLayout(RowPitch, CardHeight, edge + Depth + 1, edge + Depth + 2, Math.Max(steps, 1) * RowPitch, cards, lines, arrowheads);
    }

    private static int ReadCardTop(int step) => step * RowPitch + CardMargin;

    /// <summary>The y of a step's notch, where the lines into it land.</summary>
    public static int ReadIngress(int step) => ReadCardTop(step) + IngressOffset;

    /// <summary>The y of a step's slot, where the lines out of it leave.</summary>
    public static int ReadEgress(int step) => ReadCardTop(step) + EgressOffset;

    /// <summary>
    /// A lane per link, shortest first and nearest the cards (so of one step's lines the nearest target splits off first);
    /// two links share a lane only when their lines don't overlap.
    /// </summary>
    private static ImmutableArray<int> AssignLanes(ImmutableArray<ComparedLink> links)
    {
        var order = Enumerable.Range(0, links.Length)
            .OrderBy(index => links[index].Link.To - links[index].Link.From)
            .ThenBy(index => links[index].Link.From)
            .ThenBy(index => links[index].Link.To);
        var assigned = order.Aggregate(ImmutableDictionary<int, int>.Empty, (lanes, index) =>
            lanes.Add(index, Enumerable.Range(0, lanes.Count + 1).First(lane => !lanes
                .Where(other => other.Value == lane)
                .Any(other => Overlaps(links[other.Key].Link, links[index].Link)))));
        return [.. Enumerable.Range(0, links.Length).Select(index => assigned[index])];
    }

    private static bool Overlaps(StepLink a, StepLink b) =>
        ReadEgress(a.From) < ReadIngress(b.To) && ReadEgress(b.From) < ReadIngress(a.To);

    private static int ReadLaneX(int edge, int lane) => edge - Inset - lane * LaneWidth;

    private static ChainLine ShapeLine(ComparedLink compared, int lane, bool isOutermost, int edge)
    {
        var x = ReadLaneX(edge, lane);
        var egress = ReadEgress(compared.Link.From);
        var ingress = ReadIngress(compared.Link.To);
        var path = string.Create(Invariant,
            $"M {edge + Depth} {egress} H {x + Radius} Q {x} {egress} {x} {egress + Radius} V {ingress - Radius} Q {x} {ingress} {x + Radius} {ingress} H {edge + Depth - 1 - ArrowLength}");
        var junction = isOutermost ? Optional.None<ChainPoint>() : Optional.Some(new ChainPoint(x, egress));
        return new ChainLine(compared.Link, lane, path, junction, compared.OnlyHere, compared.Link.DescribeLink());
    }

    private static string ShapeArrowhead(int edge, int ingress)
    {
        var tip = edge + Depth - 1;
        return string.Create(Invariant, $"{tip - ArrowLength},{ingress - 4} {tip},{ingress} {tip - ArrowLength},{ingress + 4}");
    }

    /// <summary>
    /// The left part of a card, up to where the card itself starts: rounded corners, the notch cut in at the ingress, the
    /// square slot cut in at the egress. Filled like the card, so the two read as one.
    /// </summary>
    private static string ShapeCardEdge(int edge, int step)
    {
        var top = ReadCardTop(step);
        var bottom = top + CardHeight;
        var ingress = ReadIngress(step);
        var egress = ReadEgress(step);
        var right = edge + Depth + 2;
        return string.Create(Invariant,
            $"M {edge + CardRadius} {top} H {right} V {bottom} H {edge + CardRadius} Q {edge} {bottom} {edge} {bottom - CardRadius} "
            + $"V {egress + SlotHalf} H {edge + Depth} V {egress - SlotHalf} H {edge} "
            + $"V {ingress + Half} L {edge + Depth} {ingress} L {edge} {ingress - Half} "
            + $"V {top + CardRadius} Q {edge} {top} {edge + CardRadius} {top} Z");
    }
}
