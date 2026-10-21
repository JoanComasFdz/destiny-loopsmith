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

    /// <summary>The popup is titled with <paramref name="bonus"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, ArmorSetBonus bonus) =>
        that.IsTitled(bonus.ToShownText());

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

    /// <summary>It has no "In this build" section: nothing else in the build interacts with it.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasNoInBuild(this AssertingThat<ItemInfoDialogSnapshot> that)
    {
        var roles = that.InstanceToAssert.InBuild;
        Assert.True(roles.IsEmpty, $"The item details popup shows {Describe(roles.Select(role => role.Heading))} in this build, but no \"In this build\" section was expected.");
        return that;
    }

    private static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, string expected)
    {
        var title = that.InstanceToAssert.Title;
        Assert.True(title == expected, $"The item details popup is titled \"{title}\", but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>The type line under its title reads <paramref name="expected"/>.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> HasTypeLine(this AssertingThat<ItemInfoDialogSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.TypeLine;
        Assert.True(shown == expected, $"The item details popup's type line reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }

    /// <summary>Under the "In this build" role <paramref name="role"/> ("While Amplified"), <paramref name="source"/>'s items read exactly <paramref name="texts"/>, in order.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> ReadsUnder(this AssertingThat<ItemInfoDialogSnapshot> that, string role, Aspect source, params string[] texts) =>
        that.ReadsUnderFrom(role, source.ToShownText(), texts);

    /// <summary>Under the "In this build" role <paramref name="role"/> ("While Armor Charge"), <paramref name="source"/>'s items read exactly <paramref name="texts"/>, in order.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> ReadsUnder(this AssertingThat<ItemInfoDialogSnapshot> that, string role, ArmorMod source, params string[] texts) =>
        that.ReadsUnderFrom(role, source.ToShownText(), texts);

    private static AssertingThat<ItemInfoDialogSnapshot> ReadsUnderFrom(this AssertingThat<ItemInfoDialogSnapshot> that, string role, string source, params string[] texts)
    {
        var roles = that.InstanceToAssert.InBuild;
        var found = roles.Where(shown => shown.Heading == role).ToArray();
        Assert.True(found.Length > 0, $"The item details popup shows no \"{role}\" in this build (it shows {Describe(roles.Select(shown => shown.Heading))}), but one was expected.");
        var shownTexts = found.SelectMany(shown => shown.Items).Where(item => item.Source == source).Select(item => item.Text).ToArray();
        Assert.True(shownTexts.SequenceEqual(texts), $"Under \"{role}\" {source} reads {Describe(shownTexts)}, but {Describe(texts)} was expected.");
        return that;
    }

    private static string Describe(IEnumerable<string> texts)
    {
        var quoted = texts.Select(text => $"\"{text}\"").ToArray();
        return quoted.Length == 0 ? "nothing" : string.Join(", ", quoted);
    }
}
