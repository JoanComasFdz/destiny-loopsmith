using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Pure API the rule viewer's host (<c>tools/rule-viewer/Loopsmith.RuleViewer</c>) uses: the rules' dependency graph of a
/// class's subclass (which element needs what before it can fire, and what it gives) and its flows (every rule as one
/// claim: what sets it off ➜ what must be in place ➜ what it gives), as data, as text and as JSON. It reads the rules
/// through Core's Orchestration, as every host does (ADRs D39).
/// </summary>
public static class RuleViewing
{
    public static DependencyGraph DrawGraph(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);

    public static SubclassRules DrawRules(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        RuleFlowDrawing.DrawRules(catalog, guardianClass, subclass);

    /// <summary>The rule files → the flows; the parse error when the rules don't parse.</summary>
    public static Result<SubclassRules, string> DrawRules(ImmutableArray<SourceText> ruleFiles, GuardianClass guardianClass, Subclass subclass) =>
        CatalogLoading.ParseCatalog(ruleFiles).Map(catalog => RuleFlowDrawing.DrawRules(catalog, guardianClass, subclass));

    /// <summary>
    /// One rule's flows, by its full name (<c>hunter-arc-aspects-lethal-current-2</c>), wherever its file sits under the rules;
    /// the error when no rule has that name.
    /// </summary>
    public static Result<ImmutableArray<RuleFlow>, string> PickRuleFlows(RuleCatalog catalog, string ruleName) =>
        RuleFlowPicking.PickRuleFlows(catalog, ruleName);

    /// <summary>The rule files → one rule's flows, by its full name; the parse error when the rules don't parse.</summary>
    public static Result<ImmutableArray<RuleFlow>, string> PickRuleFlows(ImmutableArray<SourceText> ruleFiles, string ruleName) =>
        CatalogLoading.ParseCatalog(ruleFiles).Bind(catalog => RuleFlowPicking.PickRuleFlows(catalog, ruleName));

    public static string RenderRulesText(SubclassRules rules) => RuleFlowRendering.RenderText(rules);

    public static string RenderRulesJson(SubclassRules rules) => RuleFlowRendering.RenderJson(rules);
}
