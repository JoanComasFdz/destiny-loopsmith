using System.Collections.Immutable;

namespace Loopsmith.Core.LoopGraphing;

/// <summary>Elementary-cycle search over Rule + Player edges (Enables edges are prerequisites, not steps).</summary>
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

        var found = new List<ImmutableArray<GraphEdge>>();
        foreach (var start in graph.Nodes.Select(n => n.Key))
        {
            SearchCycles(start, start, order[start], [], [start], outgoing, order, found);
            if (found.Count >= MaxLoops)
            {
                break;
            }
        }

        return found
            .Select(edges => RotateToAction(edges, actionKeys))
            .Select(edges => new Loop(
                edges.Select(e => e.From).ToImmutableArray(),
                edges,
                edges.Any(e => energyKeys.Contains(e.From))))
            .OrderByDescending(loop => loop.RefundsEnergy)
            .ThenBy(loop => loop.Edges.Length)
            .ThenBy(loop => string.Join(">", loop.NodeKeys), StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Start a loop where the player acts ("Throw grenade → …"), so it reads like a rotation.</summary>
    private static ImmutableArray<GraphEdge> RotateToAction(ImmutableArray<GraphEdge> edges, ImmutableHashSet<string> actionKeys)
    {
        var start = edges.Select((edge, index) => (edge, index)).FirstOrDefault(x => actionKeys.Contains(x.edge.From)).index;
        return [.. edges.Skip(start), .. edges.Take(start)];
    }

    private static void SearchCycles(
        string start,
        string current,
        int startOrder,
        ImmutableArray<GraphEdge> path,
        ImmutableHashSet<string> visited,
        ImmutableDictionary<string, ImmutableArray<GraphEdge>> outgoing,
        ImmutableDictionary<string, int> order,
        List<ImmutableArray<GraphEdge>> found)
    {
        if (found.Count >= MaxLoops || path.Length >= MaxLoopLength)
        {
            return;
        }

        foreach (var edge in outgoing.GetValueOrDefault(current, []))
        {
            if (edge.To == start)
            {
                found.Add(path.Add(edge));
            }
            else if (!visited.Contains(edge.To) && order.GetValueOrDefault(edge.To) > startOrder)
            {
                SearchCycles(start, edge.To, startOrder, path.Add(edge), visited.Add(edge.To), outgoing, order, found);
            }
        }
    }
}
