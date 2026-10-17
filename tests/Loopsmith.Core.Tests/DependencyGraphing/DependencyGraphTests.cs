using System.Collections.Immutable;
using Loopsmith.Core.DependencyGraphing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;
using static Loopsmith.Core.Tests.Support.TestCatalog;

namespace Loopsmith.Core.Tests.DependencyGraphing;

/// <summary>
/// The rules' dependency graph: what each rule needs and gives as edges, the columns (the order to test the elements in)
/// and the three scopes (the subclass, what it needs from outside, what builds on it).
/// </summary>
public sealed class DependencyGraphTests
{
    private static readonly DamageSource AnySource = new DamageSource.AnySource();

    private static BuildElement Part(string id, ElementKind kind, params Rule[] rules) =>
        Element(id, kind, [.. rules]) with { Class = Optional.Some(GuardianClass.Hunter) };

    private static BuildElement Fragment(string id, params Rule[] rules) => Part(id, ElementKind.Fragment, rules);

    private static BuildElement Keyword(string id, params Rule[] rules) => Element(id, ElementKind.Keyword, [.. rules]);

    private static RuleCatalog ToCatalog(params BuildElement[] elements) =>
        RepoCatalog.Catalog with { Glossary = Glossary, Elements = elements.ToImmutableDictionary(element => element.Id) };

    private static DependencyGraph Draw(params BuildElement[] elements) =>
        DependencyGraphs.DrawGraph(ToCatalog(elements), GuardianClass.Hunter, Subclass.Arc);

    private static GraphScope FindScope(DependencyGraph graph, ScopeKind kind) => graph.Scopes.Single(scope => scope.Kind == kind);

    private static ImmutableArray<string> ListEdges(DependencyGraph graph, ScopeKind kind = ScopeKind.WithDependents) =>
        [.. FindScope(graph, kind).Edges.Select(edge => $"{edge.From} -[{edge.Kind} {edge.Label}]-> {edge.To}".Replace(" ]", "]", StringComparison.Ordinal))];

    private static int? FindColumn(DependencyGraph graph, string id, ScopeKind kind = ScopeKind.Subclass) =>
        FindScope(graph, kind).Columns.TryGetValue(id, out var column) ? column.Match<int?>(some => some.Value, _ => null) : null;

    private static Rule OnGrenadeCast(params Outcome[] then) => On(new Trigger.AbilityCast(AbilityKind.Grenade), then);

    private static Rule WhileHas(string status, params Outcome[] then) =>
        OnWhen(new Trigger.KillAny(AnySource), new Condition.HasBuff(Status(status)), then);

    [Fact]
    public void A_kill_on_a_Jolted_enemy_needs_Jolt()
    {
        var graph = Draw(Fragment("spark", On(new Trigger.KillDebuffed(AnySource, [Status("jolt")]), Buff("amplified"))));

        Assert.Contains("jolt -[Needs kill on Jolted]-> spark", ListEdges(graph));
    }

    [Fact]
    public void Applying_a_buff_gives_it()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))));

        Assert.Contains("spark -[Gives]-> amplified", ListEdges(graph));
        Assert.Contains("trigger:grenade -[Needs cast]-> spark", ListEdges(graph));
    }

    [Fact]
    public void Any_ability_is_one_edge_from_each_ability_trigger_that_deals_damage()
    {
        var graph = Draw(
            Fragment("spark", On(new Trigger.Damage(new DamageSource.AnyAbility()), Debuff("jolt"))),
            Part("dodge", ElementKind.ClassAbility, On(new Trigger.AbilityCast(AbilityKind.ClassAbility))),
            Part("leap", ElementKind.Aspect, On(new Trigger.AbilityCastAs(AbilityKind.ClassAbility, AbilityInput.Air))));

        var needed = FindScope(graph, ScopeKind.Subclass).Edges.Where(edge => edge.To == "spark" && edge.Kind == EdgeKind.Needs).Select(edge => edge.From);

        Assert.Equal(["trigger:grenade", "trigger:melee", "trigger:sliding-melee", "trigger:super"], needed.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_damage_via_ability_rule_gets_no_edge_from_Dodge_or_Air_dodge()
    {
        var graph = Draw(
            Fragment("spark", On(new Trigger.KillAny(new DamageSource.AnyAbility()), Buff("amplified"))),
            Part("dodge", ElementKind.ClassAbility, On(new Trigger.AbilityCast(AbilityKind.ClassAbility))),
            Part("leap", ElementKind.Aspect, On(new Trigger.AbilityCastAs(AbilityKind.ClassAbility, AbilityInput.Air))));

        var edges = ListEdges(graph);

        Assert.DoesNotContain("trigger:dodge -[Needs kill]-> spark", edges);
        Assert.DoesNotContain("trigger:air-dodge -[Needs kill]-> spark", edges);
        Assert.Contains("trigger:dodge -[Needs cast]-> dodge", edges);
    }

    [Fact]
    public void The_class_abilitys_air_move_is_the_Air_dodge()
    {
        var graph = Draw(Part("leap", ElementKind.Aspect, On(new Trigger.AbilityCastAs(AbilityKind.ClassAbility, AbilityInput.Air), Debuff("jolt"))));

        Assert.Contains("trigger:air-dodge -[Needs cast]-> leap", ListEdges(graph));
        Assert.Equal("Air dodge", graph.Nodes["trigger:air-dodge"].Name);
    }

    [Fact]
    public void An_inert_element_is_left_out()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))), Fragment("quiet") with { Inert = Optional.Some("Damage only.") });

        Assert.False(graph.Nodes.ContainsKey("quiet"));
    }

    [Fact]
    public void An_element_that_needs_what_it_gives_has_no_edge_to_itself()
    {
        var bolt = Keyword(
            "bolt-charge",
            OnWhen(new Trigger.Damage(new DamageSource.AnyAbility()), new Condition.AtMax(Status("bolt-charge")), new Outcome.RemoveBuff(Status("bolt-charge"))));
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("bolt-charge"))), bolt);

        Assert.DoesNotContain(FindScope(graph, ScopeKind.WithDependents).Edges, edge => edge.From == edge.To);
    }

    [Fact]
    public void A_keyword_element_is_its_state_node_and_its_rule_node()
    {
        var jolt = Keyword("jolt", On(new Trigger.DamageDebuffed(new DamageSource.AnyAbility(), [Status("jolt")]), new Outcome.StrikeTarget(Status("jolt"), HitOutcome.Damage)));
        var graph = Draw(Fragment("spark", OnGrenadeCast(Debuff("jolt"))), jolt);

        var state = graph.Nodes["jolt"];
        var rule = graph.Nodes["rule:jolt"];
        var edges = ListEdges(graph);

        Assert.True(state.IsKeyword);
        Assert.IsType<GraphNodeType.DebuffStatus>(state.Type);
        Assert.False(state.Element.IsSome());
        Assert.IsType<GraphNodeType.KeywordRule>(rule.Type);
        Assert.Equal("Jolt", rule.Name);
        Assert.True(rule.Element.IsSome());
        Assert.Contains("spark -[Gives]-> jolt", edges);
        Assert.Contains("jolt -[Needs hit on Jolted]-> rule:jolt", edges);
        Assert.Contains("trigger:grenade -[Needs hit]-> rule:jolt", edges);
        Assert.Contains("rule:jolt -[Gives]-> strike:jolt", edges);
        Assert.Equal(2, FindColumn(graph, "jolt"));
        Assert.Equal(3, FindColumn(graph, "rule:jolt"));
        Assert.Equal(4, FindColumn(graph, "strike:jolt"));
    }

    [Fact]
    public void A_keyword_with_no_glossary_entry_stays_one_keyword_node()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))), Keyword("guardian", OnGrenadeCast(Buff("amplified"))));

        Assert.IsType<GraphNodeType.UnlistedKeyword>(graph.Nodes["guardian"].Type);
        Assert.False(graph.Nodes.ContainsKey("rule:guardian"));
    }

    public static TheoryData<string, string> OutcomeEffects => new()
    {
        { "energy", "spark -[Gives 12.5%]-> effect:energy-grenade" },
        { "full energy", "spark -[Gives full]-> effect:energy-class-ability" },
        { "least charged", "spark -[Gives 10%]-> effect:energy-least-charged" },
        { "convert", "spark -[Gives 5% per stack]-> effect:energy-melee" },
        { "convert ends", "spark -[Ends spends all]-> armor-charge" },
        { "spend", "spark -[Ends spends 2]-> armor-charge" },
        { "remove", "spark -[Ends ends]-> amplified" },
        { "health", "spark -[Gives 40% + allies]-> effect:health" },
        { "damage", "spark -[Gives +25% vs melee]-> effect:damage-bonus" },
        { "weapon strike", "spark -[Gives kill]-> effect:extra-strike" },
        { "reload", "spark -[Gives]-> effect:reload" },
        { "collect", "spark -[Collects collects]-> orb-of-power" },
        { "spawn", "spark -[Gives]-> ionic-trace" },
    };

    [Theory]
    [MemberData(nameof(OutcomeEffects))]
    public void Every_outcome_is_drawn(string what, string edge)
    {
        var graph = Draw(Fragment(
            "spark",
            OnGrenadeCast(
                Energy(AbilityKind.Grenade, new GameValue.Known(0.125m)),
                new Outcome.GrantEnergy(AbilityKind.ClassAbility, new EnergyGrant.Full()),
                new Outcome.GrantLeastChargedEnergy(new EnergyGrant.Fraction(new GameValue.Known(0.1m))),
                new Outcome.ConvertStacksToEnergy(Status("armor-charge"), AbilityKind.Melee, new StackEnergy.PerStack(new GameValue.Known(0.05m))),
                new Outcome.SpendStacks(Status("armor-charge"), StackCount.From(2)),
                new Outcome.RemoveBuff(Status("amplified")),
                new Outcome.RestoreHealth(new HealAmount.Share(new GameValue.Known(0.4m)), true),
                new Outcome.ModifyDamage(new DamageSource.AbilityOf(AbilityKind.Melee), new GameValue.Known(0.25m)),
                new Outcome.WeaponStrike(new DamageSource.ThisWeapon(), Optional.None<DamageType>(), HitOutcome.Kill),
                new Outcome.ReloadWeapon(new DamageSource.ThisWeapon()),
                new Outcome.Collect(Pickup("orb-of-power")),
                new Outcome.Spawn(Pickup("ionic-trace"), 1))));

        Assert.True(ListEdges(graph).Contains(edge), what);
    }

    public static TheoryData<string, string> PassiveEffects => new()
    {
        { "resist", "spark -[Gives 25% [PvP 10%]]-> effect:damage-resistance" },
        { "combatant resist", "spark -[Gives 15% vs combatants]-> effect:damage-resistance" },
        { "stat", "spark -[Gives +20]-> effect:stat-grenade" },
        { "charges", "spark -[Gives +1]-> effect:extra-charge-grenade" },
        { "extra stacks", "spark -[Boosts +1 stack]-> bolt-charge" },
        { "max stacks", "spark -[Boosts max 4 | 5 | 6]-> armor-charge" },
        { "weapon stats", "spark -[Gives Reload Speed +50]-> effect:weapon-stats" },
    };

    [Theory]
    [MemberData(nameof(PassiveEffects))]
    public void Every_passive_is_drawn(string what, string edge)
    {
        static PassiveRule Always(Passive passive) => new(passive, [], Optional.None<string>());
        var spark = Fragment("spark", OnGrenadeCast(Buff("amplified"))) with
        {
            Passives =
            [
                Always(new Passive.ResistDamage(new PveAndPvp(new GameValue.Known(0.25m), Optional.Some<GameValue>(new GameValue.Known(0.1m))))),
                Always(new Passive.ResistCombatantDamage(new CombatantDamage.All(), new GameValue.Known(0.15m))),
                Always(new Passive.ModifyStat(StatKind.Grenade, new GameValue.Known(20m))),
                Always(new Passive.ExtraCharges(AbilityKind.Grenade, ChargeCount.From(1))),
                Always(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1))),
                Always(new Passive.RaiseMaxStacks(Status("armor-charge"), new GameValue.PerModCount([4m, 5m, 6m]))),
                Always(new Passive.ModifyWeaponStats(new ChangedWeapons.ThisWeapon(), [new WeaponStatChange("Reload Speed", new GameValue.Known(50m))])),
            ],
        };

        var graph = Draw(spark);

        Assert.True(ListEdges(graph).Contains(edge), what);
    }

    [Fact]
    public void Only_gives_edges_place_a_node()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(new Outcome.RemoveBuff(Status("amplified")))));

        Assert.Null(FindColumn(graph, "amplified"));
    }

    [Fact]
    public void An_element_that_needs_only_triggers_is_in_column_1_and_what_it_gives_in_column_2()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))));

        Assert.Equal(0, FindColumn(graph, "trigger:grenade"));
        Assert.Equal(1, FindColumn(graph, "spark"));
        Assert.Equal(2, FindColumn(graph, "amplified"));
    }

    [Fact]
    public void An_element_that_needs_what_a_first_layer_element_gives_is_in_column_3()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))), Fragment("ember", WhileHas("amplified", Debuff("jolt"))));

        Assert.Equal(3, FindColumn(graph, "ember"));
        Assert.Equal(4, FindColumn(graph, "jolt"));
    }

    [Fact]
    public void An_element_takes_the_lowest_layer_over_its_rules()
    {
        var graph = Draw(
            Fragment("spark", OnGrenadeCast(Buff("amplified"))),
            Fragment("ember", WhileHas("amplified", Debuff("jolt")), On(new Trigger.Finisher(), Debuff("jolt"))));

        Assert.Equal(1, FindColumn(graph, "ember"));
    }

    [Fact]
    public void A_cycle_ends_with_no_column_when_nothing_starts_it()
    {
        var graph = Draw(
            Fragment("spark", OnGrenadeCast(Debuff("jolt"))),
            Fragment("ember", WhileHas("amplified", Buff("bolt-charge"))),
            Fragment("flare", WhileHas("bolt-charge", Buff("amplified"))));

        Assert.Null(FindColumn(graph, "ember"));
        Assert.Null(FindColumn(graph, "flare"));
    }

    [Fact]
    public void A_cycle_with_a_way_in_is_placed_in_order()
    {
        var graph = Draw(
            Fragment("ember", WhileHas("amplified", Buff("bolt-charge")), OnGrenadeCast(Buff("bolt-charge"))),
            Fragment("flare", WhileHas("bolt-charge", Buff("amplified"))));

        Assert.Equal(1, FindColumn(graph, "ember"));
        Assert.Equal(2, FindColumn(graph, "bolt-charge"));
        Assert.Equal(3, FindColumn(graph, "flare"));
        Assert.Equal(4, FindColumn(graph, "amplified"));
    }

    [Fact]
    public void An_element_that_needs_a_weapon_has_no_column_in_the_subclass_but_one_with_its_needs()
    {
        var graph = Draw(Fragment("beacon", On(new Trigger.KillAny(new DamageSource.WeaponOfType(DamageType.Arc)), Debuff("jolt"))));

        Assert.True(FindScope(graph, ScopeKind.Subclass).Columns.ContainsKey("beacon"));
        Assert.Null(FindColumn(graph, "beacon"));
        Assert.Equal(1, FindColumn(graph, "beacon", ScopeKind.WithNeeds));
        Assert.Equal(0, FindColumn(graph, "weapon:arc-weapon", ScopeKind.WithNeeds));
    }

    [Fact]
    public void The_repos_Arc_Hunter_needs_an_Arc_weapon_from_outside_its_subclass()
    {
        var graph = DependencyGraphs.DrawGraph(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        Assert.DoesNotContain("weapon:arc-weapon", FindScope(graph, ScopeKind.Subclass).Nodes);
        Assert.Contains("weapon:arc-weapon", FindScope(graph, ScopeKind.WithNeeds).Nodes);
        Assert.Equal(3, FindColumn(graph, "spark-of-beacons", ScopeKind.WithNeeds));
        Assert.Null(FindColumn(graph, "spark-of-beacons"));
    }

    [Fact]
    public void Dielectric_builds_on_the_Arc_Hunter_because_it_needs_Jolt()
    {
        var graph = DependencyGraphs.DrawGraph(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        Assert.DoesNotContain("dielectric", FindScope(graph, ScopeKind.WithNeeds).Nodes);
        Assert.Contains("dielectric", FindScope(graph, ScopeKind.WithDependents).Nodes);
        Assert.Contains(FindScope(graph, ScopeKind.WithDependents).Edges, edge => edge.From == "jolt" && edge.To == "dielectric" && edge.Kind == EdgeKind.Needs);
    }

    [Fact]
    public void The_json_holds_the_title_the_nodes_and_the_three_scopes()
    {
        var graph = Draw(Fragment("spark", OnGrenadeCast(Buff("amplified"))));

        var json = DependencyGraphs.RenderGraphJson(graph);

        Assert.StartsWith("""{"title":"Arc Hunter","nodes":{""", json);
        Assert.Contains("""{"id":"spark","name":"Spark","type":"subclassFragment","slot":null,"icon":null,"keyword":false,"file":"rules/test","source":null,""", json);
        Assert.Contains("""{"id":"withDependents","name":"\u002B what builds on it","nodes":[""", json);   // escaped: the page embeds it in a script
        Assert.Contains(""" "columns":{"amplified":2,"spark":1,"trigger:grenade":0}""".Trim(), json);
    }
}
