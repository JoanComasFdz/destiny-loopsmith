using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the item details popup.</summary>
public static class ItemInfoDialogSnapshotAssertions
{
    /// <summary>The popup is titled with <paramref name="weapon"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, Weapon weapon)
    {
        var title = that.InstanceToAssert.Title;
        Assert.True(title == weapon.ToShownText(), $"The item details popup is titled \"{title}\", but \"{weapon.ToShownText()}\" was expected.");
        return that;
    }

    /// <summary>The type line under its title reads <paramref name="expected"/>.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasTypeLine(this AssertingThat<ItemInfoDialogSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.TypeLine;
        Assert.True(shown == expected, $"The item details popup's type line reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }
}
