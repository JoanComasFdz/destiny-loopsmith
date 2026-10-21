using JoanComasFdz.AssertingThat;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>What a test checks on the tree.</summary>
public static class RuleTreeSnapshotAssertions
{
    /// <summary>Each of <paramref name="names"/> shows and is open: its arrow points down and what is under it shows.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasOpen(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        foreach (var item in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(item.Shown, $"\"{item.Name}\" doesn't show in the tree.");
            Assert.True(item.Open == true, $"\"{item.Name}\" is folded, but it should be open.");
            var under = that.InstanceToAssert.Items.Where(child => child.Ancestors.FirstOrDefault() == item.Name).ToArray();
            var hidden = under.Where(child => !child.Shown).Select(child => child.Name).ToArray();
            Assert.True(under.Length > 0 && hidden.Length == 0, $"\"{item.Name}\" is open, but {(under.Length == 0 ? "nothing" : Quote(hidden))} under it doesn't show.");
        }

        return that;
    }

    /// <summary>Each of <paramref name="names"/> shows and is folded: nothing under it shows.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasFolded(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        foreach (var item in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(item.Shown, $"\"{item.Name}\" doesn't show in the tree.");
            Assert.True(item.Open == false, $"\"{item.Name}\" is open, but it should be folded.");
            var shownUnder = that.InstanceToAssert.ListUnder(item.Name).Where(under => under.Shown).Select(under => under.Name).ToArray();
            Assert.True(shownUnder.Length == 0, $"\"{item.Name}\" is folded, but {Quote(shownUnder)} under it show.");
        }

        return that;
    }

    /// <summary>The items right under <paramref name="parent"/> are <paramref name="children"/>, in order, and they show.</summary>
    public static AssertingThat<RuleTreeSnapshot> ShowsUnder(this AssertingThat<RuleTreeSnapshot> that, string parent, params string[] children)
    {
        var under = that.InstanceToAssert.Items.Where(item => item.Ancestors.FirstOrDefault() == parent).ToArray();
        Assert.True(
            under.Select(item => item.Name).SequenceEqual(children),
            $"Right under \"{parent}\" are {Quote(under.Select(item => item.Name))}, but {Quote(children)} were expected.");
        var hidden = under.Where(item => !item.Shown).Select(item => item.Name).ToArray();
        Assert.True(hidden.Length == 0, $"{Quote(hidden)} under \"{parent}\" don't show in the tree.");
        return that;
    }

    /// <summary>None of <paramref name="names"/> shows in the tree: an item above it is folded.</summary>
    public static AssertingThat<RuleTreeSnapshot> HidesInTheTree(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        var shown = names.Select(that.InstanceToAssert.Find).Where(item => item.Shown).Select(item => item.Name).ToArray();
        Assert.True(shown.Length == 0, $"{Quote(shown)} still show in the tree, but should be hidden.");
        return that;
    }

    /// <summary>Every checkbox in the tree is ticked, none with a dash.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasEveryItemTicked(this AssertingThat<RuleTreeSnapshot> that)
    {
        var unticked = that.InstanceToAssert.Items.Where(item => !item.Ticked).Select(item => item.Name).ToArray();
        Assert.True(unticked.Length == 0, $"{Quote(unticked)} aren't ticked, but every item should be.");
        return that;
    }

    /// <summary>Every item under <paramref name="name"/> is ticked, with no dash.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasEveryItemUnderTicked(this AssertingThat<RuleTreeSnapshot> that, string name)
    {
        var unticked = that.InstanceToAssert.ListUnder(name).Where(item => !item.Ticked).Select(item => item.Name).ToArray();
        Assert.True(unticked.Length == 0, $"{Quote(unticked)} under \"{name}\" aren't ticked.");
        return that;
    }

    /// <summary>Every item under <paramref name="name"/> is unticked, with no dash.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasEveryItemUnderUnticked(this AssertingThat<RuleTreeSnapshot> that, string name)
    {
        var ticked = that.InstanceToAssert.ListUnder(name).Where(item => item.Ticked || item.Dashed).Select(item => item.Name).ToArray();
        Assert.True(ticked.Length == 0, $"{Quote(ticked)} under \"{name}\" are still ticked (or show a dash).");
        return that;
    }

    /// <summary>Each of <paramref name="names"/> is ticked, with no dash.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasTicked(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        foreach (var item in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(item.Ticked, item.Dashed ? $"\"{item.Name}\" shows a dash, but it should be ticked." : $"\"{item.Name}\" is unticked, but it should be ticked.");
        }

        return that;
    }

    /// <summary>Each of <paramref name="names"/> is unticked, with no dash.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasUnticked(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        foreach (var item in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(!item.Ticked && !item.Dashed, $"\"{item.Name}\" is {(item.Dashed ? "showing a dash" : "ticked")}, but it should be unticked.");
        }

        return that;
    }

    /// <summary>Each of <paramref name="names"/> shows a dash: some of the items under it are ticked, some not.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasDashed(this AssertingThat<RuleTreeSnapshot> that, params string[] names)
    {
        foreach (var item in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(item.Dashed, $"\"{item.Name}\" is {(item.Ticked ? "ticked" : "unticked")}, but it should show a dash.");
        }

        return that;
    }

    /// <summary><paramref name="name"/> is the one item highlighted as selected.</summary>
    public static AssertingThat<RuleTreeSnapshot> HasOnlySelected(this AssertingThat<RuleTreeSnapshot> that, string name)
    {
        var selected = that.InstanceToAssert.Items.Where(item => item.Selected).Select(item => item.Name).ToArray();
        Assert.True(selected.SequenceEqual([name]), $"The items selected are {Quote(selected)}, but only \"{name}\" should be.");
        return that;
    }

    private static string Quote(IEnumerable<string> names) =>
        names.Any() ? string.Join(", ", names.Select(name => $"\"{name}\"")) : "none";
}
