namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>
/// An item of the tree as its row shows it: its name, its kind (folder, file, element, rule), the names of the items it
/// sits under (nearest first), whether its row shows (no item above it is folded), whether it is open (null for a rule,
/// which has no arrow), whether its checkbox is ticked, whether it shows a dash (some under it ticked, some not) and
/// whether it is selected.
/// </summary>
public sealed record TreeItemShown(string Name, string Kind, string[] Ancestors, bool Shown, bool? Open, bool Ticked, bool Dashed, bool Selected);

/// <summary>The tree as shown: every item, in its order.</summary>
public sealed record RuleTreeSnapshot(TreeItemShown[] Items)
{
    /// <summary>The item named <paramref name="name"/>.</summary>
    public TreeItemShown Find(string name) =>
        Items.SingleOrDefault(item => item.Name == name) ?? throw new Xunit.Sdk.XunitException($"The tree has no item named \"{name}\".");

    /// <summary>Every item under the one named <paramref name="name"/>.</summary>
    public IEnumerable<TreeItemShown> ListUnder(string name) => Items.Where(item => item.Ancestors.Contains(name));
}
