using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.DependencyGraphing;

/// <summary>The layers reached so far: each element's (the fewest steps to fire it) and each needed node's.</summary>
internal sealed record Layers(ImmutableDictionary<string, int> Elements, ImmutableDictionary<string, int> Nodes);

/// <summary>
/// Draws the dependency graph of a class's subclass from the rules: which element needs what before it can fire, and what
/// it gives. Pure: same rules ⇒ same graph. Three scopes: the subclass alone, with what it needs from outside (the
/// weapons), and with what builds on it (every outside element that needs one of its nodes). Each scope places its nodes
/// in columns, left to right in the order to test them.
/// </summary>
public static class DependencyGraphDrawing
{
    public static DependencyGraph DrawGraph(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass)
    {
        var context = new GraphContext(catalog, guardianClass, subclass);
        var drawn = catalog.Elements.Values
            .Where(element => DependencyMapping.IsDrawn(context, element))
            .OrderBy(element => element.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var links = drawn.ToImmutableDictionary(element => element.Id.Value, element => DependencyMapping.MapElement(context, element));
        var nodes = CollectNodes(links);
        var edges = links.Values.SelectMany(link => link.Edges).Distinct().ToImmutableArray();
        var subclassParts = drawn.Where(element => DependencyMapping.IsSubclassPart(context, element)).Select(element => element.Id.Value).ToImmutableHashSet();
        var members = ListScopeMembers(links, nodes, edges, subclassParts);
        var scopes = members.Select(member => DrawScope(context, member.Kind, member.Nodes, links, nodes, edges)).ToImmutableArray();
        var shown = members.SelectMany(member => member.Nodes).ToImmutableHashSet();
        return new DependencyGraph(
            $"{subclass} {guardianClass}",
            nodes.Where(node => shown.Contains(node.Key)).ToImmutableSortedDictionary(StringComparer.Ordinal),
            scopes);
    }

    /// <summary>Every node: the elements', then each other node an element touches (a keyword element is its status's node).</summary>
    private static ImmutableDictionary<string, GraphNode> CollectNodes(ImmutableDictionary<string, ElementLinks> links)
    {
        var elements = links.ToImmutableDictionary(link => link.Key, link => link.Value.Node);
        var touched = links.Values.SelectMany(link => link.Touched).Where(node => !elements.ContainsKey(node.Id)).DistinctBy(node => node.Id);
        return elements.AddRange(touched.Select(node => KeyValuePair.Create(node.Id, node)));
    }

    /// <summary>
    /// The nodes of each scope. Subclass: its elements, the keywords they touch, and the statuses, pickups, strikes and
    /// triggers either touches (no weapon). With needs: those and the weapons their elements need. With dependents: those,
    /// every other element that needs one of them, and the nodes those elements need or give.
    /// </summary>
    private static ImmutableArray<(ScopeKind Kind, ImmutableHashSet<string> Nodes)> ListScopeMembers(
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableArray<GraphEdge> edges,
        ImmutableHashSet<string> subclassParts)
    {
        var pairs = edges.SelectMany(edge => new[] { (Near: edge.From, Far: edge.To), (Near: edge.To, Far: edge.From) }).ToImmutableArray();
        var keywords = pairs
            .Where(pair => subclassParts.Contains(pair.Near) && links.TryGetValue(pair.Far, out var far) && far.IsKeyword)
            .Select(pair => pair.Far);
        var core = subclassParts.Union(keywords);
        var subclass = core.Union(pairs
            .Where(pair => core.Contains(pair.Near) && !links.ContainsKey(pair.Far) && !IsWeapon(nodes, pair.Far))
            .Select(pair => pair.Far));
        var withNeeds = subclass.Union(edges.Where(edge => IsWeapon(nodes, edge.From) && subclass.Contains(edge.To)).Select(edge => edge.From));
        var dependents = edges
            .Where(edge => edge.Kind == EdgeKind.Needs && withNeeds.Contains(edge.From) && links.ContainsKey(edge.To) && !withNeeds.Contains(edge.To))
            .Select(edge => edge.To)
            .ToImmutableHashSet();
        var withDependents = withNeeds.Union(dependents).Union(pairs
            .Where(pair => dependents.Contains(pair.Near) && !links.ContainsKey(pair.Far))
            .Select(pair => pair.Far));
        return [(ScopeKind.Subclass, subclass), (ScopeKind.WithNeeds, withNeeds), (ScopeKind.WithDependents, withDependents)];
    }

    private static GraphScope DrawScope(
        GraphContext context,
        ScopeKind kind,
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableArray<GraphEdge> edges) =>
        new(
            kind,
            NameScope(context, kind),
            [.. members.Order(StringComparer.Ordinal)],
            [.. edges
                .Where(edge => members.Contains(edge.From) && members.Contains(edge.To))
                .OrderBy(edge => edge.From, StringComparer.Ordinal)
                .ThenBy(edge => edge.To, StringComparer.Ordinal)
                .ThenBy(edge => edge.Kind)
                .ThenBy(edge => edge.Label, StringComparer.Ordinal)],
            PlaceColumns(members, links, nodes));

    private static string NameScope(GraphContext context, ScopeKind kind) =>
        kind switch
        {
            ScopeKind.Subclass => $"{context.Subclass} subclass",
            ScopeKind.WithNeeds => "+ what it needs from outside",
            _ => "+ what builds on it",
        };

    /// <summary>
    /// Each node's column: a trigger or weapon is column 0; an element of layer L is column 2L − 1 and a given node of
    /// layer L column 2L. A keyword that something else in the scope gives is placed as a given node, one nothing else
    /// gives as an element. None: it needs something outside this scope.
    /// </summary>
    private static ImmutableDictionary<string, Optional<int>> PlaceColumns(
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes)
    {
        var givers = ListGivers(members, links);
        var layers = ComputeLayers(members, links, nodes, givers);
        return members.ToImmutableDictionary(member => member, member => PlaceColumn(member, links, nodes, givers, layers));
    }

    private static Optional<int> PlaceColumn(
        string member,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableDictionary<string, ImmutableArray<string>> givers,
        Layers layers) =>
        IsTriggerOrWeapon(nodes, member)
            ? Optional.Some(0)
            : links.TryGetValue(member, out var link) && !(link.IsKeyword && HasOtherGivers(member, givers))
                ? FindLayer(layers.Elements, member).Map(layer => (2 * layer) - 1)
                : FindLayer(layers.Nodes, member).Map(layer => 2 * layer);

    private static bool HasOtherGivers(string member, ImmutableDictionary<string, ImmutableArray<string>> givers) =>
        givers.TryGetValue(member, out var found) && found.Any(giver => giver != member);

    /// <summary>The elements of the scope that give each node (a keyword that gives its own status among them).</summary>
    private static ImmutableDictionary<string, ImmutableArray<string>> ListGivers(
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links) =>
        links.Values
            .Where(link => members.Contains(link.Node.Id))
            .SelectMany(link => link.Gives.Where(members.Contains).Select(given => (Given: given, Giver: link.Node.Id)))
            .GroupBy(pair => pair.Given)
            .ToImmutableDictionary(group => group.Key, group => group.Select(pair => pair.Giver).ToImmutableArray());

    /// <summary>
    /// The layers, iterated from none to a fixed point: each step can only lower a layer, so a cycle (an element that needs
    /// what it gives) ends.
    /// </summary>
    private static Layers ComputeLayers(
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableDictionary<string, ImmutableArray<string>> givers)
    {
        var empty = new Layers(ImmutableDictionary<string, int>.Empty, ImmutableDictionary<string, int>.Empty);
        return IterateLayers(empty, members, links, nodes, givers);
    }

    private static Layers IterateLayers(
        Layers current,
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableDictionary<string, ImmutableArray<string>> givers)
    {
        var next = StepLayers(current, members, links, nodes, givers);
        return IsSame(current.Elements, next.Elements) && IsSame(current.Nodes, next.Nodes)
            ? current
            : IterateLayers(next, members, links, nodes, givers);
    }

    private static Layers StepLayers(
        Layers current,
        ImmutableHashSet<string> members,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableDictionary<string, ImmutableArray<string>> givers)
    {
        var elements = links.Values
            .Where(link => members.Contains(link.Node.Id))
            .SelectMany(link => ComputeElementLayer(link, current.Nodes).Match(
                layer => new[] { KeyValuePair.Create(link.Node.Id, layer.Value) },
                _ => []));
        var triggers = members.Where(member => IsTriggerOrWeapon(nodes, member)).Select(member => KeyValuePair.Create(member, 0));
        var given = givers.SelectMany(entry => FindMinimum(entry.Value.SelectMany(giver => FindLayer(current.Elements, giver).Match(
                layer => new[] { layer.Value },
                _ => []))).Match(
            layer => new[] { KeyValuePair.Create(entry.Key, layer.Value) },
            _ => []));
        return new Layers(elements.ToImmutableDictionary(), triggers.Concat(given).ToImmutableDictionary());
    }

    /// <summary>The fewest layers over the ways it fires: one more than the highest of what the way needs.</summary>
    private static Optional<int> ComputeElementLayer(ElementLinks link, ImmutableDictionary<string, int> nodes) =>
        FindMinimum(link.Ways.SelectMany(way => ComputeWayLayer(way, nodes).Match(layer => new[] { layer.Value }, _ => [])));

    private static Optional<int> ComputeWayLayer(FiringWay way, ImmutableDictionary<string, int> nodes)
    {
        var all = way.AllOf.Select(needed => FindLayer(nodes, needed)).ToImmutableArray();
        var any = way.AnyOf.Select(group => FindMinimum(group.SelectMany(needed => FindLayer(nodes, needed).Match(layer => new[] { layer.Value }, _ => []))))
            .ToImmutableArray();
        var needed = all.Concat(any).ToImmutableArray();
        return needed.All(layer => layer.IsSome())
            ? Optional.Some(1 + needed.SelectMany(layer => layer.Match(found => new[] { found.Value }, _ => [])).DefaultIfEmpty(0).Max())
            : Optional.None<int>();
    }

    private static Optional<int> FindLayer(ImmutableDictionary<string, int> layers, string id) =>
        layers.TryGetValue(id, out var layer) ? Optional.Some(layer) : Optional.None<int>();

    private static Optional<int> FindMinimum(IEnumerable<int> layers)
    {
        var all = layers.ToImmutableArray();
        return all.IsEmpty ? Optional.None<int>() : Optional.Some(all.Min());
    }

    private static bool IsSame(ImmutableDictionary<string, int> left, ImmutableDictionary<string, int> right) =>
        left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out var other) && other == entry.Value);

    private static bool IsWeapon(ImmutableDictionary<string, GraphNode> nodes, string id) =>
        nodes.TryGetValue(id, out var node) && node.Type is GraphNodeType.NeededWeapon;

    private static bool IsTriggerOrWeapon(ImmutableDictionary<string, GraphNode> nodes, string id) =>
        nodes.TryGetValue(id, out var node) && node.Type is GraphNodeType.NeededWeapon or GraphNodeType.ActionTrigger;
}
