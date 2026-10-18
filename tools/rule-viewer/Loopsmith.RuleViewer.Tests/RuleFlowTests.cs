using System.Collections.Immutable;
using Loopsmith.RuleViewer.Core;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Tests.Support;
using static Loopsmith.RuleViewer.Tests.Support.TestCatalog;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// The rules as flows, one claim each: what sets a rule off ➜ what must be in place ➜ what it gives, grouped by what
/// sets it off. A flow is one scenario to write, so a rule that names several triggers is one flow per trigger.
/// </summary>
public sealed class RuleFlowTests
{
    private static readonly DamageSource AnySource = new DamageSource.AnySource();

    private static BuildElement Fragment(string id, params Rule[] rules) =>
        Element(id, ElementKind.Fragment, [.. rules]) with { Class = Optional.Some(GuardianClass.Hunter) };

    private static SubclassRules Draw(params BuildElement[] elements) =>
        RuleViewing.DrawRules(
            RepoCatalog.Catalog with { Glossary = Glossary, Elements = elements.ToImmutableDictionary(element => element.Id) },
            GuardianClass.Hunter,
            Subclass.Arc);

    private static RuleScope FindScope(SubclassRules rules, ScopeKind kind = ScopeKind.WithDependents) => rules.Scopes.Single(scope => scope.Kind == kind);

    private static ImmutableArray<RuleFlow> ListFlows(SubclassRules rules) => [.. FindScope(rules).Groups.SelectMany(group => group.Flows)];

    private static RuleFlow FindFlow(SubclassRules rules, string id) => ListFlows(rules).Single(flow => flow.Id == id);

    private static ImmutableArray<string> ListTriggers(SubclassRules rules) => [.. ListFlows(rules).Select(flow => flow.Trigger.Name)];

    private static string Describe(RuleBox box) => box.Caption.Length == 0 ? box.Name : $"{box.Name} ({box.Caption})";

    private static Rule OnGrenadeCast(params Outcome[] then) => On(new Trigger.AbilityCast(AbilityKind.Grenade), then);

    private static Rule OnHit(DamageSource via, params Outcome[] then) => On(new Trigger.Damage(via), then);

    private static PassiveRule Always(Passive passive, params Condition[] when) => new(passive, [.. when], Optional.None<string>());

    [Fact]
    public void Each_rule_and_each_passive_is_one_flow()
    {
        var spark = Fragment("spark", OnGrenadeCast(Buff("amplified")), On(new Trigger.Finisher(), Debuff("jolt"))) with
        {
            Passives = [Always(new Passive.ModifyStat(StatKind.Grenade, new GameValue.Known(20m)))],
        };

        var rules = Draw(spark);

        Assert.Equal(["spark#0", "spark#1", "spark#p0"], ListFlows(rules).Select(flow => flow.Id).Order(StringComparer.Ordinal));
        Assert.Equal("Always", FindFlow(rules, "spark#p0").Trigger.Name);
        Assert.Equal("Grenade cast", FindFlow(rules, "spark#0").Trigger.Name);
        Assert.Equal("Finisher", FindFlow(rules, "spark#1").Trigger.Name);
    }

    [Fact]
    public void Any_ability_is_one_flow_per_ability_that_deals_damage_never_the_class_ability()
    {
        var rules = Draw(Fragment("spark", On(new Trigger.KillAny(new DamageSource.AnyAbility()), Buff("amplified"))));

        Assert.Equal(["Melee kill", "Sliding melee kill", "Grenade kill", "Super kill"], ListTriggers(rules));
        Assert.Equal(["spark#0.0", "spark#0.1", "spark#0.2", "spark#0.3"], ListFlows(rules).Select(flow => flow.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Any_melee_is_a_Melee_flow_and_a_Sliding_melee_flow()
    {
        var rules = Draw(Fragment("spark", OnHit(new DamageSource.AbilityOf(AbilityKind.Melee), Buff("amplified"))));

        Assert.Equal(["Melee hit", "Sliding melee hit"], ListTriggers(rules));
    }

    [Fact]
    public void A_punch_is_only_a_Melee_flow()
    {
        var rules = Draw(Fragment("spark", OnHit(new DamageSource.AbilityUsedAs(AbilityKind.Melee, AbilityInput.Press), Buff("amplified"))));

        Assert.Equal(["Melee hit"], ListTriggers(rules));
    }

    [Fact]
    public void A_sliding_melee_is_only_a_Sliding_melee_flow()
    {
        var rules = Draw(Fragment("spark", OnHit(new DamageSource.AbilityUsedAs(AbilityKind.Melee, AbilityInput.Slide), Buff("amplified"))));

        Assert.Equal(["Sliding melee hit"], ListTriggers(rules));
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
    public void A_flow_names_the_element_equipped_first()
    {
        var flow = FindFlow(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")))), "spark#0");

        Assert.Equal("Subclass fragment", flow.Conditions[0].Title);
        Assert.Equal("Spark (equipped)", Describe(flow.Conditions[0]));
    }

    [Fact]
    public void A_keywords_flow_shows_no_element_and_needs_nothing_more_when_nothing_else_is_in_place()
    {
        var jolt = Element("jolt", ElementKind.Keyword, [On(new Trigger.KillDebuffed(AnySource, [Status("jolt")]), Buff("amplified")), On(new Trigger.Finisher(), Buff("amplified"))]);

        var rules = Draw(Fragment("spark", OnGrenadeCast(Debuff("jolt"))), jolt);

        Assert.Equal(["Jolt (on the target)"], FindFlow(rules, "rule:jolt#0").Conditions.Select(Describe));
        Assert.Empty(FindFlow(rules, "rule:jolt#1").Conditions);
    }

    [Fact]
    public void Each_state_a_flow_needs_has_its_caption()
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

        var flow = FindFlow(Draw(spark), "spark#0");

        Assert.Equal(
            [
                "Spark (equipped)", "Jolt (on the target)", "Amplified (on you)", "Bolt Charge (at max stacks)", "Armor Charge (2+ stacks)",
                "Amplified (not on you)", "Bolt Charge (below max stacks)", "Class ability (under way)",
            ],
            flow.Conditions.Select(Describe));
    }

    [Fact]
    public void Results_go_strikes_effects_buffs_debuffs_pickups_summons_then_what_the_flow_ends()
    {
        var flow = FindFlow(
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
            flow.Results.Select(box => box.Role));
        Assert.Equal(
            [BoxTone.Strike, BoxTone.Effect, BoxTone.Buff, BoxTone.Debuff, BoxTone.Pickup, BoxTone.Buff],
            flow.Results.Select(box => box.Tone));
    }

    [Fact]
    public void Amounts_show_as_authored()
    {
        var flow = FindFlow(
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
            flow.Results.Select(Describe));
    }

    [Fact]
    public void A_passive_adds_to_the_status_it_boosts()
    {
        var spark = Fragment("spark") with { Passives = [Always(new Passive.ExtraStacks(Status("bolt-charge"), StackCount.From(1)))] };

        var flow = FindFlow(Draw(spark, Fragment("ember", OnGrenadeCast(Buff("bolt-charge")))), "spark#p0");

        Assert.Equal(["Bolt Charge (adds to · +1 stack)"], flow.Results.Select(Describe));
    }

    [Fact]
    public void A_chance_rule_is_marked()
    {
        var flow = FindFlow(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")) with { Likelihood = Likelihood.Chance })), "spark#0");

        Assert.True(flow.ByChance);
    }

    [Theory]
    [InlineData(GuardianClass.Hunter, "gamblers-dodge", 426473317u, "Dodge")]
    [InlineData(GuardianClass.Titan, "towering-barricade", 489583096u, "Barricade")]
    [InlineData(GuardianClass.Warlock, "healing-rift", 383852320u, "Rift")]
    public void The_class_ability_trigger_is_named_Class_ability_with_the_classs_own_word_and_its_class_abilitys_icon(
        GuardianClass guardianClass, string classAbility, uint hash, string word)
    {
        var element = Element(classAbility, ElementKind.ClassAbility, [On(new Trigger.AbilityCast(AbilityKind.ClassAbility), Buff("amplified"))]) with
        {
            Class = Optional.Some(guardianClass),
            Hashes = [ItemHash.From(hash)],
        };
        var catalog = RepoCatalog.Catalog with { Glossary = Glossary, Elements = ImmutableDictionary<ElementId, BuildElement>.Empty.Add(element.Id, element) };

        var rules = RuleViewing.DrawRules(catalog, guardianClass, Subclass.Arc);
        var group = Assert.Single(FindScope(rules).Groups);
        var trigger = Assert.Single(group.Flows).Trigger;

        Assert.Equal("Class ability", group.Name);
        Assert.Equal(("Trigger", "Class ability", word), (trigger.Title, trigger.Name, trigger.Caption));
        Assert.Equal(new RuleIcon.Picture("https://www.bungie.net" + catalog.FindElementItem(element.Id).Bind(item => item.Icon).Match(icon => icon.Value, _ => "")), trigger.Icon);
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

        var groups = FindScope(Draw(spark)).Groups.Select(group => $"{group.Name} {group.Flows.Length}");

        Assert.Equal(["Class ability 1", "Sliding melee 1", "Grenade 1", "Super 1", "Arc weapon 1", "Critical health 1", "Amplified 1", "Any 1", "Always on 1"], groups);
    }

    [Fact]
    public void A_flow_that_needs_a_weapon_is_only_in_the_scopes_with_the_weapon_in_view()
    {
        var rules = Draw(Fragment("beacon", On(new Trigger.KillAny(new DamageSource.WeaponOfType(DamageType.Arc)), Debuff("jolt"))));

        Assert.Equal("Arc weapon kill", Assert.Single(FindScope(rules, ScopeKind.Subclass).Groups.SelectMany(group => group.Flows)).Trigger.Name);
        Assert.Equal(["Arc subclass", "+ what it needs from outside", "+ what builds on it"], rules.Scopes.Select(scope => scope.Name));
    }

    [Fact]
    public void The_kind_titles_carry_no_keyword_suffix()
    {
        var rules = RuleViewing.DrawRules(RepoCatalog.Catalog, GuardianClass.Hunter, Subclass.Arc);

        var titles = ListFlows(rules).SelectMany(flow => flow.Conditions.Concat(flow.Results).Append(flow.Trigger)).Select(box => box.Title).ToImmutableHashSet();

        Assert.DoesNotContain(titles, title => title.Contains("keyword", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Subclass ability · Class ability", titles);
        Assert.Contains("Armor mod", titles);
    }

    [Fact]
    public void The_json_holds_the_scopes_groups_flows_and_boxes()
    {
        var json = RuleViewing.RenderRulesJson(Draw(Fragment("spark", OnGrenadeCast(Buff("amplified")))));

        Assert.StartsWith("""{"title":"Arc Hunter","scopes":[{"id":"subclass","name":"Arc subclass","groups":[{"key":"trigger:grenade","name":"Grenade",""", json);
        Assert.Contains(""""flows":[{"id":"spark#0","element":"spark","file":null,"reason":null,"chance":false,"trigger":{"node":"trigger:grenade","title":"Trigger","name":"Grenade cast","""", json);
        Assert.Contains(""""tone":"fragment","role":"condition","icon":{"kind":"blank"}"""", json);
    }
}
