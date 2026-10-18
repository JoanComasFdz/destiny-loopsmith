using System.Collections.Immutable;
using Loopsmith.Core.DependencyGraphing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;

namespace Loopsmith.Core.Orchestration;

/// <summary>
/// Pure API the lanes tool (<c>tools/graph</c>) uses: the rules' dependency graph of a class's subclass (which element
/// needs what before it can fire, and what it gives) and its lanes (every rule as one claim: what sets it off ➜ what
/// must be in place ➜ what it gives), as data, as text and as JSON.
/// </summary>
public static class DependencyGraphs
{
    public static DependencyGraph DrawGraph(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);

    public static RuleLanes DrawLanes(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        LaneDrawing.DrawLanes(catalog, guardianClass, subclass);

    /// <summary>The rule files → the lanes; the parse error when the rules don't parse.</summary>
    public static Result<RuleLanes, string> DrawLanes(ImmutableArray<SourceText> ruleFiles, GuardianClass guardianClass, Subclass subclass) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles).Map(catalog => LaneDrawing.DrawLanes(catalog, guardianClass, subclass));

    public static string RenderLanesText(RuleLanes lanes) => LaneRendering.RenderText(lanes);

    public static string RenderLanesJson(RuleLanes lanes) => LaneRendering.RenderJson(lanes);
}
