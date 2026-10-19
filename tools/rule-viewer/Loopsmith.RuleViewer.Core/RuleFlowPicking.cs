using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Picks one rule's flows out of a subclass's, by the rule's full name (<c>hunter-arc-aspects-lethal-current-2</c>,
/// ADRs D40): every flow it makes, once each whatever scopes show it, in the order of their ids (a rule split by its
/// trigger is several). Pure: same flows and name ⇒ same pick.
/// </summary>
public static class RuleFlowPicking
{
    public static Result<ImmutableArray<RuleFlow>, string> PickRuleFlows(SubclassRules rules, string ruleName)
    {
        var flows = rules.Scopes
            .SelectMany(scope => scope.Groups)
            .SelectMany(group => group.Flows)
            .Where(flow => flow.Rule == ruleName)
            .DistinctBy(flow => flow.Id)
            .OrderBy(flow => flow.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        return flows.IsEmpty
            ? new Result<ImmutableArray<RuleFlow>, string>.Error($"No {rules.Title} rule is named '{ruleName}'.")
            : new Result<ImmutableArray<RuleFlow>, string>.Ok(flows);
    }
}
