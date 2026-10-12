using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>What a picker without a login fills: a weapon of the build (in place of it) or an empty weapon slot, or an armor slot.</summary>
[Union]
public partial record GamePickerTarget
{
    partial record Weapon(WeaponTarget Target);
    partial record Armor(ArmorSlot Slot);
}

/// <summary>A weapon of the game picked without a login, for where <see cref="Target"/> says (the designer reads it, then puts it in the build).</summary>
public sealed record GameWeaponPick(WeaponTarget Target, ItemHash Weapon);

/// <summary>An armor piece of the game picked without a login, for a slot (the designer reads it, then puts it in the build).</summary>
public sealed record GameArmorPick(ArmorSlot Slot, ItemHash Piece);

/// <summary>
/// One of the game's items in a picker without a login: its square and marks (its season watermark; a legendary weapon's
/// frame and the champion it breaks), its hash, its type ("Hand Cannon"), a weapon's damage type and its icon, its frame
/// (a legendary weapon's) or set (an armor piece's), whether the rules know it (⊘ when not) and whether the build wears it.
/// </summary>
public sealed record GameOptionView(
    IconTile Tile,
    TileMarks Marks,
    ItemHash Hash,
    string Type,
    Optional<DamageType> Damage,
    Optional<string> DamageIcon,
    Optional<CornerBadge> Frame,
    Optional<string> Set,
    bool InRules,
    bool Worn);

/// <summary>What the player narrowed a picker without a login to: the text searched, and a weapon type and an element (none: all).</summary>
public sealed record GamePickerFilter(string Search, Optional<string> Type, Optional<DamageType> Element);

/// <summary>A weapon type chip ("Hand Cannon") and whether it is on.</summary>
public sealed record TypeChip(string Type, bool On);

/// <summary>An element chip (Arc, with its icon) and whether it is on.</summary>
public sealed record ElementChip(DamageType Element, Optional<string> Icon, bool On);

/// <summary>
/// A picker without a login: its title, how many items it lists for the slot and those the filter shows, the weapon
/// type and element chips (a weapon picker's; none for armor), its search field's hint and its notes.
/// </summary>
public sealed record GamePickerView(
    string Title,
    int Total,
    ImmutableArray<GameOptionView> Shown,
    ImmutableArray<TypeChip> Types,
    ImmutableArray<ElementChip> Elements,
    string SearchHint,
    ImmutableArray<string> Notes);

/// <summary>Pure: what a picker without a login shows of the game's items Core lists for a slot (LoopDesigning.ListGameWeaponOptions, ListGameArmorOptions).</summary>
public static class GamePickerShaping
{
    /// <summary>No search and no chip on: every item the slot can take.</summary>
    public static readonly GamePickerFilter NoFilter = new("", Optional.None<string>(), Optional.None<DamageType>());

    /// <summary>What the picker says when Bungie's manifest couldn't be read.</summary>
    public static string DescribeUnread(string problem) =>
        $"Couldn't read Bungie's manifest ({problem}). Close this and try again later.";

    /// <summary>"Pick a kinetic weapon".</summary>
    public static string DescribeWeaponTitle(Optional<WeaponSlot> slot) =>
        slot.Match(found => $"Pick a {found.Value.DescribeWeaponSlot().ToLowerInvariant()} weapon", _ => "Pick a weapon");

    /// <summary>"Pick a piece for the helmet slot".</summary>
    public static string DescribeArmorTitle(ArmorSlot slot) =>
        $"Pick a piece for the {slot.DescribeArmorSlot().ToLowerInvariant()} slot";

    public static GamePickerView ShapeWeaponPicker(ValidatedBuild build, GameItems game, GameItemOptions options, Optional<WeaponSlot> slot, GamePickerFilter filter)
    {
        var all = options.Options.Select(option => ShapeWeapon(build, game, option)).ToImmutableArray();
        return new GamePickerView(
            DescribeWeaponTitle(slot),
            all.Length,
            [.. all.Where(option => IsShown(option, filter))],
            [.. all.Select(option => option.Type).Where(type => type.Length > 0).Distinct().Order(StringComparer.Ordinal).Select(type => new TypeChip(type, filter.Type == Optional.Some(type)))],
            [.. all.SelectMany(option => ToSequence(option.Damage)).Distinct().Order().Select(element => new ElementChip(element, LoadoutShaping.FindDamageIcon(build.Catalog, element), filter.Element == Optional.Some(element)))],
            "Search by name, type or element",
            options.Notes);
    }

    public static GamePickerView ShapeArmorPicker(ValidatedBuild build, GameItemOptions options, ArmorSlot slot, GamePickerFilter filter)
    {
        var all = options.Options.Select(option => ShapeArmor(build, option)).ToImmutableArray();
        return new GamePickerView(
            DescribeArmorTitle(slot),
            all.Length,
            [.. all.Where(option => IsShown(option, filter))],
            [],
            [],
            "Search by name or set",
            options.Notes);
    }

    /// <summary>The filter with a weapon type chip on, or off when it was on (one type at a time).</summary>
    public static GamePickerFilter ToggleType(GamePickerFilter filter, string type) =>
        filter with { Type = filter.Type == Optional.Some(type) ? Optional.None<string>() : Optional.Some(type) };

    /// <summary>The filter with an element chip on, or off when it was on (one element at a time).</summary>
    public static GamePickerFilter ToggleElement(GamePickerFilter filter, DamageType element) =>
        filter with { Element = filter.Element == Optional.Some(element) ? Optional.None<DamageType>() : Optional.Some(element) };

    /// <summary>The filter with the text searched.</summary>
    public static GamePickerFilter SetSearch(GamePickerFilter filter, string search) =>
        filter with { Search = search };

    /// <summary>A weapon's line under its name: "Hand Cannon · Kinetic · Adaptive Frame".</summary>
    public static string DescribeWeaponLine(GameOptionView option) =>
        string.Join(" · ", new[] { option.Type, option.Damage.Match(found => found.Value.ToString(), _ => ""), option.Frame.Match(frame => frame.Value.Name, _ => "") }.Where(part => part.Length > 0));

    /// <summary>A piece's line under its name: "Helmet · Luminopotent set".</summary>
    public static string DescribeArmorLine(GameOptionView option) =>
        string.Join(" · ", new[] { option.Type, option.Set.Match(set => $"{set.Value} set", _ => "") }.Where(part => part.Length > 0));

    /// <summary>"12 of 407 weapons" (or "407 weapons" when the filter shows them all).</summary>
    public static string DescribeCount(GamePickerView view, string noun) =>
        view.Shown.Length == view.Total ? $"{view.Total} {noun}" : $"{view.Shown.Length} of {view.Total} {noun}";

    private static GameOptionView ShapeWeapon(ValidatedBuild build, GameItems game, GameItemOption option)
    {
        var catalog = build.Catalog;
        var damage = option.Item.Kind is ManifestKind.Weapon weapon ? weapon.DamageType : Optional.None<DamageType>();
        var breaker = option.Item.Kind is ManifestKind.Weapon { Breaker: Optional<ItemHash>.Some hash } && catalog.Manifest.BreakerTypes.TryGetValue(hash.Value, out var found)
            ? Optional.Some(found)
            : Optional.None<BreakerType>();
        var frame = option.Item.Tier == Optional.Some(ItemTier.Exotic)
            ? Optional.None<CornerBadge>()
            : LoopDesigning.FindGameIntrinsic(game, option.Item).Bind(intrinsic => intrinsic.Icon.Map(icon => new CornerBadge(CornerKind.Frame, intrinsic.Name, LoadoutShaping.ToIconUrl(icon))));
        return new GameOptionView(
            LoadoutShaping.DescribeWeaponItem(option.Item) with { InRules = option.InRules },
            LoadoutShaping.DescribeMarks(catalog, Optional.Some(option.Item), Optional.None<GearMarks>(), breaker, frame),
            option.Item.Hash,
            option.Item.Type.UnwrapOr(""),
            damage,
            damage.Bind(type => LoadoutShaping.FindDamageIcon(catalog, type)),
            frame,
            Optional.None<string>(),
            option.InRules,
            option.Worn);
    }

    private static GameOptionView ShapeArmor(ValidatedBuild build, GameItemOption option)
    {
        var set = option.Item.Kind is ManifestKind.Armor { Set: Optional<ItemHash>.Some setHash } && build.Catalog.Manifest.ArmorSets.TryGetValue(setHash.Value, out var found)
            ? Optional.Some(found.Name)
            : Optional.None<string>();
        return new GameOptionView(
            LoadoutShaping.DescribeArmorItem(option.Item) with { InRules = option.InRules },
            LoadoutShaping.DescribeMarks(build.Catalog, Optional.Some(option.Item), Optional.None<GearMarks>(), Optional.None<BreakerType>(), Optional.None<CornerBadge>()),
            option.Item.Hash,
            option.Item.Type.UnwrapOr(""),
            Optional.None<DamageType>(),
            Optional.None<string>(),
            Optional.None<CornerBadge>(),
            set,
            option.InRules,
            option.Worn);
    }

    /// <summary>Shown when its chips are on (or none is) and every word searched is in its name, type, element, frame or set.</summary>
    private static bool IsShown(GameOptionView option, GamePickerFilter filter)
    {
        var text = string.Join(" ", new[]
        {
            option.Tile.Name, option.Type, option.Damage.Match(found => found.Value.ToString(), _ => ""),
            option.Frame.Match(frame => frame.Value.Name, _ => ""), option.Set.UnwrapOr(""),
        });
        var words = filter.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return filter.Type.Match(type => type.Value == option.Type, _ => true)
            && filter.Element.Match(element => option.Damage == Optional.Some(element.Value), _ => true)
            && words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
