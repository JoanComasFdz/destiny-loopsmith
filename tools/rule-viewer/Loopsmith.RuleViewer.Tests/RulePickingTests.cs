using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>One rule's flows, picked by its full name for its PNG: every flow it makes, once each, and an error for a name no flow has.</summary>
public sealed class RulePickingTests
{
    private static readonly SubclassRules ArcHunter = RuleViewing.DrawRules(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

    private static ImmutableArray<RuleFlow> PickFlows(string ruleName) =>
        RuleViewing.PickRuleFlows(ArcHunter, ruleName).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void A_rule_is_its_one_flow()
    {
        var flows = PickFlows("hunter-arc-aspects-lethal-current-2");

        var flow = Assert.Single(flows);
        Assert.Equal("hunter-arc-aspects-lethal-current-2", flow.Rule);
        Assert.Equal("Class ability", flow.Trigger.Name);
    }

    [Fact]
    public void A_rule_split_by_its_trigger_is_every_flow_it_makes_once_each()
    {
        var flows = PickFlows("keywords-arc-jolt-2");

        Assert.Equal(["Grenade hit", "Melee hit", "Sliding melee hit", "Super hit"], flows.Select(flow => flow.Trigger.Name));
        Assert.All(flows, flow => Assert.Equal("keywords-arc-jolt-2", flow.Rule));
    }

    [Fact]
    public void A_name_no_rule_has_is_an_error()
    {
        var picked = RuleViewing.PickRuleFlows(ArcHunter, "hunter-arc-aspects-no-such-rule-1");

        var error = Assert.IsType<Result<ImmutableArray<RuleFlow>, string>.Error>(picked);
        Assert.Equal("No Arc Hunter rule is named 'hunter-arc-aspects-no-such-rule-1'.", error.Failure);
    }
}
