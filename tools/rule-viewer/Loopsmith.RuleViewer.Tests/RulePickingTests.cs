using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// One rule's flows, picked by its full name for its PNG, wherever its file sits: every flow it makes, once each, drawn as
/// its own element (its class, or none; its subclass by its affinity), and an error for a name no rule has.
/// </summary>
public sealed class RulePickingTests
{
    private static ImmutableArray<RuleFlow> PickFlows(RuleCatalog catalog, string ruleName) =>
        RuleViewing.PickRuleFlows(catalog, ruleName).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static ImmutableArray<RuleFlow> PickFlows(string ruleName) => PickFlows(RepoCatalog.Catalog, ruleName);

    public static TheoryData<string> RuleNames() =>
        [.. RepoCatalog.Catalog.Elements.Values
            .SelectMany(element => element.Rules.Select(rule => element.ToRuleName(rule.Id)).Concat(element.Passives.Select(passive => element.ToRuleName(passive.Id))))
            .Distinct()
            .Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(RuleNames))]
    public void Every_rule_in_the_rules_folder_has_its_flows(string ruleName)
    {
        var flows = PickFlows(ruleName);

        Assert.NotEmpty(flows);
        Assert.All(flows, flow => Assert.Equal(ruleName, flow.Rule));
    }

    [Fact]
    public void A_rule_is_its_one_flow()
    {
        var flows = PickFlows("hunter-arc-aspects-lethal-current-2");

        var flow = Assert.Single(flows);
        Assert.Equal("hunter-arc-aspects-lethal-current-2", flow.Rule);
        Assert.Equal("Class ability", flow.Trigger.Name);
        Assert.Equal("Dodge", flow.Trigger.Caption);
    }

    [Fact]
    public void A_rule_split_by_its_trigger_is_every_flow_it_makes_once_each()
    {
        var flows = PickFlows("keywords-arc-jolt-2");

        Assert.Equal(["Grenade hit", "Melee hit", "Sliding melee hit", "Super hit"], flows.Select(flow => flow.Trigger.Name));
        Assert.All(flows, flow => Assert.Equal("keywords-arc-jolt-2", flow.Rule));
    }

    [Fact]
    public void A_rule_that_builds_on_no_Arc_Hunter_part_has_its_flows()
    {
        var flows = PickFlows("mods-weapons-synergy-1");

        var flow = Assert.Single(flows);
        Assert.Equal(["Orb of Power"], flow.Results.Select(result => result.Name));
    }

    [Fact]
    public void A_rule_of_another_element_has_its_flows()
    {
        var flows = PickFlows("keywords-strand-woven-mail-1");

        var flow = Assert.Single(flows);
        Assert.Contains(flow.Conditions, condition => condition.Name == "Woven Mail");
    }

    [Fact]
    public void A_weapon_perk_outside_the_Arc_Hunter_has_its_flows() =>
        Assert.NotEmpty(PickFlows("weapons-perks-hatchling-1"));

    [Fact]
    public void A_rule_of_every_class_names_no_class_move()
    {
        var flow = Assert.Single(PickFlows("mods-class-item-distribution-1"));

        Assert.Equal("Class ability", flow.Trigger.Name);
        Assert.Equal("", flow.Trigger.Caption);
    }

    [Fact]
    public void A_rule_in_a_folder_no_rule_file_has_today_has_its_flows()
    {
        var moved = RepoCatalog.Files.Select(file => file.Path == "mods/arms.yaml" ? file with { Path = "warlock/void/class-items/arms.yaml" } : file).ToImmutableArray();
        var picked = RuleViewing.PickRuleFlows(moved, "warlock-void-class-items-arms-shield-break-charge-1");

        var flows = Assert.IsType<Result<ImmutableArray<RuleFlow>, string>.Ok>(picked).Value;
        var flow = Assert.Single(flows);
        Assert.Equal("Shield broken", flow.Trigger.Name);
    }

    [Fact]
    public void A_name_no_rule_has_is_an_error()
    {
        var picked = RuleViewing.PickRuleFlows(RepoCatalog.Catalog, "hunter-arc-aspects-no-such-rule-1");

        var error = Assert.IsType<Result<ImmutableArray<RuleFlow>, string>.Error>(picked);
        Assert.Equal("No rule is named 'hunter-arc-aspects-no-such-rule-1'.", error.Failure);
    }
}
