using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Pages;

/// <summary>What a test checks on Home.</summary>
public static class HomeSnapshotAssertions
{
    /// <summary>Home is the page shown: the designer didn't open.</summary>
    public static AssertingThat<HomeSnapshot> IsShown(this AssertingThat<HomeSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.IsShown, "The designer is shown, but Home was expected.");
        return that;
    }

    /// <summary>Home offers exactly <paramref name="starts"/> to start a loop from (in any order), nothing else.</summary>
    public static AssertingThat<HomeSnapshot> Offers(this AssertingThat<HomeSnapshot> that, params string[] starts)
    {
        var shown = that.InstanceToAssert.Starts;
        Assert.True(
            shown.Order().SequenceEqual(starts.Order()),
            $"Home offers {Quote(shown)}, but exactly {Quote(starts)} was expected.");
        return that;
    }

    /// <summary>Home shows no "Example loops" to open.</summary>
    public static AssertingThat<HomeSnapshot> ShowsNoExampleLoop(this AssertingThat<HomeSnapshot> that) =>
        that.DoesNotRead("Example loops", "an example loop");

    /// <summary>Home shows no shared DIM build to start from.</summary>
    public static AssertingThat<HomeSnapshot> ShowsNoSharedDimBuild(this AssertingThat<HomeSnapshot> that) =>
        that.DoesNotRead("shared DIM build", "a shared DIM build");

    /// <summary>Home shows no build written by hand to start from.</summary>
    public static AssertingThat<HomeSnapshot> ShowsNoBuildWrittenByHand(this AssertingThat<HomeSnapshot> that) =>
        that.DoesNotRead("build written by hand", "a build written by hand");

    /// <summary>Home has no field to paste a DIM link into.</summary>
    public static AssertingThat<HomeSnapshot> HasNoDimLinkField(this AssertingThat<HomeSnapshot> that)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.TextFields == 0, $"Home shows {shown.TextFields} text field(s), but no DIM link field was expected.");
        return that.DoesNotRead("DIM link", "a DIM link field");
    }

    private static AssertingThat<HomeSnapshot> DoesNotRead(this AssertingThat<HomeSnapshot> that, string text, string what)
    {
        var shown = that.InstanceToAssert.Text;
        Assert.False(
            shown.Contains(text, StringComparison.OrdinalIgnoreCase),
            $"Home reads \"{text}\" ({what}), but none was expected. Home reads: \"{shown}\"");
        return that;
    }

    private static string Quote(IEnumerable<string> texts) => string.Join(", ", texts.Select(text => $"\"{text}\""));
}
