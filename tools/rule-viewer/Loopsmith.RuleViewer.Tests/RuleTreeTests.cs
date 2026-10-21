using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// Every rule in the rules folder as a tree laid out like the folder: folders and files by name, folders first; a file's
/// elements in the file's order; each rule with exactly the flows its PNG draws, as written whatever build it needs.
/// </summary>
public sealed class RuleTreeTests
{
    private static readonly RuleTree Tree = RuleViewing.DrawTree(RepoCatalog.Catalog);

    private static ImmutableArray<RuleEntry> ListRules(RuleTree tree) =>
        [.. RuleTreeDrawing.ListFiles(tree.Root).SelectMany(file => file.Elements).SelectMany(element => element.Rules)];

    private static RuleFile FindFile(string path) => RuleTreeDrawing.ListFiles(Tree.Root).Single(file => file.Path == path);

    [Fact]
    public void Every_rule_and_passive_in_the_rules_folder_is_in_the_tree_with_the_flows_its_PNG_draws()
    {
        var expected = RepoCatalog.Catalog.Elements.Values
            .DistinctBy(element => element.Id)
            .SelectMany(element => element.Rules.Select(rule => element.ToRuleName(rule.Id)).Concat(element.Passives.Select(passive => element.ToRuleName(passive.Id))))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var rules = ListRules(Tree).GroupBy(rule => rule.Name).ToImmutableArray();

        Assert.Equal(expected, rules.Select(rule => rule.Key).Order(StringComparer.Ordinal));
        Assert.All(rules, rule => Assert.Equal(
            RuleViewing.PickRuleFlows(RepoCatalog.Catalog, rule.Key).Match(ok => ok.Value.Select(flow => flow.Id), _ => []),
            rule.SelectMany(entry => entry.Flows).Select(flow => flow.Id).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void An_enhanced_trait_comes_right_after_its_perk_with_its_perks_rules()
    {
        var perks = FindFile("weapons/perks.yaml").Elements.Select(element => element.Id).ToImmutableArray();
        var slice = FindFile("weapons/perks.yaml").Elements.Single(element => element.Id == "slice-enhanced");

        Assert.Equal(perks.IndexOf("slice") + 1, perks.IndexOf("slice-enhanced"));
        Assert.All(slice.Rules, rule => Assert.StartsWith("weapons-perks-slice-", rule.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Folders_come_before_files_each_by_name()
    {
        var hunter = Tree.Root.Folders.Single(folder => folder.Name == "hunter");

        Assert.Equal(["all-classes", "armor-sets", "artifact", "exotics", "hunter", "keywords", "mods", "weapons"], Tree.Root.Folders.Select(folder => folder.Name));
        Assert.Equal(["arc"], hunter.Folders.Select(folder => folder.Name));
        Assert.Equal(["class-abilities.yaml", "exotic-armor.yaml"], hunter.Files.Select(file => file.Name));
        Assert.Equal("hunter/arc/aspects.yaml", RuleTreeDrawing.ListFiles(hunter).First().Path);
        Assert.Equal("hunter/exotic-armor.yaml", RuleTreeDrawing.ListFiles(hunter).Last().Path);
    }

    [Fact]
    public void A_files_elements_keep_the_files_order()
    {
        var melees = FindFile("hunter/arc/melees.yaml");
        var lines = RepoCatalog.Files.Single(file => file.Path == "hunter/arc/melees.yaml").Text.Split('\n');
        var firstLines = melees.Elements.Select(element => Array.FindIndex(lines, line => line.TrimEnd() == $"  - id: {element.Id}")).ToImmutableArray();

        Assert.DoesNotContain(-1, firstLines);
        Assert.Equal(firstLines.Order(), firstLines);
    }

    [Fact]
    public void A_rule_that_needs_the_builds_Super_reads_as_written()
    {
        var siphon = FindFile("mods/armor.yaml").Elements.Single(element => element.Id == "harmonic-siphon");

        Assert.Equal(
            ["Weapon of your Super's element 2+ kills within 3s", "Weapon of your Super's element fewer than 2 kills"],
            siphon.Rules.SelectMany(rule => rule.Flows).Select(flow => flow.Trigger.Name));
    }

    [Fact]
    public void An_element_is_named_with_its_kind_and_icon_as_its_flows_draw_it()
    {
        var siphon = FindFile("mods/armor.yaml").Elements.Single(element => element.Id == "harmonic-siphon");

        Assert.Equal(("Armor mod", "Harmonic Siphon"), (siphon.Box.Title, siphon.Box.Name));
        Assert.Equal(siphon.Rules[0].Flows[0].Conditions[0].Icon, siphon.Box.Icon);
    }

    [Fact]
    public void An_element_with_no_rule_and_a_file_with_none_are_left_out()
    {
        var inert = TestCatalog.Element("inert", ElementKind.Fragment, []) with { RuleFile = "warlock/void/fragments.yaml" };
        var spark = TestCatalog.Element("spark", ElementKind.Fragment, [TestCatalog.On(new Trigger.Finisher(), TestCatalog.Buff("amplified"))]) with { RuleFile = "all-classes/arc/fragments.yaml" };
        var tree = RuleViewing.DrawTree(RepoCatalog.Catalog with { Glossary = TestCatalog.Glossary, Elements = new[] { inert, spark }.ToImmutableDictionary(element => element.Id) });

        Assert.Equal(["all-classes"], tree.Root.Folders.Select(folder => folder.Name));
        Assert.Equal(["spark"], RuleTreeDrawing.ListFiles(tree.Root).Single().Elements.Select(element => element.Id));
    }
}
