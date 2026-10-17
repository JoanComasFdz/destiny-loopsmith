using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.DependencyGraphing;

/// <summary>
/// The dependency graph as text (the golden test's: per scope, each column's nodes, those with no column, the edges) and
/// as the JSON the graph page reads. Pure.
/// </summary>
public static class GraphRendering
{
    /// <summary>Where the page template takes the graph's JSON.</summary>
    public const string DataPlaceholder = "__DATA__";

    public static string RenderText(DependencyGraph graph) =>
        string.Join('\n', new[] { $"# {graph.Title}" }.Concat(graph.Scopes.SelectMany(scope => RenderScopeLines(graph, scope)))) + "\n";

    private static IEnumerable<string> RenderScopeLines(DependencyGraph graph, GraphScope scope)
    {
        var placed = scope.Nodes
            .Select(id => (Node: graph.Nodes[id], Column: scope.Columns[id]))
            .ToImmutableArray();
        var columns = placed
            .SelectMany(entry => entry.Column.Match(column => new[] { (entry.Node, Column: column.Value) }, _ => []))
            .GroupBy(entry => entry.Column)
            .OrderBy(group => group.Key)
            .SelectMany(group => new[] { $"column {group.Key.ToString(CultureInfo.InvariantCulture)}" }
                .Concat(group.Select(entry => $"  {DescribeNode(entry.Node)}").Order(StringComparer.Ordinal)));
        var unplaced = placed.Where(entry => !entry.Column.IsSome()).Select(entry => $"  {DescribeNode(entry.Node)}").Order(StringComparer.Ordinal).ToImmutableArray();
        var edges = scope.Edges.Select(edge => $"  {edge.From} -[{DescribeEdge(edge)}]-> {edge.To}").Order(StringComparer.Ordinal);
        return new[] { "", $"## {scope.Name} ({ToScopeId(scope.Kind)}): {scope.Nodes.Length.ToString(CultureInfo.InvariantCulture)} nodes, {scope.Edges.Length.ToString(CultureInfo.InvariantCulture)} edges" }
            .Concat(columns)
            .Concat(unplaced.IsEmpty ? [] : new[] { "no column" }.Concat(unplaced))
            .Concat(new[] { "edges" })
            .Concat(edges);
    }

    private static string DescribeNode(GraphNode node) => $"{ToTypeName(node.Type)} {node.Name}";

    private static string DescribeEdge(GraphEdge edge) =>
        edge.Label.Length == 0 ? ToKindName(edge.Kind) : $"{ToKindName(edge.Kind)} {edge.Label}";

    /// <summary>
    /// <c>{"title", "nodes": {id: {id, name, type, slot, keyword, file, source, description, reasons}}, "scopes": [{id, name,
    /// nodes, edges: [{from, to, kind, label}], columns: {id: column}}]}</c>; a node with no column is left out of
    /// <c>columns</c>.
    /// </summary>
    public static string RenderJson(DependencyGraph graph) =>
        new JsonObject
        {
            ["title"] = graph.Title,
            ["nodes"] = new JsonObject(graph.Nodes.Select(node => KeyValuePair.Create(node.Key, (JsonNode?)ToNodeObject(node.Value)))),
            ["scopes"] = new JsonArray([.. graph.Scopes.Select(ToScopeObject)]),
        }.ToJsonString();

    /// <summary>The graph page: the template with the graph's JSON where <see cref="DataPlaceholder"/> is.</summary>
    public static string RenderPage(string template, DependencyGraph graph) =>
        template.Replace(DataPlaceholder, RenderJson(graph), StringComparison.Ordinal);

    private static JsonObject ToNodeObject(GraphNode node) =>
        new()
        {
            ["id"] = node.Id,
            ["name"] = node.Name,
            ["type"] = ToTypeName(node.Type),
            ["slot"] = node.Type is GraphNodeType.SubclassAbility ability ? ToCamelCase(ability.Slot.ToString()) : null,
            ["keyword"] = node.IsKeyword,
            ["file"] = node.Element.Match<string?>(facts => facts.Value.File, _ => null),
            ["source"] = node.Element.Bind(facts => facts.Source).Match<string?>(source => source.Value, _ => null),
            ["description"] = node.Element.Bind(facts => facts.Description).Match<string?>(description => description.Value, _ => null),
            ["reasons"] = new JsonArray([.. node.Element.Match(facts => facts.Value.Reasons, _ => []).Select(reason => (JsonNode?)reason)]),
        };

    private static JsonObject ToScopeObject(GraphScope scope) =>
        new()
        {
            ["id"] = ToScopeId(scope.Kind),
            ["name"] = scope.Name,
            ["nodes"] = new JsonArray([.. scope.Nodes.Select(id => (JsonNode?)id)]),
            ["edges"] = new JsonArray([.. scope.Edges.Select(ToEdgeObject)]),
            ["columns"] = new JsonObject(scope.Nodes.SelectMany(id => scope.Columns[id].Match(
                column => new[] { KeyValuePair.Create(id, (JsonNode?)column.Value) },
                _ => []))),
        };

    private static JsonNode? ToEdgeObject(GraphEdge edge) =>
        new JsonObject
        {
            ["from"] = edge.From,
            ["to"] = edge.To,
            ["kind"] = ToKindName(edge.Kind),
            ["label"] = edge.Label,
        };

    private static string ToScopeId(ScopeKind kind) => ToCamelCase(kind.ToString());

    private static string ToKindName(EdgeKind kind) => ToCamelCase(kind.ToString());

    /// <summary>The node type as the page names it ("trigger", "subclassAbility", "armorMod").</summary>
    public static string ToTypeName(GraphNodeType type) =>
        type.Match(
            actionTrigger: _ => "trigger",
            neededWeapon: _ => "weapon",
            subclassAbility: _ => "subclassAbility",
            subclassSuper: _ => "subclassSuper",
            subclassAspect: _ => "subclassAspect",
            subclassFragment: _ => "subclassFragment",
            buffStatus: _ => "buff",
            debuffStatus: _ => "debuff",
            pickupDrop: _ => "pickup",
            summonedAlly: _ => "summon",
            unlistedKeyword: _ => "keyword",
            keywordStrike: _ => "strike",
            outsideElement: outside => ToCamelCase(outside.Kind.ToString()));

    private static string ToCamelCase(string name) =>
        string.Concat(name[..1].ToLowerInvariant(), name[1..]);
}
