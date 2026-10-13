using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One item of the game the pickers' "Every item" lists: its square and marks (its season watermark, a weapon's breaker),
/// its hash, its type line ("Hand Cannon · Arc · Shield Piercing", "Helmet · Luminopotent set"), a weapon's damage type and that type's icon, what the build
/// can't hold of it (an exotic class item's spirits), the text the search box matches and whether the build wears it.
/// </summary>
public sealed record ItemOptionView(
    IconTile Tile,
    TileMarks Marks,
    ItemHash Hash,
    string Line,
    Optional<DamageType> Damage,
    Optional<string> DamageIcon,
    Optional<string> Note,
    string Search,
    bool Worn);

/// <summary>"Every item" for a slot: the items it lists (the one the build wears there first) and what it leaves out.</summary>
public sealed record ItemListView(ImmutableArray<ItemOptionView> Options, ImmutableArray<string> Notes);

/// <summary>Pure: what "Every item" shows of the items Core lists for a slot (LoopDesigning.ListWeaponItems, ListArmorItems), and its search.</summary>
public static class ItemListShaping
{
    public static ItemListView ShapeWeapons(ValidatedBuild build, WeaponItemOptions options) =>
        new([.. options.Options.Select(option => ShapeWeapon(build, option))], options.Notes);

    public static ItemListView ShapeArmor(ValidatedBuild build, ArmorItemOptions options) =>
        new([.. options.Options.Select(option => ShapePiece(build, option))], options.Notes);

    /// <summary>The options whose name, type, damage type or set hold every word of <paramref name="query"/> (any case); all of them for an empty query.</summary>
    public static ImmutableArray<ItemOptionView> FilterOptions(ImmutableArray<ItemOptionView> options, string query)
    {
        var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. options.Where(option => words.All(word => option.Search.Contains(word, StringComparison.Ordinal)))];
    }

    /// <summary>How many items show: "872 weapons", or "12 of 872 weapons" while the search narrows them.</summary>
    public static string DescribeCount(int shown, int all, string noun)
    {
        var count = all.ToString(CultureInfo.InvariantCulture);
        var plural = all == 1 ? noun : $"{noun}s";
        return shown == all ? $"{count} {plural}" : $"{shown.ToString(CultureInfo.InvariantCulture)} of {count} {plural}";
    }

    private static ItemOptionView ShapeWeapon(ValidatedBuild build, WeaponItemOption option)
    {
        var item = option.Item;
        var weapon = item.Kind is ManifestKind.Weapon kind ? Optional.Some(kind) : Optional.None<ManifestKind.Weapon>();
        var damage = weapon.Bind(found => found.DamageType);
        var breaker = weapon.Bind(found => found.Breaker).Bind(build.Catalog.Manifest.FindBreakerType);
        var line = JoinParts(item.Type.UnwrapOr(""), damage.Match(found => found.Value.ToString(), _ => ""), breaker.Match(found => found.Value.Name, _ => ""));
        return new ItemOptionView(
            LoadoutShaping.DescribeWeaponItem(item),
            LoadoutShaping.DescribeMarks(build.Catalog, Optional.Some(item), Optional.None<GearMarks>(), breaker, Optional.None<CornerBadge>()),
            item.Hash,
            line,
            damage,
            damage.Bind(found => LoadoutShaping.FindDamageIcon(build.Catalog, found)),
            Optional.None<string>(),
            ToSearchText(item.Name, line),
            option.Worn);
    }

    private static ItemOptionView ShapePiece(ValidatedBuild build, ArmorItemOption option)
    {
        var item = option.Item;
        var line = JoinParts(item.Type.UnwrapOr(""), option.Set.Match(set => $"{set.Value.Name} set", _ => ""));
        return new ItemOptionView(
            LoadoutShaping.DescribeArmorItem(item),
            LoadoutShaping.DescribeMarks(build.Catalog, Optional.Some(item), Optional.None<GearMarks>(), Optional.None<BreakerType>(), Optional.None<CornerBadge>()),
            item.Hash,
            line,
            Optional.None<DamageType>(),
            Optional.None<string>(),
            option.Note,
            ToSearchText(item.Name, line),
            option.Worn);
    }

    private static string JoinParts(params string[] parts) =>
        string.Join(" · ", parts.Where(part => part.Length > 0));

    private static string ToSearchText(string name, string line) =>
        $"{name} {line}".ToLowerInvariant();
}
