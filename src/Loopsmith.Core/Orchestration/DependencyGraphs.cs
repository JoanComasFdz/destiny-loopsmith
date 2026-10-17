using System.Collections.Immutable;
using Loopsmith.Core.DependencyGraphing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;

namespace Loopsmith.Core.Orchestration;

/// <summary>The graph's two files: the JSON the page reads, and the page with it inside.</summary>
public sealed record GraphPages(string Json, string Html);

/// <summary>
/// Pure API the graph tool (<c>tools/graph</c>) uses: the rules' dependency graph of a class's subclass — which element
/// needs what before it can fire, and what it gives — as JSON, as its page and as text.
/// </summary>
public static class DependencyGraphs
{
    public static DependencyGraph DrawGraph(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass);

    public static string RenderGraphText(DependencyGraph graph) => GraphRendering.RenderText(graph);

    public static string RenderGraphJson(DependencyGraph graph) => GraphRendering.RenderJson(graph);

    /// <summary>The rule files → the graph's JSON and its page (<paramref name="pageTemplate"/> with the JSON at <c>__DATA__</c>).</summary>
    public static Result<GraphPages, string> DrawGraphPages(
        ImmutableArray<SourceText> ruleFiles, string pageTemplate, GuardianClass guardianClass, Subclass subclass) =>
        RuleCatalogParsing.ParseCatalog(ruleFiles)
            .Map(catalog => DependencyGraphDrawing.DrawGraph(catalog, guardianClass, subclass))
            .Map(graph => new GraphPages(GraphRendering.RenderJson(graph), GraphRendering.RenderPage(pageTemplate, graph)));
}
