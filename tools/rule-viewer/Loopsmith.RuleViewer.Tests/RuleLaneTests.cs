using System.Collections.Immutable;
using Loopsmith.RuleViewer.Core;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Tests.Support;
using static Loopsmith.RuleViewer.Tests.Support.TestCatalog;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// The rules as lanes, one claim each: what sets a rule off ➜ what must be in place ➜ what it gives, grouped by what
/// sets it off. A lane is one scenario to write, so a rule that names several triggers is one lane per trigger.
/// </summary>
public sealed class RuleLaneTests
{
    private static readonly DamageSource AnySource = new DamageSource.AnySource();

    private static BuildElement Fragment(string id, params Rule[] rules) =>
        Element(id, ElementKind.Fragment, [.. rules]) with { Class = Optional.Some(GuardianClass.Hunter) };

    private static RuleLanes Draw(params BuildElement[] elements) =>
        RuleViewing.DrawLanes(
            RepoCatalog.Catalog with { Glossary = Glossary, Elements = elements.ToImmutableDictionary(element => element.Id) },
            GuardianClass.Hunter,
            Subclass.Arc);

    private static LaneScope FindScope(RuleLanes lanes, ScopeKind kind = ScopeKind.WithDependents) => lanes.Scopes.Single(scope => scope.Kind == kind);

    private static ImmutableArray<RuleLane> ListLanes(RuleLanes lanes) => [.. FindScope(lanes).Groups.SelectMany(group => group.Lanes)];

    private static RuleLane FindLane(RuleLanes lanes, string id) => ListLanes(lanes).Single(lane => lane.Id == id);

    private static ImmutableArray<string> ListTriggers(RuleLanes lanes) => [.. ListLanes(lanes).Select(lane => lane.Trigger.Name)];

    private static string Describe(LaneBox box) => box.Caption.Length == 0 ? box.Name : $"{box.Name} ({box.Caption})";

    private static Rule OnGrenadeCast(params Outcome[] then) => On(new Trigger.AbilityCast(AbilityKind.Grenade), then);

    private static Rule OnHit(DamageSource via, params Outcome[] then) => On(new Trigger.Damage(via), then);

    private static PassiveRule Always(Passive passive, params Condition[] when) => new(passive, [.. when], Optional.None<string>());

    [Fact]
    public void Each_rule_and_each_passive_is_one_lane()
    {
        var spark = Fragment("spark", OnGrenadeCast(Buff("amplified")), On(new Trigger.Finisher(), Debuff("jolt"))) with
        {
            Passives = [Always(new Passive.ModifyStat(StatKind.Grenade, new GameValue.Known(20m)))],
        };

        var lanes = Draw(spark);

        Assert.Equal(["spark#0", "spark#1", "spark#p0"], ListLanes(lanes).Select(lane => lane.Id).Order(StringComparer.Ordinal));
        Assert.Equal("Always", FindLane(lanes, "spark#p0").Trigger.Name);
        Assert.Equal("Grenade cast", FindLane(lanes, "spark#0").Trigger.Name);
        Assert.Equal("Finisher", FindLane(lanes, "spark#1").Trigger.Name);
    }

    [Fact]
    public void Any_ability_is_one_lane_per_ability_that_deals_damage_never_a_dodge()
    {
        var lanes = Draw(Fragment("spark", On(new Trigger.KillAny(new DamageSource.AnyAbility()), Buff("amplified"))));

        Assert.Equal(["Melee kill", "Sliding melee kill", "Grenade kill", "Super kill"], ListTriggers(lanes));
        Assert.Equal(["spark#0.0", "spark#0.1", "spark#0.2", "spark#0.3"], ListLanes(lanes).Select(lane => lane.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Any_melee_is_a_Melee_lane_and_a_Sliding_melee_lane()
    {
        var lanes = Draw(Fragment("spark", OnHit(new DamageSource.AbilityOf(AbilityKind.Melee), Buff("amplified"))));

        Assert.Equal(["Melee hit", "Sliding melee hit"], ListTriggers(lanes));
    }

    [Fact]
    public void A_punch_is_only_a_Melee_lane()
    {
        var lanes = Draw(Fragment("spark", OnHit(new DamageSource.AbilityUsedAs(AbilityKind.Melee, AbilityInput.Press), Buff("amplified"))));

        Assert.Equal(["Melee hit"], ListTriggers(lanes));
    }

    [Fact]
    public void A_sliding_melee_is_only_a_Sliding_melee_lane()
    {
        var lanes = Draw(Fragment("spark", OnHit(new DamageSource.AbilityUsedAs(AbilityKind.Melee, AbilityInput.Slide), Buff("amplified"))));

        Assert.Equal(["Sliding melee hit"], ListTriggers(lanes));
    }

    [Fact]
    public void The_graph_draws_any_melee_from_both_melee_triggers()
    {
        var graph = RuleViewing.DrawGraph(
            RepoCatalog.Catalog with { Glossary = Glossary, Elements = ImmutableDictionary.CreateRange([KeyValuePair.Create(ElementId.From("spark"), Fragment("spark", OnHit(new DamageSource.AbilityOf(AbilityKind.Melee), Buff("amplified"))))]) },
            GuardianClass.Hunter,
            Subclass.Arc);

        var needs = graph.Scopes[^1].Edges.Where(edge => edge.To == "spark" && edge.Kind == EdgeKind.Needs).Select(edge => edge.From);

        Assert.Equal(["trigger:melee", "trigger:sliding-melee"], needs.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_lane_names_the_element_equipped_first()
    {
        var lane = FindLane(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")))), "spark#0");

        Assert.Equal("Subclass fragment", lane.Conditions[0].Title);
        Assert.Equal("Spark (equipped)", Describe(lane.Conditions[0]));
    }

    [Fact]
    public void A_keywords_lane_shows_no_element_and_needs_nothing_more_when_nothing_else_is_in_place()
    {
        var jolt = Element("jolt", ElementKind.Keyword, [On(new Trigger.KillDebuffed(AnySource, [Status("jolt")]), Buff("amplified")), On(new Trigger.Finisher(), Buff("amplified"))]);

        var lanes = Draw(Fragment("spark", OnGrenadeCast(Debuff("jolt"))), jolt);

        Assert.Equal(["Jolt (on the target)"], FindLane(lanes, "rule:jolt#0").Conditions.Select(Describe));
        Assert.Empty(FindLane(lanes, "rule:jolt#1").Conditions);
    }

    [Fact]
    public void Each_state_a_lane_needs_has_its_caption()
    {
        var spark = Fragment(
            "spark",
            new Rule(
                new Trigger.KillDebuffed(AnySource, [Status("jolt")]),
                [
                    new Condition.HasBuff(Status("amplified")),
                    new Condition.AtMax(Status("bolt-charge")),
                    new Condition.Holds(Status("armor-charge"), StackCount.From(2)),
                    new Condition.LacksBuff(Status("amplified")),
                    new Condition.BelowMax(Status("bolt-charge")),
                    new Condition.During(AbilityKind.ClassAbility),
                ],
                [Buff("amplified")],
                Optional.None<string>(),
                Likelihood.Always,
                []));

        var lane = FindLane(Draw(spark), "spark#0");

        Assert.Equal(
            [
                "Spark (equipped)", "Jolt (on the target)", "Amplified (on you)", "Bolt Charge (at max stacks)", "Armor Charge (2+ stacks)",
                "Amplified (not on you)", "Bolt Charge (below max stacks)", "Dodge (under way)",
            ],
            lane.Conditions.Select(Describe));
    }

    [Fact]
    public void Results_go_strikes_effects_buffs_debuffs_pickups_summons_then_what_the_lane_ends()
    {
        var lane = FindLane(
            Draw(Fragment(
                "spark",
                OnGrenadeCast(
                    new Outcome.SpendStacks(Status("armor-charge"), StackCount.From(2)),
                    new Outcome.Spawn(Pickup("ionic-trace"), 1),
                    Debuff("jolt"),
                    Buff("amplified"),
                    Energy(AbilityKind.Melee, new GameValue.Known(1m)),
                    new Outcome.StrikeTarget(Status("bolt-charge"), HitOutcome.Damage)))),
            "spark#0");

        Assert.Equal(
            [BoxRole.Gives, BoxRole.Gives, BoxRole.Gives, BoxRole.Gives, BoxRole.Gives, BoxRole.Ends],
            lane.Results.Select(box => box.Role));
        Assert.Equal(
            [BoxTone.Strike, BoxTone.Effect, BoxTone.Buff, BoxTone.Debuff, BoxTone.Pickup, BoxTone.Buff],
            lane.Results.Select(box => box.Tone));
    }

    [Fact]
    public void Amounts_show_as_authored()
    {
        var lane = FindLane(
            Draw(Fragment(
                "spark",
                OnGrenadeCast(
                    Buff("bolt-charge", stacks: 2),
                    new Outcome.DebuffTarget(Status("jolt"), Optional.Some(Seconds.From(8.5m))),
                    Energy(AbilityKind.Melee, new GameValue.Known(1m)),
                    Energy(AbilityKind.Grenade, new GameValue.Unknown()),
                    new Outcome.SpendStacks(Status("armor-charge"), StackCount.From(2)),
                    new Outcome.RemoveBuff(Status("amplified"))))),
            "spark#0");

        Assert.Equal(
            ["Melee energy (100%)", "Grenade energy (?)", "Bolt Charge (×2)", "Jolt (8.5s)", "Armor Charge (ends · spends 2)", "Amplified (ends)"],
            lane.Results.Select(Describe));
    }

    [Fact]
    public void A_passive_adds_to_the_status_it_boosts()
    {
        var spark = Fragment("spark") with { Passives = [Always(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)))] };

        var lane = FindLane(Draw(spark, Fragment("ember", OnGrenadeCast(Buff("bolt-charge")))), "spark#p0");

        Assert.Equal(["Bolt Charge (adds to · +1 stack)"], lane.Results.Select(Describe));
    }

    [Fact]
    public void A_chance_rule_is_marked()
    {
        var lane = FindLane(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")) with { Likelihood = Likelihood.Chance })), "spark#0");

        Assert.True(lane.ByChance);
    }

    [Fact]
    public void Groups_go_by_what_sets_them_off_with_the_passives_last()
    {
        var spark = Fragment(
            "spark",
            OnGrenadeCast(Buff("amplified")),
            On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified")),
            On(new Trigger.CriticalHealth(), Buff("amplified")),
            On(new Trigger.KillAny(new DamageSource.WeaponOfType(DamageType.Arc)), Buff("amplified")),
            On(new Trigger.BuffGained(Status("amplified")), Debuff("jolt")),
            On(new Trigger.KillAny(AnySource), Buff("amplified")),
            OnHit(new DamageSource.AbilityUsedAs(AbilityKind.Melee, AbilityInput.Slide), Buff("amplified")),
            On(new Trigger.AbilityCast(AbilityKind.Super), Buff("amplified"))) with
        {
            Passives = [Always(new Passive.ModifyStat(StatKind.Grenade, new GameValue.Known(20m)))],
        };

        var groups = FindScope(Draw(spark)).Groups.Select(group => $"{group.Name} {group.Lanes.Length}");

        Assert.Equal(["Dodge 1", "Sliding melee 1", "Grenade 1", "Super 1", "Arc weapon 1", "Critical health 1", "Amplified 1", "Any 1", "Always on 1"], groups);
    }

    [Fact]
    public void A_lane_that_needs_a_weapon_is_only_in_the_scopes_with_the_weapon_in_view()
    {
        var lanes = Draw(Fragment("beacon", On(new Trigger.KillAny(new DamageSource.WeaponOfType(DamageType.Arc)), Debuff("jolt"))));

        Assert.Equal("Arc weapon kill", Assert.Single(FindScope(lanes, ScopeKind.Subclass).Groups.SelectMany(group => group.Lanes)).Trigger.Name);
        Assert.Equal(["Arc subclass", "+ what it needs from outside", "+ what builds on it"], lanes.Scopes.Select(scope => scope.Name));
    }

    [Fact]
    public void The_kind_titles_carry_no_keyword_suffix()
    {
        var lanes = RuleViewing.DrawLanes(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        var titles = ListLanes(lanes).SelectMany(lane => lane.Conditions.Concat(lane.Results).Append(lane.Trigger)).Select(box => box.Title).ToImmutableHashSet();

        Assert.DoesNotContain(titles, title => title.Contains("keyword", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Subclass ability · Class ability", titles);
        Assert.Contains("Armor mod", titles);
    }

    [Fact]
    public void The_json_holds_the_scopes_groups_lanes_and_boxes()
    {
        var json = RuleViewing.RenderLanesJson(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")))));

        Assert.StartsWith("""{"title":"Arc Hunter","scopes":[{"id":"subclass","name":"Arc subclass","groups":[{"key":"trigger:grenade","name":"Grenade",""", json);
        Assert.Contains(""""lanes":[{"id":"spark#0","element":"spark","file":null,"reason":null,"chance":false,"trigger":{"node":"trigger:grenade","title":"Trigger","name":"Grenade cast","""", json);
        Assert.Contains(""""tone":"fragment","role":"condition","icon":{"kind":"blank"}"""", json);
    }
}
