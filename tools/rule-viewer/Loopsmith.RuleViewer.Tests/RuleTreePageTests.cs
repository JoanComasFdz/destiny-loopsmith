using System.Text.RegularExpressions;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Page;
using Loopsmith.RuleViewer.Tests.Support;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// The rules page: the tree, a folder explorer whose every item has an arrow (but a rule), a checkbox and a name, folders
/// and files open and elements folded; beside it each rule's flows under the tree item they belong to.
/// </summary>
public sealed partial class RuleTreePageTests
{
    private static readonly RuleTree Tree = RuleViewing.DrawTree(RepoCatalog.Catalog);

    private static readonly string Page = RulePage.RenderPage(Tree, new PageAssets("", ""));

    private static int CountRules() =>
        RuleTreeDrawing.ListFiles(Tree.Root).Sum(file => file.Elements.Sum(element => element.Rules.Length));

    [Fact]
    public void Every_tree_item_has_a_ticked_checkbox_and_a_name_and_all_but_a_rule_an_arrow()
    {
        var items = TreeItem().Matches(Page);

        Assert.All(items, item => Assert.Contains("<input type=\"checkbox\" checked", item.Value));
        Assert.All(items, item => Assert.Contains("class=\"tname\"", item.Value));
        Assert.All(items.Where(item => item.Groups["kind"].Value != "rule"), item => Assert.Contains("<button type=\"button\" class=\"twisty\"", item.Value));
        Assert.Equal(CountRules(), items.Count(item => item.Groups["kind"].Value == "rule"));
    }

    [Fact]
    public void Folders_and_files_start_open_and_elements_folded()
    {
        var items = TreeItem().Matches(Page);

        Assert.All(items.Where(item => item.Groups["kind"].Value is "folder" or "file"), item => Assert.Contains("aria-expanded=\"true\"", item.Value));
        Assert.All(items.Where(item => item.Groups["kind"].Value == "element"), item => Assert.Contains("aria-expanded=\"false\"", item.Value));
    }

    [Fact]
    public void Each_rule_of_the_tree_has_its_flows_beside_it()
    {
        var names = RuleItem().Matches(Page).Select(match => match.Groups["name"].Value).ToHashSet();

        Assert.Equal(CountRules(), FlowsOfRule().Matches(Page).Count);
        Assert.All(FlowsOfRule().Matches(Page), flows => Assert.Contains(flows.Groups["name"].Value, names));
        Assert.Contains("<div class=\"rrule\" data-item=\"rule:harmonic-siphon:mods-armor-harmonic-siphon-1\">", Page);
    }

    [GeneratedRegex("<li role=\"treeitem\" class=\"titem (?<kind>folder|file|element|rule)\"[^>]*><div class=\"trow\">.*?</div>")]
    private static partial Regex TreeItem();

    [GeneratedRegex("<li role=\"treeitem\" class=\"titem rule\" data-item=\"rule:(?<name>[a-z0-9-]+:[a-z0-9-]+)\"")]
    private static partial Regex RuleItem();

    [GeneratedRegex("<div class=\"rrule\" data-item=\"rule:(?<name>[a-z0-9-]+:[a-z0-9-]+)\">")]
    private static partial Regex FlowsOfRule();
}
