using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on a problem shown where the player acted.</summary>
public static class InlineProblemSnapshotAssertions
{
    /// <summary>The problem says exactly <paramref name="text"/>.</summary>
    public static AssertingThat<InlineProblemSnapshot> Reads(this AssertingThat<InlineProblemSnapshot> that, string text)
    {
        var shown = that.InstanceToAssert.Text;
        Assert.True(shown == text, $"The import problem reads \"{shown}\", but \"{text}\" was expected.");
        return that;
    }
}
