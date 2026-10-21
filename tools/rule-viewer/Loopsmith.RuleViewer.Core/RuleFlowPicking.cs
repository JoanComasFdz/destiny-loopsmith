using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>An element drawn as itself: its own box, and each of its rules and passives with the flows it makes.</summary>
internal sealed record DrawnElement(RuleBox Box, ImmutableArray<RuleEntry> Rules);

/// <summary>
/// Picks one rule's flows out of the rules, by the rule's full name (<c>hunter-arc-aspects-lethal-current-2</c>, ADRs
/// D40), wherever its file sits: the element whose rule or passive has that name, drawn as itself (its class, none when it
/// is every class's; its subclass by its affinity, Prismatic when it has none), every flow the rule makes, once each, in
/// the order of their ids (a rule split by its trigger is several). Pure: same rules and name ⇒ same pick.
/// </summary>
public static class RuleFlowPicking
{
    public static Result<ImmutableArray<RuleFlow>, string> PickRuleFlows(RuleCatalog catalog, string ruleName)
    {
        var flows = catalog.Elements.Values
            .DistinctBy(element => element.Id)
            .Where(element => NamesRule(element, ruleName))
            .SelectMany(element => DrawElement(catalog, element).Rules)
            .Where(rule => rule.Name == ruleName)
            .SelectMany(rule => rule.Flows)
            .DistinctBy(flow => flow.Id)
            .OrderBy(flow => flow.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        return flows.IsEmpty
            ? new Result<ImmutableArray<RuleFlow>, string>.Error($"No rule is named '{ruleName}'.")
            : new Result<ImmutableArray<RuleFlow>, string>.Ok(flows);
    }

    /// <summary>
    /// An element drawn as itself, for its class (none when it is every class's) and its affinity's subclass: what a rule
    /// needs from the build stays as written ("Weapon of your Super's element"; a class ability of every class names no move).
    /// </summary>
    internal static DrawnElement DrawElement(RuleCatalog catalog, BuildElement element)
    {
        var context = new GraphContext(catalog, element.Class, ToSubclass(element.Affinity));
        return new DrawnElement(RuleFlowMapping.MapElementBox(context, element), RuleFlowMapping.MapRules(context, element));
    }

    private static bool NamesRule(BuildElement element, string ruleName) =>
        element.Rules.Any(rule => element.ToRuleName(rule.Id) == ruleName)
        || element.Passives.Any(passive => element.ToRuleName(passive.Id) == ruleName);

    /// <summary>The subclass an element is drawn for: its affinity's; Prismatic for one of no element (neutral, kinetic).</summary>
    private static Subclass ToSubclass(Affinity affinity) =>
        affinity switch
        {
            Affinity.Arc => Subclass.Arc,
            Affinity.Solar => Subclass.Solar,
            Affinity.Void => Subclass.Void,
            Affinity.Stasis => Subclass.Stasis,
            Affinity.Strand => Subclass.Strand,
            _ => Subclass.Prismatic,
        };
}
