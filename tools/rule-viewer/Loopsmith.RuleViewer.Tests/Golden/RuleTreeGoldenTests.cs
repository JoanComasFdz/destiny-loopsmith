using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;
using static VerifyXunit.Verifier;

namespace Loopsmith.RuleViewer.Tests.Golden;

/// <summary>
/// Every rule in the repository's rules folder as text: how many rules and flows, then each file, its elements and every
/// flow, one line each, in the tree's order. A change to the rules shows here as the flows (the scenarios to write) it
/// adds, removes or changes.
/// </summary>
public class RuleTreeGoldenTests
{
    [Fact]
    public Task Every_rule_file()
    {
        var tree = RuleViewing.DrawTree(RepoCatalog.Catalog);

        return Verify(RuleViewing.RenderTreeText(tree));
    }
}
