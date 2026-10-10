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
/// Pure: a validated build → its DIM-style view. Names, rarity, slots and icons come from the catalog and its manifest
/// excerpt (<c>rules/manifest.yaml</c>); what the excerpt doesn't know has no icon and keeps its catalog name (or "?").
/// Left-out items (a DIM loadout's, <see cref="Build.LeftOut"/>) sit where DIM would show them, marked as not in the
/// rules. Nothing is inferred: a mod goes under the armor piece whose slot the manifest gives it, every other mod is general.
/// </summary>
public static class LoadoutShaping
{
    private const string IconHost = "https://www.bungie.net";

    /// <summary>A weapon's selected perks: the two trait columns of its roll.</summary>
    private const int SelectedPerks = 2;

    private static readonly ImmutableArray<ArmorSlot> ArmorSlots = [ArmorSlot.Helmet, ArmorSlot.Arms, ArmorSlot.Chest, ArmorSlot.Legs, ArmorSlot.ClassItem];

    public static LoadoutView ShapeLoadout(ValidatedBuild build)
    {
        var b = build.Build;
        var catalog = build.Catalog;
        var manifest = catalog.Manifest;
        var leftOut = b.LeftOut.Select(item => (item.Part, Item: FindItem(manifest, item.Hash), item.Hash)).ToImmutableArray();
        var leftOutPlugs = leftOut.Where(entry => entry.Part == LoadoutPart.SubclassPlug).ToImmutableArray();
        var mods = b.ArmorMods
            .Select(id => (Tile: DescribeElement(catalog, id), Slot: FindElementItem(catalog, id).Bind(item => item.ArmorSlot)))
            .Concat(leftOut.Where(entry => entry.Part == LoadoutPart.ArmorMod)
                .Select(entry => (Tile: DescribeLeftOut(entry.Item, entry.Hash), Slot: entry.Item.Bind(item => item.ArmorSlot))))
            .ToImmutableArray();
        var pieces = leftOut
            .Where(entry => entry.Part == LoadoutPart.Item)
            .SelectMany(entry => ToSome(entry.Item).Where(item => item.Kind == ManifestKind.Armor).Select(item => (Slot: item.ArmorSlot, Tile: DescribeLeftOut(entry.Item, entry.Hash))))
            .Concat(ToSome(b.ExoticArmor).Select(id => (Slot: FindElementItem(catalog, id).Bind(item => item.ArmorSlot), Tile: DescribeElement(catalog, id))))
            .ToImmutableArray();

        var subclass = new SubclassView(
            FindSubclassItem(catalog, b.Class, b.Subclass).Map(item => DescribeItem(item, item.Name, true, Optional.None<string>(), b.Subclass.ToAffinity())),
            DescribeAbility(catalog, b.Abilities.Super, leftOutPlugs, ManifestKind.Super),
            [
                DescribeAbility(catalog, b.Abilities.ClassAbility, leftOutPlugs, ManifestKind.ClassAbility),
                .. leftOutPlugs.Where(entry => IsKind(entry.Item, ManifestKind.Movement)).Select(entry => DescribeLeftOut(entry.Item, entry.Hash)),
                DescribeAbility(catalog, b.Abilities.Melee, leftOutPlugs, ManifestKind.Melee),
                DescribeAbility(catalog, b.Abilities.Grenade, leftOutPlugs, ManifestKind.Grenade),
            ],
            [.. b.Aspects.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs(leftOutPlugs, ManifestKind.Aspect)],
            [.. b.Fragments.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs(leftOutPlugs, ManifestKind.Fragment)]);
        return new LoadoutView(
            subclass,
            [
                .. b.ArtifactPerks.Select(id => DescribeElement(catalog, id)),
                .. leftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk).Select(entry => DescribeLeftOut(entry.Item, entry.Hash)),
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
        FindSubclassItem(catalog, guardianClass, subclass).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>The bungie.net address of a manifest icon path.</summary>
    public static string ToIconUrl(string path) => IconHost + path;

    /// <summary>"Kinetic", "Energy", "Heavy": a weapon slot as Destiny names it.</summary>
    public static string DescribeSlot(WeaponSlot slot) =>
        slot switch
        {
            WeaponSlot.Kinetic => "Kinetic",
            WeaponSlot.Energy => "Energy",
            _ => "Heavy",
        };

    /// <summary>"grenade-launcher" → "Grenade Launcher": a weapon archetype as a label.</summary>
    public static string DescribeArchetype(string archetype) =>
        string.Join(' ', archetype.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    /// <summary>"Helmet", "Class item": an armor slot as a label.</summary>
    public static string DescribeSlot(ArmorSlot slot) =>
        slot switch
        {
            ArmorSlot.Helmet => "Helmet",
            ArmorSlot.Arms => "Arms",
            ArmorSlot.Chest => "Chest",
            ArmorSlot.Legs => "Legs",
            _ => "Class item",
        };

    private static Optional<ManifestItem> FindItem(ManifestExcerpt manifest, ItemHash hash) =>
        manifest.Items.TryGetValue(hash, out var item) ? Optional.Some(item) : Optional.None<ManifestItem>();

    /// <summary>The manifest item of an element: the first of its hashes the excerpt has (an element's copies share their look).</summary>
    private static Optional<ManifestItem> FindElementItem(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Select(hash => FindItem(catalog.Manifest, hash)).FindFirstSome()
            : Optional.None<ManifestItem>();

    private static Optional<ManifestItem> FindSubclassItem(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.Glossary.Subclasses
            .Where(definition => definition.Class == guardianClass && definition.Subclass == subclass)
            .SelectMany(definition => definition.Hashes)
            .Select(hash => FindItem(catalog.Manifest, hash))
            .FindFirstSome();

    private static IconTile DescribeElement(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? FindElementItem(catalog, id).Match(
                item => DescribeItem(item.Value, element.Name, true, element.Description, element.Affinity),
                _ => new IconTile(element.Name, Optional.None<string>(), element.Affinity, Optional.None<ItemTier>(), true, element.Description, 1))
            : new IconTile(id.Value, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), true, Optional.None<string>(), 1);

    private static IconTile DescribeItem(ManifestItem item, string name, bool inRules, Optional<string> description, Affinity affinity) =>
        new(name, item.Icon.Map(ToIconUrl), affinity, item.Tier, inRules, description, 1);

    /// <summary>A hash the build left out: the manifest's name and icon when the excerpt has it, else just the hash.</summary>
    private static IconTile DescribeLeftOut(Optional<ManifestItem> item, ItemHash hash) =>
        item.Match(
            known => DescribeItem(known.Value, known.Value.Name, false, Optional.Some("Not in Loopsmith's rules yet: left out of the build."), ToAffinity(known.Value)),
            _ => new IconTile(hash.Value.ToString(CultureInfo.InvariantCulture), Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                Optional.Some("Not in Loopsmith's rules or its manifest excerpt yet."), 1));

    /// <summary>
    /// An ability slot: its element; or, when the build has "?", the left-out subclass plug of that kind the manifest
    /// names (dimmed); or a "?" tile.
    /// </summary>
    private static IconTile DescribeAbility(
        RuleCatalog catalog,
        Optional<ElementId> ability,
        ImmutableArray<(LoadoutPart Part, Optional<ManifestItem> Item, ItemHash Hash)> leftOutPlugs,
        ManifestKind kind) =>
        ability.Match(
            known => DescribeElement(catalog, known.Value),
            _ => leftOutPlugs
                .Where(entry => IsKind(entry.Item, kind))
                .Select(entry => Optional.Some(DescribeLeftOut(entry.Item, entry.Hash)))
                .FindFirstSome()
                .UnwrapOr(new IconTile(DomainPhrasing.Unknown, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                    Optional.Some("Not known: not in the rule catalog yet, so it sets nothing off."), 1)));

    private static IEnumerable<IconTile> DescribeLeftOutPlugs(
        ImmutableArray<(LoadoutPart Part, Optional<ManifestItem> Item, ItemHash Hash)> leftOutPlugs, ManifestKind kind) =>
        leftOutPlugs.Where(entry => IsKind(entry.Item, kind)).Select(entry => DescribeLeftOut(entry.Item, entry.Hash));

    private static WeaponView DescribeWeapon(RuleCatalog catalog, WeaponLoadout weapon)
    {
        var item = weapon.Hash.Bind(hash => FindItem(catalog.Manifest, hash));
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

    private static bool IsKind(Optional<ManifestItem> item, ManifestKind kind) =>
        item.Match(known => known.Value.Kind == kind, _ => false);

    private static Affinity ToAffinity(ManifestItem item) =>
        item.DamageType.Match(type => type.Value.ToAffinity(), _ => Affinity.Neutral);

    /// <summary>Copies of the same tile as one, with a count (general mods: "Grenade Mod ×5").</summary>
    private static ImmutableArray<IconTile> GroupCopies(IEnumerable<IconTile> tiles) =>
        [.. tiles.GroupBy(tile => (tile.Name, tile.Icon)).Select(group => group.First() with { Copies = group.Count() })];

    private static IEnumerable<T> ToSome<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
