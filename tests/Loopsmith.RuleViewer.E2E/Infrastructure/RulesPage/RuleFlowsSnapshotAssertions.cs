using JoanComasFdz.AssertingThat;

namespace Loopsmith.RuleViewer.E2E.Infrastructure.RulesPage;

/// <summary>What a test checks on the flows.</summary>
public static class RuleFlowsSnapshotAssertions
{
    // Where a part brought to the top may start, from the window's top: a pixel above it (the scroll lands on fractions of
    // a pixel) to its scroll margin and a little more below it.
    private const double AboveTheTop = -2;
    private const double TopOfTheView = 24;

    /// <summary>The flows of each of <paramref name="names"/> show, each with its wires drawn to where its boxes are.</summary>
    public static AssertingThat<RuleFlowsSnapshot> ShowsWithItsWires(this AssertingThat<RuleFlowsSnapshot> that, params string[] names)
    {
        foreach (var part in names.Select(that.InstanceToAssert.Find))
        {
            Assert.True(part.Shown, $"The flows of \"{part.Name}\" are hidden, but they should show.");
            Assert.True(part.Wired, $"The flows of \"{part.Name}\" show, but their wires don't reach their boxes.");
        }

        return that;
    }

    /// <summary>No flow of any of <paramref name="names"/> shows.</summary>
    public static AssertingThat<RuleFlowsSnapshot> Hides(this AssertingThat<RuleFlowsSnapshot> that, params string[] names)
    {
        var shown = names.Select(that.InstanceToAssert.Find).Where(part => part.Shown).Select(part => $"\"{part.Name}\"").ToArray();
        Assert.True(shown.Length == 0, $"The flows of {string.Join(", ", shown)} show, but they should be hidden.");
        return that;
    }

    /// <summary>The flows of <paramref name="name"/> start at the top of the view.</summary>
    public static AssertingThat<RuleFlowsSnapshot> HasAtTheTopOfTheView(this AssertingThat<RuleFlowsSnapshot> that, string name)
    {
        var part = that.InstanceToAssert.Find(name);
        Assert.True(part.Shown, $"The flows of \"{name}\" are hidden, so they can't be at the top of the view.");
        Assert.True(
            part.Top is >= AboveTheTop and <= TopOfTheView,
            $"The flows of \"{name}\" start {part.Top:0} px from the window's top, but they should be at the top of the view ({AboveTheTop:0} to {TopOfTheView:0} px).");
        return that;
    }
}
