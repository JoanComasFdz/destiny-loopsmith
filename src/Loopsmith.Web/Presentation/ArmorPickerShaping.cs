using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>One of the player's pieces in the armor picker: its square, its copy, its stats, where it is, its set and whether the build wears it.</summary>
public sealed record ArmorOptionView(IconTile Tile, ItemInstanceId Instance, string Roll, string Place, Optional<string> Set, bool Worn);

/// <summary>The armor picker for a slot: its title, the player's pieces for it and what it leaves out.</summary>
public sealed record ArmorPickerView(ArmorSlot Slot, string Title, ImmutableArray<ArmorOptionView> Options, ImmutableArray<string> Notes);

/// <summary>Pure: what the armor picker shows of the pieces Core lists for a slot (LoopDesigning.ListArmorOptions).</summary>
public static class ArmorPickerShaping
{
    public static ArmorPickerView ShapePicker(ValidatedBuild build, ArmorOptions options) =>
        new(
            options.Slot,
            $"Pick your {options.Slot.DescribeArmorSlot().ToLowerInvariant()}",
            [.. options.Options.Select(option => ShapeOption(build, option))],
            options.Notes);

    private static ArmorOptionView ShapeOption(ValidatedBuild build, ArmorOption option) =>
        new(
            LoadoutShaping.DescribeArmorItem(option.Item),
            option.Armor.InstanceId,
            option.Points.Match(points => StatsShaping.DescribeRoll(points.Value), _ => "Stats Loopsmith can't read (Bungie's data may be newer than Loopsmith's)"),
            StatsShaping.DescribePlace(option.Armor.Place),
            FindSetName(build, option.Item),
            option.Worn);

    private static Optional<string> FindSetName(ValidatedBuild build, ManifestItem item) =>
        item.Kind is ManifestKind.Armor { Set: Optional<ItemHash>.Some set } && build.Catalog.Manifest.ArmorSets.TryGetValue(set.Value, out var found)
            ? Optional.Some(found.Name)
            : Optional.None<string>();
}
