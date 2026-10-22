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

    /// <summary>The picked row's "Rank" picker shows, at <paramref name="rank"/>, and can be changed.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> HasTheRankPickerEnabledAt(this AssertingThat<TriggerPaletteSnapshot> that, EnemyRank rank) =>
        that.HasTheRankPicker(rank, isEnabled: true);

    /// <summary>The picked row's "Rank" picker shows, at <paramref name="rank"/>, greyed out: it can't be changed.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> HasTheRankPickerGreyedOutAt(this AssertingThat<TriggerPaletteSnapshot> that, EnemyRank rank) =>
        that.HasTheRankPicker(rank, isEnabled: false);

    private static AssertingThat<TriggerPaletteSnapshot> HasTheRankPicker(this AssertingThat<TriggerPaletteSnapshot> that, EnemyRank rank, bool isEnabled)
    {
        var shown = that.InstanceToAssert.Rank;
        var expected = $"\"{rank.ToShownText()}\", {(isEnabled ? "enabled" : "greyed out")}";
        Assert.True(shown is not null, $"The picked row shows no \"Rank\" picker, but one at {expected} was expected.");
        Assert.True(
            shown.Picked == rank && shown.IsEnabled == isEnabled,
            $"The picked row's \"Rank\" picker is at \"{shown.Picked.ToShownText()}\", {(shown.IsEnabled ? "enabled" : "greyed out")}, but {expected} was expected.");
        return that;
    }

    /// <summary>"On you" shows <paramref name="status"/> on you, with exactly <paramref name="buttons"/> beside it, in that order ("hit", "ends").</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsOnYouWith(this AssertingThat<TriggerPaletteSnapshot> that, Keyword status, params string[] buttons)
    {
        var chip = that.InstanceToAssert.OnYou.FirstOrDefault(shown => !shown.IsButton && shown.Name == status.ToShownText());
        Assert.True(chip is not null, $"\"On you\" shows {DescribeOnYou(that.InstanceToAssert)}, but \"{status.ToShownText()}\" on you was expected.");
        Assert.True(
            chip.Buttons.SequenceEqual(buttons),
            $"\"On you\" shows \"{chip.Name}\" with {DescribeCaptions(chip.Buttons)}, but {DescribeCaptions(buttons)} was expected.");
        return that;
    }

    /// <summary>"On you" shows <paramref name="status"/> on you with <paramref name="icon"/> before its name.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsOnYouWithIcon(this AssertingThat<TriggerPaletteSnapshot> that, Keyword status, KeywordIcon icon) =>
        that.ShowsOnYouWithIcons(status, [icon]);

    /// <summary>"On you" shows <paramref name="status"/> on you with no icon before its name (Bungie gives it none).</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsOnYouWithNoIcon(this AssertingThat<TriggerPaletteSnapshot> that, Keyword status) =>
        that.ShowsOnYouWithIcons(status, []);

    private static AssertingThat<TriggerPaletteSnapshot> ShowsOnYouWithIcons(this AssertingThat<TriggerPaletteSnapshot> that, Keyword status, KeywordIcon[] icons)
    {
        var chip = that.InstanceToAssert.OnYou.FirstOrDefault(shown => !shown.IsButton && shown.Name == status.ToShownText());
        Assert.True(chip is not null, $"\"On you\" shows {DescribeOnYou(that.InstanceToAssert)}, but \"{status.ToShownText()}\" on you was expected.");
        Assert.True(
            chip.Icons.SequenceEqual(icons),
            $"\"On you\" shows \"{chip.Name}\" with the icons {DescribeCaptions(chip.Icons.Select(shown => shown.ToShownText()))}, but {DescribeCaptions(icons.Select(icon => icon.ToShownText()))} was expected.");
        return that;
    }

    /// <summary>"On you" shows <paramref name="state"/> as a button of its own, a state to declare (not a buff on you).</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsOnYouAsAButton(this AssertingThat<TriggerPaletteSnapshot> that, Keyword state)
    {
        Assert.True(
            that.InstanceToAssert.OnYou.Any(shown => shown.IsButton && shown.Name == state.ToShownText()),
            $"\"On you\" shows {DescribeOnYou(that.InstanceToAssert)}, but \"{state.ToShownText()}\" as a button was expected.");
        return that;
    }

    /// <summary>"On you" shows "<paramref name="ability"/> recharged" as a button of its own ("Grenade recharged").</summary>
    public static AssertingThat<TriggerPaletteSnapshot> OffersRecharged(this AssertingThat<TriggerPaletteSnapshot> that, AbilitySlot ability)
    {
        Assert.True(
            that.InstanceToAssert.OnYou.Any(shown => shown.IsButton && shown.Name == ability.ToRechargedText()),
            $"\"On you\" shows {DescribeOnYou(that.InstanceToAssert)}, but \"{ability.ToRechargedText()}\" was expected.");
        return that;
    }

    /// <summary>"On you" shows no "<paramref name="ability"/> recharged" ("Grenade recharged"): the ability is full.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> DoesNotOfferRecharged(this AssertingThat<TriggerPaletteSnapshot> that, AbilitySlot ability)
    {
        Assert.True(
            !that.InstanceToAssert.OnYou.Any(shown => shown.IsButton && shown.Name == ability.ToRechargedText()),
            $"\"On you\" shows {DescribeOnYou(that.InstanceToAssert)}, but no \"{ability.ToRechargedText()}\" was expected.");
        return that;
    }

    private static string DescribeOnYou(TriggerPaletteSnapshot shown) =>
        shown.OnYou.IsEmpty
            ? "nothing"
            : string.Join(", ", shown.OnYou.Select(chip => chip.IsButton ? $"the button \"{chip.Name}\"" : $"\"{chip.Name}\" with {DescribeCaptions(chip.Buttons)}"));

    private static string DescribeCaptions(IEnumerable<string> captions) =>
        captions.Any() ? string.Join(" and ", captions.Select(caption => $"\"{caption}\"")) : "no buttons";

    /// <summary>The picked row shows exactly <paramref name="triggers"/>, in that order: those with the "×" enemy count, then those apart from it.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> ShowsTriggers(this AssertingThat<TriggerPaletteSnapshot> that, params StepTrigger[] triggers)
    {
        var shown = that.InstanceToAssert.WithTheCount.Concat(that.InstanceToAssert.ApartFromTheCount).ToArray();
        Assert.True(shown.SequenceEqual(triggers), $"The picked row shows {Describe(shown)}, but {Describe(triggers)} was expected.");
        return that;
    }

    /// <summary><paramref name="trigger"/> shows in the picked row but is disabled: it can't be tapped.</summary>
    public static AssertingThat<TriggerPaletteSnapshot> IsDisabled(this AssertingThat<TriggerPaletteSnapshot> that, StepTrigger trigger)
    {
        var shown = that.InstanceToAssert;
        Assert.True(
            shown.Disabled.Contains(trigger),
            $"The picked row shows {Describe(shown.Disabled)} disabled, but \"{trigger.ToShownText()}\" was expected disabled.");
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

/// <summary>What a test checks on how full an ability's square in the step HUD is (ADRs D48).</summary>
public static class SquareFillAssertions
{
    /// <summary><paramref name="ability"/>'s square's colour fills <paramref name="expected"/> of it from below ("8.44%", "0%": all gray).</summary>
    public static AssertingThat<TriggerPaletteSnapshot> FillsSquare(this AssertingThat<TriggerPaletteSnapshot> that, Ability ability, string expected)
    {
        var shown = that.InstanceToAssert.Squares.FirstOrDefault(square => square.Name == ability.ToShownText());
        Assert.True(
            shown is not null && shown.Fill == expected,
            $"The \"{ability.ToShownText()}\" square is filled to {shown?.Fill ?? "nothing (it shows no energy)"}, but {expected} was expected.");
        return that;
    }
}
