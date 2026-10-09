using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Presentation;

/// <summary>One button of the palette: the option it appends and its short caption ("hit", "kill").</summary>
public sealed record TriggerButton(TriggerOption Option, string Caption);

/// <summary>
/// A row of the palette: one trigger, or the hit / kill pair of the same ability or weapon. <see cref="Key"/> names
/// the row ("cast:Grenade", "fire:Kinetic", "class", …) and stays the same from one step to the next.
/// </summary>
public sealed record TriggerChoice(string Key, string Title, ImmutableArray<TriggerButton> Buttons);

public sealed record TriggerSection(TriggerGroup Group, string Heading, ImmutableArray<TriggerChoice> Choices);

/// <summary>
/// Pure: <see cref="LoopDesigning.ListTriggerOptions"/> → palette sections with hit/kill pairs side by side. Every
/// option is offered as pickable: the palette does not gate abilities on energy.
/// </summary>
public static class TriggerPaletteShaping
{
    public static ImmutableArray<TriggerSection> ShapePalette(ImmutableArray<TriggerOption> options) =>
    [
        .. options
            .GroupBy(option => option.Group)
            .OrderBy(group => group.Key)
            .Select(group => new TriggerSection(group.Key, DescribeGroup(group.Key), ShapeChoices([.. group]))),
    ];

    private static ImmutableArray<TriggerChoice> ShapeChoices(ImmutableArray<TriggerOption> options) =>
    [
        .. options
            .GroupBy(option => ToPairKey(option.Action))
            .Select(pair => (pair.Key, Options: pair.OrderBy(option => ReadHitOrder(option.Action)).ToImmutableArray()))
            .Select(pair => pair.Options.Length == 1
                ? new TriggerChoice(pair.Key, pair.Options[0].Label, [new TriggerButton(pair.Options[0], pair.Options[0].Label)])
                : new TriggerChoice(pair.Key, ReadTitle(pair.Options[0].Label), [.. pair.Options.Select(option => new TriggerButton(option, ReadQualifier(option.Label)))])),
    ];

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
