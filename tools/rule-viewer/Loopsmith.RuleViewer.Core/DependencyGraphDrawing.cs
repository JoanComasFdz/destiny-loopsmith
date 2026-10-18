using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Draws the dependency graph of a class's subclass from the rules: which element needs what before it can fire, and what
/// it gives. Pure: same rules ⇒ same graph. Three scopes: the subclass alone, with what it needs from outside (the
/// weapons), and with what builds on it (every outside element that needs one of its nodes). The flows take each
/// scope's elements (<see cref="RuleFlowDrawing"/>).
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
        var links = drawn.Select(element => DependencyMapping.MapElement(context, element)).ToImmutableDictionary(link => link.Node.Id);
        var nodes = CollectNodes(links);
        var edges = links.Values.SelectMany(link => link.Edges).Distinct().ToImmutableArray();
        var subclassParts = drawn.Where(element => DependencyMapping.IsSubclassPart(context, element)).Select(element => element.Id.Value).ToImmutableHashSet();
        var members = ListScopeMembers(links, nodes, edges, subclassParts);
        var scopes = members.Select(member => DrawScope(context, member.Kind, member.Nodes, edges)).ToImmutableArray();
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
    /// The nodes of each scope. Subclass: its elements, the rules of the keywords whose states they touch, and the nodes
    /// either touches but weapons. With needs: those and the weapons their elements need. With dependents: those, every
    /// other element that needs one of them (and the rules of the keywords whose states it touches), and their nodes.
    /// </summary>
    private static ImmutableArray<(ScopeKind Kind, ImmutableHashSet<string> Nodes)> ListScopeMembers(
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableDictionary<string, GraphNode> nodes,
        ImmutableArray<GraphEdge> edges,
        ImmutableHashSet<string> subclassParts)
    {
        var pairs = edges.SelectMany(edge => new[] { (Near: edge.From, Far: edge.To), (Near: edge.To, Far: edge.From) }).ToImmutableArray();
        var core = AddKeywordRules(subclassParts, links, pairs);
        var subclass = core.Union(ListTouched(core, links, pairs).Where(id => !IsWeapon(nodes, id)));
        var withNeeds = subclass.Union(edges.Where(edge => IsWeapon(nodes, edge.From) && subclass.Contains(edge.To)).Select(edge => edge.From));
        var dependents = edges
            .Where(edge => edge.Kind == EdgeKind.Needs && withNeeds.Contains(edge.From) && links.ContainsKey(edge.To) && !withNeeds.Contains(edge.To))
            .Select(edge => edge.To)
            .ToImmutableHashSet();
        var added = AddKeywordRules(dependents, links, pairs).Except(withNeeds);
        var withDependents = withNeeds.Union(added).Union(ListTouched(added, links, pairs));
        return [(ScopeKind.Subclass, subclass), (ScopeKind.WithNeeds, withNeeds), (ScopeKind.WithDependents, withDependents)];
    }

    /// <summary>The elements and the rule nodes of the keywords whose states they touch (Arc Staff gives Bolt Charge: rule:bolt-charge).</summary>
    private static ImmutableHashSet<string> AddKeywordRules(
        ImmutableHashSet<string> elements,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableArray<(string Near, string Far)> pairs)
    {
        var touched = ListTouched(elements, links, pairs).ToImmutableHashSet();
        return elements.Union(links.Values
            .Where(link => link.StateId.Match(state => touched.Contains(state.Value), _ => false))
            .Select(link => link.Node.Id));
    }

    /// <summary>The nodes that aren't elements next to these elements.</summary>
    private static IEnumerable<string> ListTouched(
        ImmutableHashSet<string> elements,
        ImmutableDictionary<string, ElementLinks> links,
        ImmutableArray<(string Near, string Far)> pairs) =>
        pairs.Where(pair => elements.Contains(pair.Near) && !links.ContainsKey(pair.Far)).Select(pair => pair.Far);

    private static GraphScope DrawScope(
        GraphContext context,
        ScopeKind kind,
        ImmutableHashSet<string> members,
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
                .ThenBy(edge => edge.Label, StringComparer.Ordinal)]);

    private static string NameScope(GraphContext context, ScopeKind kind) =>
        kind switch
        {
            ScopeKind.Subclass => $"{context.Subclass} subclass",
            ScopeKind.WithNeeds => "+ what it needs from outside",
            _ => "+ what builds on it",
        };

    private static bool IsWeapon(ImmutableDictionary<string, GraphNode> nodes, string id) =>
        nodes.TryGetValue(id, out var node) && node.Type is GraphNodeType.NeededWeapon;
}
