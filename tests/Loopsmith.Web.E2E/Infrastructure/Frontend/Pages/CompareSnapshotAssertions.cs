using System.Collections.Immutable;
using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>What a test checks on the Compare screen.</summary>
public static class CompareSnapshotAssertions
{
    /// <summary>Compare says "Nothing to compare yet".</summary>
    public static AssertingThat<CompareSnapshot> ShowsNothingToCompare(this AssertingThat<CompareSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.ShowsNothingToCompare, "Compare doesn't show \"Nothing to compare yet\", but it was expected.");
        return that;
    }

    /// <summary>Compare shows no loop picker, on either side.</summary>
    public static AssertingThat<CompareSnapshot> ShowsNoLoopPicker(this AssertingThat<CompareSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.LeftOffers.IsEmpty && shown.RightOffers.IsEmpty,
            $"Compare shows loop pickers offering {Quote(shown.LeftOffers)} (Left) and {Quote(shown.RightOffers)} (Right), but none was expected.");
        return that;
    }

    /// <summary>The help text reads <paramref name="text"/> (a sentence of it).</summary>
    public static AssertingThat<CompareSnapshot> HelpReads(this AssertingThat<CompareSnapshot> that, string text)
    {
        var help = that.InstanceToAssert.Help;
        Assert.True(help.Contains(text, StringComparison.Ordinal), $"The help text reads \"{help}\", but \"{text}\" was expected in it.");
        return that;
    }

    /// <summary>The help text names no example loop.</summary>
    public static AssertingThat<CompareSnapshot> HelpNamesNoExample(this AssertingThat<CompareSnapshot> that)
    {
        var help = that.InstanceToAssert.Help;
        Assert.False(help.Contains("example", StringComparison.OrdinalIgnoreCase), $"The help text names an example: \"{help}\", but none was expected.");
        return that;
    }

    /// <summary>The Left loop picker offers exactly <paramref name="loops"/>, in this order.</summary>
    public static AssertingThat<CompareSnapshot> OffersOnTheLeft(this AssertingThat<CompareSnapshot> that, params string[] loops) =>
        that.Offers("Left", that.InstanceToAssert.LeftOffers, loops);

    /// <summary>The Right loop picker offers exactly <paramref name="loops"/>, in this order.</summary>
    public static AssertingThat<CompareSnapshot> OffersOnTheRight(this AssertingThat<CompareSnapshot> that, params string[] loops) =>
        that.Offers("Right", that.InstanceToAssert.RightOffers, loops);

    private static AssertingThat<CompareSnapshot> Offers(this AssertingThat<CompareSnapshot> that, string side, ImmutableArray<string> shown, string[] loops)
    {
        Assert.True(shown.SequenceEqual(loops), $"The {side} loop picker offers {Quote(shown)}, but exactly {Quote(loops)} was expected.");
        return that;
    }

    private static string Quote(IEnumerable<string> texts) =>
        texts.Any() ? string.Join(", ", texts.Select(text => $"\"{text}\"")) : "nothing";
}
