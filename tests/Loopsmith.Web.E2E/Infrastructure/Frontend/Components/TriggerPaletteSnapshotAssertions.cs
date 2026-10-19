using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the trigger panel: its picked row, and whether it is greyed out.</summary>
public static class TriggerPaletteSnapshotAssertions
{
    /// <summary>The "×" enemy count shows, with exactly <paramref name="triggers"/>, in that order.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsTheCountWith(this AssertingThat<TriggerPaletteSnapshot> that, params StepTrigger[] triggers)
    {
        var shown = that.InstanceToAssert;
        Assert.True(shown.ShowsTheCount, "The picked row shows no \"×\" enemy count, but one was expected.");
        Assert.True(
            shown.WithTheCount.SequenceEqual(triggers),
            $"The \"×\" enemy count goes with {Describe(shown.WithTheCount)}, but {Describe(triggers)} was expected.");
        return that;
    }

    /// <summary><paramref name="trigger"/> stands at the row's right end, apart from the "×" enemy count.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsApartFromTheCount(this AssertingThat<TriggerPaletteSnapshot> that, StepTrigger trigger)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.ApartFromTheCount.Contains(trigger) && !shown.WithTheCount.Contains(trigger),
            $"The picked row shows {Describe(shown.ApartFromTheCount)} apart from the \"×\" enemy count and {Describe(shown.WithTheCount)} with it, but {trigger.ToShownText()} was expected apart.");
        return that;
    }

    /// <summary>The "×" enemy count offers exactly <paramref name="counts"/>, in that order ("1 enemy", "3 in 3 s · One for All").</summary>
    public static AssertingThat<TriggerPaletteSnapshot> OffersCounts(this AssertingThat<TriggerPaletteSnapshot> that, params string[] counts)
    {
        var shown = that.InstanceToAssert.TargetCounts;
        var listed = string.Join(", ", shown.Select(count => $"\"{count}\""));
        Assert.True(
            shown.SequenceEqual(counts),
            $"The \"×\" enemy count offers {(listed.Length == 0 ? "nothing" : listed)}, but {string.Join(", ", counts.Select(count => $"\"{count}\""))} was expected.");
        return that;
    }

    /// <summary>The picked row shows <paramref name="option"/>'s box ("🎯 precision").</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsTheOption(this AssertingThat<TriggerPaletteSnapshot> that, RowOption option)
    {
        var shown = that.InstanceToAssert.Options;
        Assert.True(shown.Contains(option), $"The picked row shows {Describe(shown)}, but \"{option.ToShownText()}\" was expected among its boxes.");
        return that;
    }

    /// <summary>The picked row shows no <paramref name="option"/> box.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsNoOption(this AssertingThat<TriggerPaletteSnapshot> that, RowOption option)
    {
        var shown = that.InstanceToAssert.Options;
        Assert.False(shown.Contains(option), $"The picked row shows \"{option.ToShownText()}\", but no such box was expected.");
        return that;
    }

    private static string Describe(IEnumerable<RowOption> options)
    {
        var listed = string.Join(", ", options.Select(option => $"\"{option.ToShownText()}\""));
        return listed.Length == 0 ? "no box to tick" : listed;
    }

    private static string Describe(IEnumerable<StepTrigger> triggers)
    {
        var listed = string.Join(", ", triggers.Select(trigger => $"\"{trigger.ToShownText()}\""));
        return listed.Length == 0 ? "nothing" : listed;
    }

    /// <summary>The panel is greyed out: nothing in it can be tapped.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> IsGreyedOut(this AssertingThat<TriggerPaletteSnapshot> that)
    {
        Assert.True(that.InstanceToAssert.IsGreyedOut, "The trigger panel can be tapped, but it was expected greyed out.");
        return that;
    }

    /// <summary>The greyed-out panel's line reads <paramref name="expected"/>.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> Reads(this AssertingThat<TriggerPaletteSnapshot> that, string expected)
    {
        var shown = that.InstanceToAssert.DisabledNote;
        Assert.True(shown == expected, $"The trigger panel reads \"{shown}\", but \"{expected}\" was expected.");
        return that;
    }
}
