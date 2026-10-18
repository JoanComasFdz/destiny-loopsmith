using Loopsmith.Core.Domain;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;
using static VerifyXunit.Verifier;

namespace Loopsmith.RuleViewer.Tests.Golden;

/// <summary>
/// The repository's Arc Hunter flows as text: each scope's groups and counts, then every flow, one line each. A change to
/// the rules shows here as the flows (the scenarios to write) it adds, removes or changes.
/// </summary>
public class ArcHunterRulesGoldenTests
{
    [Fact]
    public Task Arc_Hunter_rules()
    {
        var rules = RuleViewing.DrawRules(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        return Verify(RuleViewing.RenderRulesText(rules));
    }
}
