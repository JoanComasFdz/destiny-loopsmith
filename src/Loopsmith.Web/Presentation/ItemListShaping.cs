using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One item of the game the pickers' "Every item" lists: its square and marks (its season watermark, a weapon's breaker),
/// its hash, its type ("Hand Cannon"), its type line ("Hand Cannon · Arc · Shield Piercing", "Helmet · Luminopotent set"), a weapon's damage type and that type's icon, what the build
/// can't hold of it (an exotic class item's spirits), the text the search box matches, whether the rules know it (⊘ when not) and whether the build wears it.
/// </summary>
public sealed record ItemOptionView(
    IconTile Tile,
    TileMarks Marks,
    ItemHash Hash,
    string Type,
    string Line,
    Optional<DamageType> Damage,
    Optional<string> DamageIcon,
    Optional<string> Note,
    string Search,
    bool InRules,
    bool Worn);

/// <summary>An element chip of the weapons' "Every item" (Arc, with its icon).</summary>
public sealed record ElementChip(DamageType Element, Optional<string> Icon);

/// <summary>
/// "Every item" for a slot: the items it lists (the one the build wears there first), what it leaves out, and its chips:
/// the weapon types and elements of the weapons it lists (none for armor).
/// </summary>
public sealed record ItemListView(ImmutableArray<ItemOptionView> Options, ImmutableArray<string> Notes, ImmutableArray<string> Types, ImmutableArray<ElementChip> Elements);

/// <summary>What the player narrowed "Every item" to: the words searched, and a weapon type and an element chip (none: all).</summary>
public sealed record ItemListFilter(string Query, Optional<string> Type, Optional<DamageType> Element);

/// <summary>Pure: what "Every item" shows of the items Core lists for a slot (LoopDesigning.ListWeaponItems, ListArmorItems), and its search.</summary>
public static class ItemListShaping
{
    /// <summary>No search and no chip on: every item the slot can take.</summary>
    public static readonly ItemListFilter NoFilter = new("", Optional.None<string>(), Optional.None<DamageType>());

    public static ItemListView ShapeWeapons(ValidatedBuild build, WeaponItemOptions options)
    {
        var shaped = options.Options.Select(option => ShapeWeapon(build, option)).ToImmutableArray();
        return new(
            shaped,
            options.Notes,
            [.. shaped.Select(option => option.Type).Where(type => type.Length > 0).Distinct().Order(StringComparer.Ordinal)],
            [.. shaped.SelectMany(option => ToSequence(option.Damage)).Distinct().Order().Select(element => new ElementChip(element, LoadoutShaping.FindDamageIcon(build.Catalog, element)))]);
    }

    public static ItemListView ShapeArmor(ValidatedBuild build, ArmorItemOptions options) =>
        new([.. options.Options.Select(option => ShapePiece(build, option))], options.Notes, [], []);

    /// <summary>
    /// The options of the chips on (a weapon type, an element; any when none is) whose name, type, damage type or set
    /// hold every word searched (any case); all of them for no filter.
    /// </summary>
    public static ImmutableArray<ItemOptionView> FilterOptions(ImmutableArray<ItemOptionView> options, ItemListFilter filter)
    {
        var words = filter.Query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return
        [
            .. options.Where(option =>
                filter.Type.Match(type => type.Value == option.Type, _ => true)
                && filter.Element.Match(element => option.Damage == Optional.Some(element.Value), _ => true)
                && words.All(word => option.Search.Contains(word, StringComparison.Ordinal))),
        ];
    }

    /// <summary>The filter with the words searched.</summary>
    public static ItemListFilter SetQuery(ItemListFilter filter, string query) =>
        filter with { Query = query };

    /// <summary>The filter with a weapon type chip on, or off when it was on (one type at a time).</summary>
    public static ItemListFilter ToggleType(ItemListFilter filter, string type) =>
        filter with { Type = filter.Type == Optional.Some(type) ? Optional.None<string>() : Optional.Some(type) };

    /// <summary>The filter with an element chip on, or off when it was on (one element at a time).</summary>
    public static ItemListFilter ToggleElement(ItemListFilter filter, DamageType element) =>
        filter with { Element = filter.Element == Optional.Some(element) ? Optional.None<DamageType>() : Optional.Some(element) };

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
            LoadoutShaping.DescribeWeaponItem(item) with { InRules = option.InRules },
            LoadoutShaping.DescribeMarks(build.Catalog, Optional.Some(item), Optional.None<GearMarks>(), breaker, Optional.None<CornerBadge>()),
            item.Hash,
            item.Type.UnwrapOr(""),
            line,
            damage,
            damage.Bind(found => LoadoutShaping.FindDamageIcon(build.Catalog, found)),
            Optional.None<string>(),
            ToSearchText(item.Name, line),
            option.InRules,
            option.Worn);
    }

    private static ItemOptionView ShapePiece(ValidatedBuild build, ArmorItemOption option)
    {
        var item = option.Item;
        var line = JoinParts(item.Type.UnwrapOr(""), option.Set.Match(set => $"{set.Value.Name} set", _ => ""));
        return new ItemOptionView(
            LoadoutShaping.DescribeArmorItem(item) with { InRules = option.InRules },
            LoadoutShaping.DescribeMarks(build.Catalog, Optional.Some(item), Optional.None<GearMarks>(), Optional.None<BreakerType>(), Optional.None<CornerBadge>()),
            item.Hash,
            item.Type.UnwrapOr(""),
            line,
            Optional.None<DamageType>(),
            Optional.None<string>(),
            option.Note,
            ToSearchText(item.Name, line),
            option.InRules,
            option.Worn);
    }

    private static string JoinParts(params string[] parts) =>
        string.Join(" · ", parts.Where(part => part.Length > 0));

    private static string ToSearchText(string name, string line) =>
        $"{name} {line}".ToLowerInvariant();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}
