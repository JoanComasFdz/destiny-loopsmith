using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// Pure API the rule viewer's host (<c>tools/rule-viewer/Loopsmith.RuleViewer</c>) uses: the rules' dependency graph of a
/// class's subclass (which element needs what before it can fire, and what it gives) and its lanes (every rule as one
/// claim: what sets it off ➜ what must be in place ➜ what it gives), as data, as text and as JSON. It reads the rules
/// through Core's Orchestration, as every host does (ADRs D39).
/// </summary>
public static class RuleViewing
{
    public static DependencyGraph DrawGraph(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);

    public static RuleLanes DrawLanes(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        LaneDrawing.DrawLanes(catalog, guardianClass, subclass);

    /// <summary>The rule files → the lanes; the parse error when the rules don't parse.</summary>
    public static Result<RuleLanes, string> DrawLanes(ImmutableArray<SourceText> ruleFiles, GuardianClass guardianClass, Subclass subclass) =>
        CatalogLoading.ParseCatalog(ruleFiles).Map(catalog => LaneDrawing.DrawLanes(catalog, guardianClass, subclass));

    public static string RenderLanesText(RuleLanes lanes) => LaneRendering.RenderText(lanes);

    public static string RenderLanesJson(RuleLanes lanes) => LaneRendering.RenderJson(lanes);
}
