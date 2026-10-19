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

    /// <summary>The popup is titled with <paramref name="keyword"/>'s name.</summary>
    public static AssertingThat<ItemInfoDialogSnapshot> IsTitled(this AssertingThat<ItemInfoDialogSnapshot> that, Keyword keyword)
    {
        var title = that.InstanceToAssert.Title;
        Assert.True(title == keyword.ToShownText(), $"The item details popup is titled \"{title}\", but \"{keyword.ToShownText()}\" was expected.");
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
    public static AssertingThat<ItemInfoDialogSnapshot> ReadsUnder(this AssertingThat<ItemInfoDialogSnapshot> that, string role, Aspect source, params string[] texts)
    {
        var roles = that.InstanceToAssert.InBuild;
        var found = roles.Where(shown => shown.Heading == role).ToArray();
        Assert.True(found.Length > 0, $"The item details popup shows no \"{role}\" in this build (it shows {Describe(roles.Select(shown => shown.Heading))}), but one was expected.");
        var shownTexts = found.SelectMany(shown => shown.Items).Where(item => item.Source == source.ToShownText()).Select(item => item.Text).ToArray();
        Assert.True(shownTexts.SequenceEqual(texts), $"Under \"{role}\" {source.ToShownText()} reads {Describe(shownTexts)}, but {Describe(texts)} was expected.");
        return that;
    }

    private static string Describe(IEnumerable<string> texts)
    {
        var quoted = texts.Select(text => $"\"{text}\"").ToArray();
        return quoted.Length == 0 ? "nothing" : string.Join(", ", quoted);
    }
}
