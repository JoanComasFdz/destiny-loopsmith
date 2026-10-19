using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer.Core;

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
            .SelectMany(element => RuleFlowMapping.MapFlows(new GraphContext(catalog, element.Class, ToSubclass(element.Affinity)), element))
            .Select(mapped => mapped.Flow)
            .Where(flow => flow.Rule == ruleName)
            .DistinctBy(flow => flow.Id)
            .OrderBy(flow => flow.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        return flows.IsEmpty
            ? new Result<ImmutableArray<RuleFlow>, string>.Error($"No rule is named '{ruleName}'.")
            : new Result<ImmutableArray<RuleFlow>, string>.Ok(flows);
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
