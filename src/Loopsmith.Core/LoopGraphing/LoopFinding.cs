using System.Collections.Immutable;

namespace Loopsmith.Core.LoopGraphing;

/// <summary>
/// Elementary-cycle search over Rule, Player and Declared edges — a max you declare is a link like an action you take
/// (ADRs D3); Enables edges are prerequisites, not steps.
/// </summary>
public static class LoopFinding
{
    public const int MaxLoopLength = 8;
    public const int MaxLoops = 200;

    public static ImmutableArray<Loop> FindLoops(LoopGraph graph)
    {
        var order = graph.Nodes.Select((node, index) => (node.Key, index)).ToImmutableDictionary(x => x.Key, x => x.index);
        var stepEdges = graph.Edges.Where(e => e.Kind != EdgeKind.Enables).ToImmutableArray();
        var outgoing = stepEdges
            .GroupBy(e => e.From)
            .ToImmutableDictionary(g => g.Key, g => g.OrderBy(e => order.GetValueOrDefault(e.To)).ToImmutableArray());
        var energyKeys = graph.Nodes.Where(n => n.Kind == NodeKind.Energy).Select(n => n.Key).ToImmutableHashSet();
        var actionKeys = graph.Nodes.Where(n => n.Kind == NodeKind.Action).Select(n => n.Key).ToImmutableHashSet();
        var filterKeys = graph.Nodes.Where(n => n.Kind == NodeKind.Filter).Select(n => n.Key).ToImmutableHashSet();

        var found = graph.Nodes
            .Select(n => n.Key)
            .Aggregate(ImmutableArray<ImmutableArray<GraphEdge>>.Empty, (acc, start) =>
                acc.Length >= MaxLoops ? acc : acc.AddRange(SearchCycles(start, start, order[start], [], [start], outgoing, order, filterKeys, MaxLoops - acc.Length)));

        return found
            .Select(edges => RotateToAction(edges, actionKeys))
            .Select(edges => new Loop(
                edges.Select(e => e.From).ToImmutableArray(),
                edges,
                edges.Any(e => energyKeys.Contains(e.From))))
            .OrderByDescending(loop => loop.GivesEnergyBack)
            .ThenBy(loop => loop.Edges.Count(edge => !filterKeys.Contains(edge.To)))   // steps: a "doesn't stack" node is not one
            .ThenBy(loop => string.Join(">", loop.NodeKeys), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Start a loop where the player acts ("Throw grenade → …"), so it reads like a rotation.</summary>
    private static ImmutableArray<GraphEdge> RotateToAction(ImmutableArray<GraphEdge> edges, ImmutableHashSet<string> actionKeys)
    {
        var start = edges.Select((edge, index) => (edge, index)).FirstOrDefault(x => actionKeys.Contains(x.edge.From)).index;
        return [.. edges.Skip(start), .. edges.Take(start)];
    }

    /// <summary>
    /// Cycles through <paramref name="start"/> whose other nodes all come later in node order (each cycle once), up to
    /// <see cref="MaxLoopLength"/> steps; passing a "doesn't stack" node is not a step.
    /// </summary>
    private static ImmutableArray<ImmutableArray<GraphEdge>> SearchCycles(
        string start,
        string current,
        int startOrder,
        ImmutableArray<GraphEdge> path,
        ImmutableHashSet<string> visited,
        ImmutableDictionary<string, ImmutableArray<GraphEdge>> outgoing,
        ImmutableDictionary<string, int> order,
        ImmutableHashSet<string> filterKeys,
        int budget)
    {
        if (budget <= 0 || path.Count(edge => !filterKeys.Contains(edge.To)) >= MaxLoopLength)
        {
            return [];
        }

        return outgoing.GetValueOrDefault(current, []).Aggregate(ImmutableArray<ImmutableArray<GraphEdge>>.Empty, (acc, edge) =>
        {
            var remaining = budget - acc.Length;
            if (remaining <= 0)
            {
                return acc;
            }

            if (edge.To == start)
            {
                return acc.Add(path.Add(edge));
            }

            return !visited.Contains(edge.To) && order.GetValueOrDefault(edge.To) > startOrder
                ? acc.AddRange(SearchCycles(start, edge.To, startOrder, path.Add(edge), visited.Add(edge.To), outgoing, order, filterKeys, remaining))
                : acc;
        });
    }
}
