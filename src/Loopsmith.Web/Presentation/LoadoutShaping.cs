using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// One square of the DIM-style loadout: the item's icon (a bungie.net address) or none, its name, colour and rarity,
/// and whether the rules know it (an item the DIM loadout had but the build leaves out is shown, dimmed).
/// </summary>
public sealed record IconTile(
    string Name,
    Optional<string> Icon,
    Affinity Affinity,
    Optional<ItemTier> Tier,
    bool InRules,
    Optional<string> Description,
    int Copies);

/// <summary>The subclass block: the subclass item, its super, the abilities (class, jump, melee, grenade), aspects, fragments.</summary>
public sealed record SubclassView(
    Optional<IconTile> Subclass,
    IconTile Super,
    ImmutableArray<IconTile> Abilities,
    ImmutableArray<IconTile> Aspects,
    ImmutableArray<IconTile> Fragments);

/// <summary>
/// A weapon: its tile, slot, archetype, damage type (and that type's icon), the perks the build gives it, and how many of
/// its two selected perks the build doesn't name (a DIM loadout names the weapon, not its roll).
/// </summary>
public sealed record WeaponView(
    IconTile Weapon,
    WeaponSlot Slot,
    Optional<string> Archetype,
    DamageType Type,
    Optional<string> DamageIcon,
    ImmutableArray<IconTile> Perks,
    int MissingPerks);

/// <summary>An armor slot: the piece in it (when the loadout names one) and the mods that only fit that slot, one tile each.</summary>
public sealed record ArmorView(ArmorSlot Slot, Optional<IconTile> Piece, ImmutableArray<IconTile> Mods);

/// <summary>
/// A build laid out like DIM shows a loadout: subclass, artifact, weapons (kinetic, energy, power), armor with each
/// piece's mods, general mods, set bonuses.
/// </summary>
public sealed record LoadoutView(
    SubclassView Subclass,
    ImmutableArray<IconTile> Artifact,
    ImmutableArray<WeaponView> Weapons,
    ImmutableArray<ArmorView> Armor,
    ImmutableArray<IconTile> GeneralMods,
    ImmutableArray<IconTile> SetBonuses);

/// <summary>
/// Pure: a validated build → its DIM-style view: where each tile goes. What an element, a hash or a subclass is comes from
/// the kernel (<see cref="ManifestReading"/>, over the manifest excerpt <c>rules/manifest.yaml</c>); what the excerpt
/// doesn't know has no icon and keeps its catalog name (or "?"). Left-out items (a DIM loadout's,
/// <see cref="Build.LeftOut"/>) sit where DIM would show them, marked as not in the rules: a mod under the armor piece of
/// the slot the manifest gives it, every other mod general; an unknown ability as the left-out subclass plug of its kind.
/// </summary>
public static class LoadoutShaping
{
    private const string IconHost = "https://www.bungie.net";

    /// <summary>A weapon's selected perks: the two trait columns of its roll.</summary>
    private const int SelectedPerks = 2;

    private static readonly ImmutableArray<ArmorSlot> ArmorSlots = [ArmorSlot.Helmet, ArmorSlot.Arms, ArmorSlot.Chest, ArmorSlot.Legs, ArmorSlot.ClassItem];

    private sealed record LeftOutEntry(LoadoutPart Part, Optional<ManifestItem> Item, ItemHash Hash);

    public static LoadoutView ShapeLoadout(ValidatedBuild build)
    {
        var b = build.Build;
        var catalog = build.Catalog;
        var leftOut = b.LeftOut.Select(item => new LeftOutEntry(item.Part, catalog.Manifest.FindItem(item.Hash), item.Hash)).ToImmutableArray();
        var plugs = leftOut.Where(entry => entry.Part == LoadoutPart.SubclassPlug).ToImmutableArray();
        var mods = b.ArmorMods
            .Select(id => (Tile: DescribeElement(catalog, id), Slot: catalog.FindElementItem(id).Bind(item => item.ReadArmorSlot())))
            .Concat(leftOut.Where(entry => entry.Part == LoadoutPart.ArmorMod)
                .Select(entry => (Tile: DescribeLeftOut(entry), Slot: entry.Item.Bind(item => item.ReadArmorSlot()))))
            .ToImmutableArray();
        var pieces = leftOut
            .Where(entry => entry.Part == LoadoutPart.Item && IsKind<ManifestKind.Armor>(entry.Item))
            .Select(entry => (Slot: entry.Item.Bind(item => item.ReadArmorSlot()), Tile: DescribeLeftOut(entry)))
            .Concat(ToSome(b.ExoticArmor).Select(id => (Slot: catalog.FindElementItem(id).Bind(item => item.ReadArmorSlot()), Tile: DescribeElement(catalog, id))))
            .ToImmutableArray();

        var subclass = new SubclassView(
            catalog.FindSubclassItem(b.Class, b.Subclass).Map(item => DescribeItem(item, item.Name, true, Optional.None<string>(), b.Subclass.ToAffinity())),
            DescribeAbility<ManifestKind.Super>(catalog, b.Abilities.Super, plugs),
            [
                DescribeAbility<ManifestKind.ClassAbility>(catalog, b.Abilities.ClassAbility, plugs),
                .. DescribeLeftOutPlugs<ManifestKind.Movement>(plugs),
                DescribeAbility<ManifestKind.Melee>(catalog, b.Abilities.Melee, plugs),
                DescribeAbility<ManifestKind.Grenade>(catalog, b.Abilities.Grenade, plugs),
            ],
            [.. b.Aspects.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Aspect>(plugs)],
            [.. b.Fragments.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Fragment>(plugs)]);
        return new LoadoutView(
            subclass,
            [
                .. b.ArtifactPerks.Select(id => DescribeElement(catalog, id)),
                .. leftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk).Select(DescribeLeftOut),
            ],
            [.. b.Weapons.OrderBy(weapon => weapon.Slot).Select(weapon => DescribeWeapon(catalog, weapon))],
            [
                .. ArmorSlots
                    .Select(slot => new ArmorView(
                        slot,
                        pieces.Where(piece => piece.Slot == Optional.Some(slot)).Select(piece => Optional.Some(piece.Tile)).FindFirstSome(),
                        [.. mods.Where(mod => mod.Slot == Optional.Some(slot)).Select(mod => mod.Tile)]))
                    .Where(view => view.Piece.IsSome() || !view.Mods.IsEmpty),
            ],
            GroupCopies(mods.Where(mod => !mod.Slot.IsSome()).Select(mod => mod.Tile)),
            GroupCopies(b.ArmorSetBonuses.Select(id => DescribeElement(catalog, id))));
    }

    /// <summary>The icon of the build's subclass item (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<string> FindSubclassIcon(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.FindSubclassItem(guardianClass, subclass).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>The bungie.net address of a manifest icon path.</summary>
    public static string ToIconUrl(string path) => IconHost + path;

    private static IconTile DescribeElement(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? catalog.FindElementItem(id).Match(
                item => DescribeItem(item.Value, element.Name, true, element.Description, element.Affinity),
                _ => new IconTile(element.Name, Optional.None<string>(), element.Affinity, Optional.None<ItemTier>(), true, element.Description, 1))
            : new IconTile(id.Value, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), true, Optional.None<string>(), 1);

    private static IconTile DescribeItem(ManifestItem item, string name, bool inRules, Optional<string> description, Affinity affinity) =>
        new(name, item.Icon.Map(ToIconUrl), affinity, item.Tier, inRules, description, 1);

    /// <summary>A hash the build left out: the manifest's name and icon when the excerpt has it, else just the hash.</summary>
    private static IconTile DescribeLeftOut(LeftOutEntry entry) =>
        entry.Item.Match(
            known => DescribeItem(known.Value, known.Value.Name, false, Optional.Some("Not in Loopsmith's rules yet: left out of the build."), ToAffinity(known.Value)),
            _ => new IconTile(entry.Hash.Value.ToString(CultureInfo.InvariantCulture), Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                Optional.Some("Not in Loopsmith's rules or its manifest excerpt yet."), 1));

    /// <summary>
    /// An ability slot: its element; or, when the build has "?", the left-out subclass plug of that kind the manifest
    /// names (dimmed); or a "?" tile.
    /// </summary>
    private static IconTile DescribeAbility<TKind>(RuleCatalog catalog, Optional<ElementId> ability, ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        ability.Match(
            known => DescribeElement(catalog, known.Value),
            _ => DescribeLeftOutPlugs<TKind>(plugs)
                .Select(Optional.Some)
                .FindFirstSome()
                .UnwrapOr(new IconTile(DomainPhrasing.Unknown, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                    Optional.Some("Not known: not in the rule catalog yet, so it sets nothing off."), 1)));

    private static IEnumerable<IconTile> DescribeLeftOutPlugs<TKind>(ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        plugs.Where(entry => IsKind<TKind>(entry.Item)).Select(DescribeLeftOut);

    private static WeaponView DescribeWeapon(RuleCatalog catalog, WeaponLoadout weapon)
    {
        var item = weapon.Hash.Bind(hash => catalog.Manifest.FindItem(hash));
        var tile = item.Match(
            known => DescribeItem(known.Value, weapon.Name, true, Optional.None<string>(), weapon.Type.ToAffinity()),
            _ => new IconTile(weapon.Name, Optional.None<string>(), weapon.Type.ToAffinity(), Optional.None<ItemTier>(), true, Optional.None<string>(), 1));
        var damageIcon = catalog.Manifest.DamageTypeIcons.TryGetValue(weapon.Type, out var icon) ? Optional.Some(ToIconUrl(icon)) : Optional.None<string>();
        return new WeaponView(
            tile,
            weapon.Slot,
            weapon.Archetype,
            weapon.Type,
            damageIcon,
            [.. weapon.Perks.Select(id => DescribeElement(catalog, id))],
            Math.Max(SelectedPerks - weapon.Perks.Length, 0));
    }

    private static bool IsKind<TKind>(Optional<ManifestItem> item)
        where TKind : ManifestKind =>
        item.Match(known => known.Value.Kind is TKind, _ => false);

    private static Affinity ToAffinity(ManifestItem item) =>
        item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } ? type.Value.ToAffinity() : Affinity.Neutral;

    /// <summary>Copies of the same tile as one, with a count (general mods: "Grenade Mod ×5").</summary>
    private static ImmutableArray<IconTile> GroupCopies(IEnumerable<IconTile> tiles) =>
        [.. tiles.GroupBy(tile => (tile.Name, tile.Icon)).Select(group => group.First() with { Copies = group.Count() })];

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
