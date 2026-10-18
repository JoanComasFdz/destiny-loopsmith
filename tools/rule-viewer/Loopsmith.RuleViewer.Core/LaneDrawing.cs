using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Draws the lanes of a class's subclass: every rule and passive of the elements each scope of the dependency graph
/// holds, one lane per claim, grouped by what sets it off. Pure: same rules ⇒ same lanes. The lanes are the list of the
/// atomic scenarios the rules make: one lane, one claim, one scenario.
/// </summary>
public static class LaneDrawing
{
    /// <summary>The groups that come first, in the order a player reaches for them; the weapons come after the Super.</summary>
    private static readonly ImmutableArray<string> LeadingGroups =
        ["trigger:dodge", "trigger:air-dodge", "trigger:melee", "trigger:sliding-melee", "trigger:grenade", "trigger:finisher", "trigger:super"];

    private const string AnyWeaponGroup = "weapon:weapon";

    private const string CriticalHealthGroup = "trigger:critical-health";

    public static RuleLanes DrawLanes(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass)
    {
        var context = new GraphContext(catalog, guardianClass, subclass);
        var graph = DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);
        var lanes = catalog.Elements.Values
            .Where(element => DependencyMapping.IsDrawn(context, element))
            .SelectMany(element => LaneMapping.MapLanes(context, element))
            .ToImmutableArray();
        return new RuleLanes(
            graph.Title,
            [.. graph.Scopes.Select(scope => DrawScope(graph, scope, lanes))]);
    }

    private static LaneScope DrawScope(DependencyGraph graph, GraphScope scope, ImmutableArray<MappedLane> lanes)
    {
        var members = scope.Nodes.ToImmutableHashSet();
        var groups = lanes
            .Where(mapped => members.Contains(mapped.Lane.Element))
            .GroupBy(mapped => mapped.GroupKey)
            .Select(group => new LaneGroup(
                group.Key,
                group.First().GroupName,
                group.First().Lane.Trigger.Icon,
                [.. group
                    .Select(mapped => mapped.Lane)
                    .OrderBy(lane => RankOwner(graph.Nodes[lane.Element].Type))
                    .ThenBy(lane => graph.Nodes[lane.Element].Name, StringComparer.Ordinal)
                    .ThenBy(lane => lane.Id, StringComparer.Ordinal)]))
            .OrderBy(group => RankGroup(group.Key))
            .ThenBy(group => group.Name, StringComparer.Ordinal);
        return new LaneScope(scope.Kind, scope.Name, [.. groups]);
    }

    /// <summary>
    /// Dodge, Air dodge, Melee, Sliding melee, Grenade, Finisher, Super; then the weapons (any weapon first); Critical
    /// health; every other trigger (a pickup, a buff gained, a strike…) by name; the passives ("Always on") last.
    /// </summary>
    private static int RankGroup(string key) =>
        LeadingGroups.IndexOf(key) is var leading and >= 0
            ? leading
            : key == AnyWeaponGroup
                ? LeadingGroups.Length
                : key.StartsWith("weapon:", StringComparison.Ordinal)
                    ? LeadingGroups.Length + 1
                    : key == CriticalHealthGroup
                        ? LeadingGroups.Length + 2
                        : LaneMapping.IsAlwaysGroup(key)
                            ? LeadingGroups.Length + 4
                            : LeadingGroups.Length + 3;

    /// <summary>Within a group: the subclass's abilities, super, aspects and fragments; then the keywords; then what comes from outside.</summary>
    private static int RankOwner(GraphNodeType type) =>
        type.Match(
            actionTrigger: _ => 7,
            neededWeapon: _ => 7,
            subclassAbility: _ => 0,
            subclassSuper: _ => 1,
            subclassAspect: _ => 2,
            subclassFragment: _ => 3,
            buffStatus: _ => 4,
            debuffStatus: _ => 4,
            pickupDrop: _ => 4,
            summonedAlly: _ => 4,
            unlistedKeyword: _ => 4,
            keywordRule: _ => 4,
            shownEffect: _ => 7,
            keywordStrike: _ => 7,
            outsideElement: _ => 5);
}
