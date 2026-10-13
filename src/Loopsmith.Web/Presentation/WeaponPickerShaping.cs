using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One of the player's weapons in the weapon picker: its square and marks (tier, masterwork, breaker), its copy, its
/// archetype ("Hand Cannon"), damage type and its icon, its perks (one group per trait column it fills: the perk it
/// holds, then those it can switch to), whether its catalyst is in, where it is and whether the build wears it.
/// </summary>
public sealed record WeaponOptionView(
    IconTile Tile,
    TileMarks Marks,
    ItemInstanceId Instance,
    string Archetype,
    Optional<DamageType> Damage,
    Optional<string> DamageIcon,
    ImmutableArray<ImmutableArray<IconTile>> Columns,
    bool Catalyst,
    string Place,
    bool Worn);

/// <summary>The weapon picker for a slot: its title, the player's weapons for it and what it leaves out.</summary>
public sealed record WeaponPickerView(string Title, ImmutableArray<WeaponOptionView> Options, ImmutableArray<string> Notes);

/// <summary>Pure: what the weapon picker shows of the weapons Core lists for a slot (LoopDesigning.ListWeaponOptions).</summary>
public static class WeaponPickerShaping
{
    public static WeaponPickerView ShapePicker(ValidatedBuild build, WeaponOptions options) =>
        new(
            options.Slot.Match(slot => $"Pick your {slot.Value.DescribeWeaponSlot().ToLowerInvariant()} weapon", _ => "Pick a weapon"),
            [.. options.Options.Select(option => ShapeOption(build, option))],
            options.Notes);

    /// <summary>A weapon's type line: its archetype and damage type ("Hand Cannon · Arc").</summary>
    public static string DescribeType(WeaponOptionView option) =>
        string.Join(" · ", new[] { option.Archetype, DescribeDamage(option) }.Where(part => part.Length > 0));

    /// <summary>A weapon's damage type by name ("Strand"), the footer icon's alt text; empty when it has none.</summary>
    public static string DescribeDamage(WeaponOptionView option) =>
        option.Damage.Match(found => found.Value.ToString(), _ => "");

    private static WeaponOptionView ShapeOption(ValidatedBuild build, WeaponOption option)
    {
        var catalog = build.Catalog;
        var kind = option.Item.Kind is ManifestKind.Weapon weapon ? Optional.Some(weapon) : Optional.None<ManifestKind.Weapon>();
        var type = kind.Bind(found => found.DamageType);
        return new WeaponOptionView(
            LoadoutShaping.DescribeWeaponItem(option.Item),
            LoadoutShaping.DescribeMarks(
                catalog, Optional.Some(option.Item), Optional.Some(option.Weapon.Marks), LoopDesigning.FindCopyBreaker(build, option.Weapon), LoadoutShaping.DescribeFrame(build, option.Item)),
            option.Weapon.InstanceId,
            option.Item.Type.UnwrapOr(""),
            type,
            type.Bind(found => LoadoutShaping.FindDamageIcon(catalog, found)),
            [.. option.Columns.Where(column => !column.IsEmpty).Select(column => column.Select(hash => LoadoutShaping.DescribePerk(catalog, hash)).ToImmutableArray())],
            option.Catalyst,
            StatsShaping.DescribePlace(option.Weapon.Place),
            option.Worn);
    }
}
