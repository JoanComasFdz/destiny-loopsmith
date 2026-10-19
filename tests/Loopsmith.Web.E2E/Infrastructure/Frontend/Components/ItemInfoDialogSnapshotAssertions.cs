using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the item details popup.</summary>
public static class ItemInfoDialogSnapshotAssertions
{
    /// <summary>The popup is titled with <paramref name="weapon"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, Weapon weapon) =>
        that.IsTitled(weapon.ToShownText());

    /// <summary>The popup is titled with <paramref name="fragment"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, Fragment fragment) =>
        that.IsTitled(fragment.ToShownText());

    /// <summary>The popup is titled with <paramref name="perk"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, WeaponPerk perk) =>
        that.IsTitled(perk.ToShownText());

    /// <summary>The popup is titled with <paramref name="keyword"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, Keyword keyword) =>
        that.IsTitled(keyword.ToShownText());

    /// <summary>Its "<paramref name="title"/>" card reads only <paramref name="lines"/>, in order.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasRightThen(this AssertingThat<ItemInfoDialogSnapshot> that, string title, params string[] lines)
    {
        var cards = that.InstanceToAssert.RightThen;
        Assert.True(cards.Length == 1 && cards[0].Title == title,
            $"The item details popup has {Describe(cards.Select(card => card.Title))} cards, but one \"{title}\" card was expected.");
        var shown = cards[0].Lines;
        Assert.True(shown.SequenceEqual(lines), $"Its \"{title}\" card reads {Describe(shown)}, but {Describe(lines)} was expected.");
        return that;
    }

    /// <summary>It has no "Right then" card: it says nothing about a step.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasNoRightThen(this AssertingThat<ItemInfoDialogSnapshot> that)
    {
        var cards = that.InstanceToAssert.RightThen;
        Assert.True(cards.IsEmpty, $"The item details popup has {Describe(cards.Select(card => card.Title))} cards, but no \"Right then\" card was expected.");
        return that;
    }

    private static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, string expected)
    {
        var title = that.InstanceToAssert.Title;
        Assert.True(title == expected, $"The item details popup is titled \"{title}\", but \"{expected}\" was expected.");
        return that;
    }

    private static string Describe(IEnumerable<string> texts) =>
        texts.Any() ? string.Join(" and ", texts.Select(text => $"\"{text}\"")) : "nothing";

    /// <summary>The type line under its title reads <paramref name="expected"/>.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasTypeLine(this AssertingThat<ItemInfoDialogSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.TypeLine;
        Assert.True(shown == expected, $"The item details popup's type line reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }
}
