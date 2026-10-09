using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One button of the palette: the action it appends (the option aimed at its row's target count), that action's token
/// and label in the core's wording ("grenade:kill:3", "Grenade (kill 3)"), and the button's short caption ("hit", "kill").
/// </summary>
public sealed record TriggerButton(PlayerAction Action, string Token, string Label, string Caption, bool IsNew);

/// <summary>
/// A row of the palette: one trigger, or the hit / kill pair of the same ability or weapon. <see cref="Key"/> names
/// the row ("cast:Grenade", "fire:Kinetic", "class", …) and stays the same from one step to the next.
/// <see cref="Targets"/> is how many enemies the row's actions hit or kill — none for a row without targets (class
/// ability, pickups, wait).
/// </summary>
public sealed record TriggerChoice(string Key, string Title, Optional<TargetCount> Targets, ImmutableArray<TriggerButton> Buttons);

public sealed record TriggerSection(TriggerGroup Group, string Heading, ImmutableArray<TriggerChoice> Choices);

/// <summary>
/// Pure: <see cref="LoopDesigning.ListTriggerOptions"/> → palette sections with hit/kill pairs side by side, each pair
/// aimed at the target count picked for its row (<see cref="LoopDesigning.SetTargetCount"/>). Every option is pickable.
/// </summary>
public static class TriggerPaletteShaping
{
    /// <summary>The target counts a row offers (the core accepts up to <see cref="TargetCount.Maximum"/>).</summary>
    public static readonly ImmutableArray<int> TargetCountChoices = [.. Enumerable.Range(1, 9)];

    /// <param name="counts">The target count picked per row <see cref="TriggerChoice.Key"/>; a row not in it aims at one enemy.</param>
    public static ImmutableArray<TriggerSection> ShapePalette(
        ImmutableArray<TriggerOption> options, ImmutableDictionary<string, TargetCount> counts, ValidatedBuild build) =>
    [
        .. options
            .GroupBy(option => option.Group)
            .OrderBy(group => group.Key)
            .Select(group => new TriggerSection(group.Key, DescribeGroup(group.Key), ShapeChoices([.. group], counts, build))),
    ];

    /// <summary>A picked count ("3") as a <see cref="TargetCount"/>; none when it is not a whole number in range.</summary>
    public static Optional<TargetCount> ParseTargetCount(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && TargetCount.TryFrom(value, out var count)
            ? Optional.Some(count)
            : Optional.None<TargetCount>();

    private static ImmutableArray<TriggerChoice> ShapeChoices(
        ImmutableArray<TriggerOption> options, ImmutableDictionary<string, TargetCount> counts, ValidatedBuild build) =>
    [
        .. options
            .GroupBy(option => ToPairKey(option.Action))
            .Select(pair => ShapeChoice(pair.Key, [.. pair.OrderBy(option => ReadHitOrder(option.Action))], counts, build)),
    ];

    private static TriggerChoice ShapeChoice(
        string key, ImmutableArray<TriggerOption> pair, ImmutableDictionary<string, TargetCount> counts, ValidatedBuild build)
    {
        var targets = LoopDesigning.ReadTargetCount(pair[0].Action)
            .Map(one => counts.TryGetValue(key, out var picked) ? picked : one);
        var title = pair.Length == 1 ? pair[0].Label : ReadTitle(pair[0].Label);
        var buttons = pair
            .Select(option => ShapeButton(option, targets, pair.Length == 1 ? option.Label : ReadQualifier(option.Label), build))
            .ToImmutableArray();
        return new TriggerChoice(key, title, targets, buttons);
    }

    private static TriggerButton ShapeButton(TriggerOption option, Optional<TargetCount> targets, string caption, ValidatedBuild build)
    {
        var action = targets.Match(count => LoopDesigning.SetTargetCount(option.Action, count.Value), _ => option.Action);
        return new TriggerButton(action, action.ToActionToken(), build.Catalog.Glossary.DescribeAction(action, build.Build), caption, option.IsNew);
    }

    private static string DescribeGroup(TriggerGroup group) =>
        group switch
        {
            TriggerGroup.Ability => "Abilities",
            TriggerGroup.Weapon => "Weapons",
            TriggerGroup.Pickup => "Pickups",
            _ => "Time",
        };

    /// <summary>Hit and kill of the same ability or weapon share a key; everything else stands alone.</summary>
    private static string ToPairKey(PlayerAction action) =>
        action.Match(
            cast => $"cast:{cast.Kind}",
            _ => "class",
            fire => $"fire:{fire.Slot}",
            collect => $"pickup:{collect.Pickup}",
            wait => $"wait:{wait.Duration}");

    private static int ReadHitOrder(PlayerAction action) =>
        action.Match(
            cast => cast.Hit == HitOutcome.Kill ? 1 : 0,
            _ => 0,
            fire => fire.Hit == HitOutcome.Kill ? 1 : 0,
            _ => 0,
            _ => 0);

    /// <summary>"Grenade (kill)" → "Grenade".</summary>
    private static string ReadTitle(string label)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        return open < 0 ? label : label[..open];
    }

    /// <summary>"Grenade (kill)" → "kill".</summary>
    private static string ReadQualifier(string label)
    {
        var open = label.LastIndexOf(" (", StringComparison.Ordinal);
        return open < 0 || !label.EndsWith(')') ? label : label[(open + 2)..^1];
    }
}
