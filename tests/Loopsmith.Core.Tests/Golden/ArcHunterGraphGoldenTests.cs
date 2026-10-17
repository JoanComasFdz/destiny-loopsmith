using Loopsmith.Core.Domain;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;
using static VerifyXunit.Verifier;

namespace Loopsmith.Core.Tests.Golden;

/// <summary>The repository's Arc Hunter dependency graph, its three scopes as text: columns, nodes with no column, edges.</summary>
public class ArcHunterGraphGoldenTests
{
    [Fact]
    public Task Arc_Hunter_graph()
    {
        var graph = DependencyGraphs.DrawGraph(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        return Verify(DependencyGraphs.RenderGraphText(graph));
    }
}
