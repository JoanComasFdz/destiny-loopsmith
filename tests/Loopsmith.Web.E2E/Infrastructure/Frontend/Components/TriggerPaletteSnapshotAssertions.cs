using JoanComasFdz.AssertingThat;
using Loopsmith.Web.E2E.Infrastructure.Vocabulary;

namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>What a test checks on the trigger panel's picked row.</summary>
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

    private static string Describe(IEnumerable<StepTrigger> triggers)
    {
        var listed = string.Join(", ", triggers.Select(trigger => $"\"{trigger.ToShownText()}\""));
        return listed.Length == 0 ? "nothing" : listed;
    }
}
