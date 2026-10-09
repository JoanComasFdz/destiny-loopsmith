using System.Collections.Immutable;
using System.Text;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.LoopGraphing;

public static class LoopRendering
{
    /// <summary>Terminal view: each loop as a chain "A →[source] B →[source] C ↺".</summary>
    public static ImmutableArray<StyledLine> RenderLoops(LoopGraph graph, ImmutableArray<Loop> loops, int limit)
    {
        var nodes = graph.Nodes.ToImmutableDictionary(n => n.Key);
        var shown = loops.Take(limit).ToImmutableArray();
        var header = StyledText.ToLine(0,
            $"{loops.Length} loop{(loops.Length == 1 ? "" : "s")} found".ToSpan(Tone.Strong),
            (loops.Length > shown.Length ? $" (showing {shown.Length}; --limit to change)" : "").ToSpan(Tone.Muted));
        var rows = shown.SelectMany((loop, index) => RenderLoop(nodes, loop, index + 1));
        return [header, .. rows];
    }

    private static IEnumerable<StyledLine> RenderLoop(ImmutableDictionary<string, GraphNode> nodes, Loop loop, int number)
    {
        var kind = loop.RefundsEnergy
            ? "ability loop — refunds " + string.Join(", ", loop.NodeKeys.Where(k => nodes[k].Kind == NodeKind.Energy).Select(k => nodes[k].Label.ToLowerInvariant()))
            : "buff loop";
        yield return StyledText.ToLine(0, $"Loop {number}".ToSpan(Tone.Strong), $" · {kind} · {loop.Edges.Length} steps".ToSpan(Tone.Muted));
        var spans = loop.Edges.SelectMany((edge, index) => new[]
            {
                index == 0 ? nodes[edge.From].Label.ToSpan(nodes[edge.From].Affinity.ToTone()) : null,
                edge.Kind == EdgeKind.Player ? " → ".ToSpan(Tone.Muted) : $" →[{string.Join(", ", edge.Sources)}] ".ToSpan(Tone.Muted),
                index == loop.Edges.Length - 1 ? "↺".ToSpan(Tone.Strong) : nodes[edge.To].Label.ToSpan(nodes[edge.To].Affinity.ToTone()),
            }
            .OfType<StyledSpan>());
        yield return new StyledLine(1, [.. spans]);
    }

    /// <summary>
    /// Mermaid flowchart (renders on GitHub and mermaid.live). Loop edges are thick; player edges dotted;
    /// "requires debuff" edges dashed. Colours follow the design proposal's element palette.
    /// </summary>
    public static string RenderMermaid(LoopGraph graph, ImmutableArray<Loop> loops, bool loopsOnly)
    {
        var loopEdges = loops.SelectMany(l => l.Edges).Select(e => (e.From, e.To)).ToImmutableHashSet();
        var loopNodes = loops.SelectMany(l => l.NodeKeys).ToImmutableHashSet();
        var nodes = graph.Nodes.Where(n => !loopsOnly || loopNodes.Contains(n.Key)).ToImmutableArray();
        var ids = nodes.Select((n, i) => (n.Key, Id: $"n{i}")).ToImmutableDictionary(x => x.Key, x => x.Id);
        var edges = graph.Edges
            .Where(e => ids.ContainsKey(e.From) && ids.ContainsKey(e.To))
            .Where(e => !loopsOnly || loopEdges.Contains((e.From, e.To)))
            .ToImmutableArray();

        var text = new StringBuilder();
        text.AppendLine("flowchart LR");
        foreach (var (affinity, color) in Palette)
        {
            text.AppendLine($"  classDef {affinity.ToString().ToLowerInvariant()} fill:#111620,stroke:{color},color:#e7eaf0");
        }

        foreach (var node in nodes)
        {
            var label = Escape(node.Label);
            var shape = node.Kind switch
            {
                NodeKind.Energy => $"([\"{label}\"])",
                NodeKind.Action => $"[/\"{label}\"/]",
                _ => $"(\"{label}\")",
            };
            text.AppendLine($"  {ids[node.Key]}{shape}:::{node.Affinity.ToString().ToLowerInvariant()}");
        }

        var thick = new List<int>();
        foreach (var (edge, index) in edges.Select((e, i) => (e, i)))
        {
            var isLoop = loopEdges.Contains((edge.From, edge.To));
            var label = edge.Kind == EdgeKind.Player ? "" : $"|\"{Escape(string.Join(" · ", edge.Sources))}\"|";
            var arrow = (edge.Kind, isLoop) switch
            {
                (EdgeKind.Enables, _) => "-.->",
                (EdgeKind.Player, true) => "==>",
                (EdgeKind.Player, false) => "-.->",
                (_, true) => "==>",
                _ => "-->",
            };
            text.AppendLine($"  {ids[edge.From]} {arrow}{label} {ids[edge.To]}");
            if (isLoop && edge.Kind != EdgeKind.Enables)
            {
                thick.Add(index);
            }
        }

        if (thick.Count > 0)
        {
            text.AppendLine($"  linkStyle {string.Join(",", thick)} stroke:#ff5fa2,stroke-width:3px");
        }

        return text.ToString();
    }

    private static readonly (Affinity Affinity, string Color)[] Palette =
    [
        (Affinity.Neutral, "#8a94a8"), (Affinity.Kinetic, "#e7eaf0"), (Affinity.Arc, "#6fe3ff"), (Affinity.Solar, "#ff9a3c"),
        (Affinity.Void, "#b07cff"), (Affinity.Stasis, "#5aa0ff"), (Affinity.Strand, "#4ee28a"), (Affinity.Prismatic, "#ff5fa2"),
    ];

    private static string Escape(string text) => text.Replace("\"", "#quot;");
}
