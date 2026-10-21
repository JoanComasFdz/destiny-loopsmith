using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Draws the flows of a class's subclass: every rule and passive of the elements each scope of the dependency graph
/// holds, one flow per claim, grouped by what sets it off. Pure: same rules ⇒ same flows. The flows are the list of the
/// atomic scenarios the rules make: one flow, one claim, one scenario.
/// </summary>
public static class RuleFlowDrawing
{
    /// <summary>The groups that come first, in the order a player reaches for them; the weapons come after the Super.</summary>
    private static readonly ImmutableArray<string> LeadingGroups =
        ["trigger:class-ability", "trigger:air-class-ability", "trigger:melee", "trigger:sliding-melee", "trigger:grenade", "trigger:finisher", "trigger:super"];

    private const string AnyWeaponGroup = "weapon:weapon";

    private const string CriticalHealthGroup = "trigger:critical-health";

    private const string HitAtCriticalHealthGroup = "trigger:hit-at-critical-health";

    public static SubclassRules DrawRules(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass)
    {
        var context = new GraphContext(catalog, Optional.Some(guardianClass), subclass);
        var graph = DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);
        var flows = catalog.Elements.Values
            .Where(element => DependencyMapping.IsDrawn(context, element))
            .SelectMany(element => RuleFlowMapping.MapFlows(context, element))
            .ToImmutableArray();
        return new SubclassRules(
            graph.Title,
            [.. graph.Scopes.Select(scope => DrawScope(graph, scope, flows))]);
    }

    private static RuleScope DrawScope(DependencyGraph graph, GraphScope scope, ImmutableArray<MappedFlow> flows)
    {
        var members = scope.Nodes.ToImmutableHashSet();
        var groups = flows
            .Where(mapped => members.Contains(mapped.Flow.Element))
            .GroupBy(mapped => mapped.GroupKey)
            .Select(group => new RuleGroup(
                group.Key,
                group.First().GroupName,
                group.First().Flow.Trigger.Icon,
                [.. group
                    .Select(mapped => mapped.Flow)
                    .OrderBy(flow => RankOwner(graph.Nodes[flow.Element].Type))
                    .ThenBy(flow => graph.Nodes[flow.Element].Name, StringComparer.Ordinal)
                    .ThenBy(flow => flow.Id, StringComparer.Ordinal)]))
            .OrderBy(group => RankGroup(group.Key))
            .ThenBy(group => group.Name, StringComparer.Ordinal);
        return new RuleScope(scope.Kind, scope.Name, [.. groups]);
    }

    /// <summary>
    /// Class ability, Air class ability, Melee, Sliding melee, Grenade, Finisher, Super; then the weapons (any weapon first); Critical
    /// health, then Hit at critical health; every other trigger (a pickup, a buff gained, a strike…) by name; the passives ("Always on") last.
    /// </summary>
    private static int RankGroup(string key) =>
        LeadingGroups.IndexOf(key) is var leading and >= 0
            ? leading
            : key == AnyWeaponGroup
                ? LeadingGroups.Length
                : key.StartsWith("weapon:", StringComparison.Ordinal)
                    ? LeadingGroups.Length + 1
                    : key is CriticalHealthGroup or HitAtCriticalHealthGroup
                        ? LeadingGroups.Length + 2
                        : RuleFlowMapping.IsAlwaysGroup(key)
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
