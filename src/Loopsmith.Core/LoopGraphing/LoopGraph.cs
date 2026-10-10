using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.LoopGraphing;

/// <summary><see cref="Filter"/>: "doesn't stack" — the arrows of rules that don't stack meet there, and only one leaves.</summary>
public enum NodeKind { Trigger, Energy, Action, Filter }

public enum EdgeKind
{
    Rule,      // a build element turns the source into the target ("Shinobu's Vow")
    Player,    // you do it: energy → cast, cast → hit/kill
    Declared,  // you declare it: a stacking buff you gain is at its max when you say so (ADRs D3)
    Enables,   // a state the target needs (not part of loops): a debuff its trigger requires, the max a rule's guard reads
}

public sealed record GraphNode(string Key, string Label, NodeKind Kind, Affinity Affinity);

public sealed record GraphEdge(string From, string To, ImmutableArray<string> Sources, EdgeKind Kind);

public sealed record LoopGraph(ImmutableArray<GraphNode> Nodes, ImmutableArray<GraphEdge> Edges);

/// <summary>A cycle in the graph: following it brings you back to where you started; it may give ability energy back (ADRs D5).</summary>
public sealed record Loop(ImmutableArray<string> NodeKeys, ImmutableArray<GraphEdge> Edges, bool GivesEnergyBack);
