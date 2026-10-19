using JoanComasFdz.AssertingThat;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the trigger panel as a whole: greyed out or not, and why.</summary>
public static class TriggerPanelStateSnapshotAssertions
{
    /// <summary>The panel is greyed out: nothing in it can be tapped.</summary>
    public static AssertingThat<TriggerPanelStateSnapshot> IsGreyedOut(this AssertingThat<TriggerPanelStateSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.IsGreyedOut, "The trigger panel can be tapped, but it was expected greyed out.");
        return that;
    }

    /// <summary>The greyed-out panel's line reads <paramref name="expected"/>.</summary>
    public static AssertingThat<TriggerPanelStateSnapshot> Reads(this AssertingThat<TriggerPanelStateSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.Note;
        Assert.True(shown == expected, $"The trigger panel reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }
}
