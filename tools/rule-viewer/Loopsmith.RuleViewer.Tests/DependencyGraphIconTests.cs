using System.Collections.Immutable;
using Loopsmith.RuleViewer.Core;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Tests.Support;
using static Loopsmith.RuleViewer.Tests.Support.TestCatalog;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>Each node of the dependency graph carries the icon the web app would show for it (a bungie.net address), or none.</summary>
public sealed class DependencyGraphIconTests
{
    private const string Host = "https://www.bungie.net";

    private static readonly RuleCatalog Catalog = RepoCatalog.Catalog;

    private static readonly DependencyGraph Graph = RuleViewing.DrawGraph(Catalog, GuardianClass.Hunter, Subclass.Arc);

    private static string? FindIcon(DependencyGraph graph, string id) => graph.Nodes[id].Icon.Match<string?>(icon => icon.Value, _ => null);

    private static string StatIcon(StatKind stat) => Host + Catalog.Manifest.ArmorStats[stat].Icon;

    private static string TraitIcon(string status) =>
        Host + Catalog.Manifest.FindTraitIcon(Catalog.Glossary.Statuses[StatusId.From(status)].Trait.Match(trait => trait.Value, _ => throw new InvalidOperationException(status)))
            .Match(icon => icon.Value, _ => throw new InvalidOperationException(status));

    private static string ElementIcon(string id) =>
        Host + Catalog.FindElementItem(ElementId.From(id)).Bind(item => item.Icon).Match(icon => icon.Value, _ => throw new InvalidOperationException(id));

    /// <summary>A small catalog on the repository's glossary and manifest.</summary>
    private static DependencyGraph Draw(params BuildElement[] elements) =>
        RuleViewing.DrawGraph(Catalog with { Elements = elements.ToImmutableDictionary(element => element.Id) }, GuardianClass.Hunter, Subclass.Arc);

    private static BuildElement Fragment(string id, params Rule[] rules) =>
        Element(id, ElementKind.Fragment, [.. rules]) with { Class = Optional.Some(GuardianClass.Hunter) };

    [Fact]
    public void An_element_has_its_manifest_items_icon() =>
        Assert.Equal(ElementIcon("flashbang-grenade"), FindIcon(Graph, "flashbang-grenade"));

    [Fact]
    public void A_keywords_rule_node_and_state_node_have_its_traits_icon()
    {
        Assert.Equal(TraitIcon("jolt"), FindIcon(Graph, "rule:jolt"));
        Assert.Equal(TraitIcon("jolt"), FindIcon(Graph, "jolt"));
    }

    [Fact]
    public void A_status_with_no_trait_that_shares_an_elements_id_has_the_elements_icon() =>
        Assert.Equal(ElementIcon("lethal-current"), FindIcon(Graph, "status:lethal-current"));

    [Fact]
    public void A_status_with_no_trait_and_no_element_item_has_no_icon()
    {
        Assert.Null(FindIcon(Graph, "super"));   // its keyword element has no manifest item
        Assert.Null(FindIcon(Graph, "rule:super"));
    }

    [Fact]
    public void A_keyword_with_no_trait_has_its_elements_item_icon()
    {
        var flashbang = Catalog.Elements[ElementId.From("flashbang-grenade")];
        var orb = Catalog.Elements[ElementId.From("orb-of-power")] with { Hashes = flashbang.Hashes };
        var graph = Draw(Fragment("spark", On(new Trigger.AbilityCast(AbilityKind.Grenade), new Outcome.Spawn(Pickup("orb-of-power"), 1))), orb);

        Assert.Equal(ElementIcon("flashbang-grenade"), FindIcon(graph, "orb-of-power"));
        Assert.Equal(ElementIcon("flashbang-grenade"), FindIcon(graph, "rule:orb-of-power"));
    }

    [Fact]
    public void A_strike_with_no_trait_has_the_icon_of_the_element_it_is_named_for() =>
        Assert.Equal(ElementIcon("lethal-current"), FindIcon(Graph, "strike:lethal-current"));

    [Fact]
    public void A_status_the_game_draws_has_no_address_so_no_icon() =>
        Assert.Null(FindIcon(Graph, "unraveling-rounds"));

    [Fact]
    public void A_strike_has_the_icon_of_what_it_strikes_with() =>
        Assert.Equal(TraitIcon("bolt-charge"), FindIcon(Graph, "strike:bolt-charge"));

    [Theory]
    [InlineData("trigger:grenade", StatKind.Grenade)]
    [InlineData("trigger:melee", StatKind.Melee)]
    [InlineData("trigger:sliding-melee", StatKind.Melee)]
    [InlineData("trigger:super", StatKind.Super)]
    [InlineData("trigger:critical-health", StatKind.Health)]
    [InlineData("weapon:weapon", StatKind.Weapons)]
    [InlineData("effect:energy-grenade", StatKind.Grenade)]
    [InlineData("effect:energy-melee", StatKind.Melee)]
    [InlineData("effect:energy-class-ability", StatKind.Class)]
    [InlineData("effect:energy-super", StatKind.Super)]
    [InlineData("effect:health", StatKind.Health)]
    public void A_trigger_weapon_or_effect_has_its_stats_icon(string id, StatKind stat) =>
        Assert.Equal(StatIcon(stat), FindIcon(Graph, id));

    [Theory]
    [InlineData("trigger:class-ability")]
    [InlineData("trigger:air-class-ability")]
    public void The_class_ability_has_the_icon_of_the_classs_first_class_ability(string id) =>
        Assert.Equal(ElementIcon("gamblers-dodge"), FindIcon(Graph, id));

    [Fact]
    public void The_class_ability_has_the_Class_stats_icon_when_the_rules_have_no_class_ability() =>
        Assert.Equal(StatIcon(StatKind.Class), FindIcon(Draw(Fragment("spark", On(new Trigger.AbilityCast(AbilityKind.ClassAbility)))), "trigger:class-ability"));

    [Fact]
    public void The_finisher_has_the_classs_finisher_icon() =>
        Assert.Equal(Host + Catalog.FindClassFinisher(GuardianClass.Hunter).Bind(item => item.Icon).Match(icon => icon.Value, _ => ""), FindIcon(Graph, "trigger:finisher"));

    [Fact]
    public void An_element_weapon_has_its_damage_types_icon() =>
        Assert.Equal(Host + Catalog.Manifest.DamageTypeIcons[DamageType.Arc], FindIcon(Graph, "weapon:arc-weapon"));

    [Fact]
    public void A_stat_effect_has_that_stats_icon()
    {
        var graph = Draw(Fragment("spark", On(new Trigger.AbilityCast(AbilityKind.Grenade))) with
        {
            Passives = [new PassiveRule(RuleId.From("test-rule-1"), new Passive.ModifyStat(StatKind.Melee, new GameValue.Known(20m)), [], Optional.None<string>())],
        });

        Assert.Equal(StatIcon(StatKind.Melee), FindIcon(graph, "effect:stat-melee"));
    }

    [Fact]
    public void Shield_broken_reload_a_power_weapon_and_the_other_effects_have_no_icon()
    {
        var graph = Draw(
            Fragment("spark", On(new Trigger.ShieldBroken(new DamageSource.WeaponInSlot(WeaponSlot.Power)), new Outcome.ReloadWeapon(new DamageSource.ThisWeapon()))),
            Fragment("ember", On(new Trigger.Reload(new DamageSource.ThisWeapon()), new Outcome.ModifyDamage(new DamageSource.AnySource(), new GameValue.Known(0.1m)))));

        Assert.Null(FindIcon(graph, "trigger:shield-broken"));
        Assert.Null(FindIcon(graph, "trigger:reload"));
        Assert.Null(FindIcon(graph, "weapon:power-weapon"));
        Assert.Null(FindIcon(graph, "effect:reload"));
        Assert.Null(FindIcon(graph, "effect:damage-bonus"));
    }
}
