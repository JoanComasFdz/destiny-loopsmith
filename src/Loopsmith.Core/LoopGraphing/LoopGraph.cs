using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.LoopGraphing;

public enum NodeKind { Trigger, Energy, Action }

public enum EdgeKind
{
    Rule,      // a build element turns the source into the target ("Shinobu's Vow")
    Player,    // you do it: energy → cast, cast → hit/kill
    Enables,   // a debuff the target trigger requires (not part of loops)
}

public sealed record GraphNode(string Key, string Label, NodeKind Kind, Affinity Affinity);

public sealed record GraphEdge(string From, string To, ImmutableArray<string> Sources, EdgeKind Kind);

public sealed record LoopGraph(ImmutableArray<GraphNode> Nodes, ImmutableArray<GraphEdge> Edges);

/// <summary>A cycle in the graph: following it brings you back to where you started.</summary>
public sealed record Loop(ImmutableArray<string> NodeKeys, ImmutableArray<GraphEdge> Edges, bool RefundsEnergy);
