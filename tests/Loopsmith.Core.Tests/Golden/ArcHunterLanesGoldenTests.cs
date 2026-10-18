using Loopsmith.Core.Domain;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;
using static VerifyXunit.Verifier;

namespace Loopsmith.Core.Tests.Golden;

/// <summary>
/// The repository's Arc Hunter lanes as text: each scope's groups and counts, then every lane, one line each. A change to
/// the rules shows here as the lanes (the scenarios to write) it adds, removes or changes.
/// </summary>
public class ArcHunterLanesGoldenTests
{
    [Fact]
    public Task Arc_Hunter_lanes()
    {
        var lanes = DependencyGraphs.DrawLanes(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        return Verify(DependencyGraphs.RenderLanesText(lanes));
    }
}
