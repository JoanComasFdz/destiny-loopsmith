using System.Collections.Immutable;
using System.Globalization;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// What tapping a square opens: an element of the rules (and Bungie's text for its item), an item only the manifest
/// excerpt knows, an exotic weapon (its intrinsic, perks and catalyst) or its catalyst. Legendary armor and weapons
/// open nothing: their perks and mods carry what there is to know.
/// </summary>
[Union]
public partial record ItemDetails
{
    partial record OfElement(ElementId Element);
    partial record OfItem(ItemHash Hash);
    partial record OfWeapon(int Weapon);
    partial record OfCatalyst(int Weapon);
}

/// <summary>
/// One square of the DIM-style loadout: the item's icon (a bungie.net address) or none, its name, colour and rarity,
/// whether the rules know it (an item the DIM loadout had but the build leaves out is shown, dimmed), and what tapping
/// it opens (none: nothing to show).
/// </summary>
public sealed record IconTile(
    string Name,
    Optional<string> Icon,
    Affinity Affinity,
    Optional<ItemTier> Tier,
    bool InRules,
    Optional<string> Description,
    int Copies,
    Optional<ItemDetails> Details);

/// <summary>The subclass block: the subclass item, its super, the abilities (class, jump, melee, grenade), aspects, fragments.</summary>
public sealed record SubclassView(
    Optional<IconTile> Subclass,
    IconTile Super,
    ImmutableArray<IconTile> Abilities,
    ImmutableArray<IconTile> Aspects,
    ImmutableArray<IconTile> Fragments);

/// <summary>One perk a weapon's trait column rolls with, as the picker offers it: its hash, its tile (the rules' element when they know it) and its type as the game words it ("Enhanced Trait").</summary>
public sealed record PerkOption(ItemHash Hash, IconTile Tile, Optional<string> Type);

/// <summary>The perk a rolling column holds: its hash (which option it is) and its tile.</summary>
public sealed record PickedPerk(ItemHash Hash, IconTile Tile);

/// <summary>
/// One perk square of a weapon, from the build's <see cref="WeaponPerkSlot"/>s: a perk that just shows (one the build
/// names in no column, or a column that doesn't roll), or a column that rolls — the perk it holds, if any ("?"), and
/// what it can roll with.
/// </summary>
[Union]
public partial record PerkSquare
{
    partial record Shown(IconTile Perk);
    partial record Pickable(int Column, Optional<PickedPerk> Perk, ImmutableArray<PerkOption> Options);
}

/// <summary>An exotic's catalyst square: the catalyst item's tile and whether the build turns it on.</summary>
public sealed record CatalystSquare(IconTile Tile, bool On);

/// <summary>
/// A weapon: where it is in the build (<see cref="Index"/>, what a pick names), its tile, its light.gg page, slot,
/// archetype, damage type (and that type's icon), its perk squares — one per trait column the excerpt gives, then the
/// perks the build names in none of them — and an exotic's catalyst.
/// </summary>
public sealed record WeaponView(
    int Index,
    IconTile Weapon,
    Optional<string> Page,
    WeaponSlot Slot,
    Optional<string> Archetype,
    DamageType Type,
    Optional<string> DamageIcon,
    ImmutableArray<PerkSquare> Perks,
    Optional<CatalystSquare> Catalyst);

/// <summary>An exotic's catalyst turned on or off in the designer (weapon 0-based, in the build's order).</summary>
public sealed record CatalystToggle(int Weapon, bool On);

/// <summary>A set perk's square: its tile, how many pieces it needs and whether it is active.</summary>
public sealed record SetPerkView(IconTile Tile, int Pieces, bool Active);

/// <summary>An armor set: its name, the pieces the build wears (none: the build doesn't say) and its perks.</summary>
public sealed record ArmorSetView(string Name, Optional<int> Pieces, ImmutableArray<SetPerkView> Perks);

/// <summary>A perk picked in the designer: weapon <see cref="Weapon"/>'s column <see cref="Column"/> (both 0-based, in the build's order), or back to "?" when none.</summary>
public sealed record PerkPick(int Weapon, int Column, Optional<ItemHash> Perk);

/// <summary>An armor slot: the piece in it (when the loadout names one) and the mods that only fit that slot, one tile each.</summary>
public sealed record ArmorView(ArmorSlot Slot, Optional<IconTile> Piece, ImmutableArray<IconTile> Mods);

/// <summary>
/// The artifact block: the season's artifact the perks are from (when the manifest excerpt has it: tapping it shows
/// Bungie's text) and the artifact perks the build has, then those the DIM loadout had that the rules don't know.
/// </summary>
public sealed record ArtifactView(Optional<IconTile> Item, ImmutableArray<IconTile> Perks);

/// <summary>
/// A build laid out like DIM shows a loadout: subclass, artifact, weapons (kinetic, energy, power), armor with each
/// piece's mods, general mods, the armor sets with their perks, and set bonuses in no set the excerpt has.
/// </summary>
public sealed record LoadoutView(
    SubclassView Subclass,
    ArtifactView Artifact,
    ImmutableArray<WeaponView> Weapons,
    ImmutableArray<ArmorView> Armor,
    ImmutableArray<IconTile> GeneralMods,
    ImmutableArray<ArmorSetView> ArmorSets,
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
    private const string PageHost = "https://www.light.gg/db/items/";

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
                .Select(entry => (Tile: DescribeLeftOut(catalog, entry), Slot: entry.Item.Bind(item => item.ReadArmorSlot()))))
            .ToImmutableArray();
        var pieces = leftOut
            .Where(entry => entry.Part == LoadoutPart.Item && IsKind<ManifestKind.Armor>(entry.Item))
            .Select(entry => (Slot: entry.Item.Bind(item => item.ReadArmorSlot()), Tile: DescribeLeftOut(catalog, entry)))
            .Concat(ToSome(b.ExoticArmor).Select(id => (Slot: catalog.FindElementItem(id).Bind(item => item.ReadArmorSlot()), Tile: DescribeElement(catalog, id))))
            .ToImmutableArray();

        var subclass = new SubclassView(
            catalog.FindSubclassItem(b.Class, b.Subclass).Map(item => DescribeItem(item, item.Name, true, Optional.None<string>(), b.Subclass.ToAffinity(),
                Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Hash)))),
            DescribeAbility<ManifestKind.Super>(catalog, b.Abilities.Super, plugs),
            [
                DescribeAbility<ManifestKind.ClassAbility>(catalog, b.Abilities.ClassAbility, plugs),
                .. DescribeLeftOutPlugs<ManifestKind.Movement>(catalog, plugs),
                DescribeAbility<ManifestKind.Melee>(catalog, b.Abilities.Melee, plugs),
                DescribeAbility<ManifestKind.Grenade>(catalog, b.Abilities.Grenade, plugs),
            ],
            [.. b.Aspects.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Aspect>(catalog, plugs)],
            [.. b.Fragments.Select(id => DescribeElement(catalog, id)), .. DescribeLeftOutPlugs<ManifestKind.Fragment>(catalog, plugs)]);
        return new LoadoutView(
            subclass,
            DescribeArtifact(build, [.. leftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk)]),
            [.. b.Weapons.Select((weapon, index) => DescribeWeapon(build, weapon, index)).OrderBy(view => view.Slot)],
            [
                .. ArmorSlots
                    .Select(slot => new ArmorView(
                        slot,
                        pieces.Where(piece => piece.Slot == Optional.Some(slot)).Select(piece => Optional.Some(piece.Tile)).FindFirstSome(),
                        [.. mods.Where(mod => mod.Slot == Optional.Some(slot)).Select(mod => mod.Tile)]))
                    .Where(view => view.Piece.IsSome() || !view.Mods.IsEmpty),
            ],
            GroupCopies(mods.Where(mod => !mod.Slot.IsSome()).Select(mod => mod.Tile)),
            [.. LoopDesigning.ListArmorSets(build).Select(set => DescribeArmorSet(catalog, set))],
            GroupCopies(LoopDesigning.ListLooseSetBonuses(build).Select(id => DescribeElement(catalog, id))));
    }

    /// <summary>light.gg's page for an item of the manifest (a sandbox perk has none).</summary>
    public static Optional<string> ToPageUrl(RuleCatalog catalog, ItemHash hash) =>
        catalog.Manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Perk
            ? Optional.None<string>()
            : Optional.Some(PageHost + hash.Value.ToString(CultureInfo.InvariantCulture) + "/"));

    /// <summary>The icon of the build's subclass item (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<string> FindSubclassIcon(RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.FindSubclassItem(guardianClass, subclass).Bind(item => item.Icon).Map(ToIconUrl);

    /// <summary>The bungie.net address of a manifest icon path.</summary>
    public static string ToIconUrl(string path) => IconHost + path;

    /// <summary>An element's icon (the manifest's, by its hash); none for one without a hash (a keyword: Bolt Charge, Jolt).</summary>
    public static Optional<string> FindElementIcon(RuleCatalog catalog, ElementId id) =>
        catalog.FindElementItem(id).Bind(item => item.Icon).Map(ToIconUrl);

    private static IconTile DescribeElement(RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? catalog.FindElementItem(id).Match(
                item => DescribeItem(item.Value, element.Name, true, element.Description, element.Affinity, ToElementDetails(id)),
                _ => new IconTile(element.Name, Optional.None<string>(), element.Affinity, Optional.None<ItemTier>(), true, element.Description, 1, ToElementDetails(id)))
            : new IconTile(id.Value, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), true, Optional.None<string>(), 1, Optional.None<ItemDetails>());

    /// <summary>The build's artifact and its perks: the build's, then the left-out ones.</summary>
    private static ArtifactView DescribeArtifact(ValidatedBuild build, ImmutableArray<LeftOutEntry> leftOut)
    {
        var catalog = build.Catalog;
        var item = FindArtifact(build).Map(artifact =>
            DescribeItem(artifact, artifact.Name, true, artifact.Description, Affinity.Neutral, Optional.Some<ItemDetails>(new ItemDetails.OfItem(artifact.Hash))));
        return new ArtifactView(item, [.. build.Build.ArtifactPerks.Select(id => DescribeElement(catalog, id)), .. leftOut.Select(entry => DescribeLeftOut(catalog, entry))]);
    }

    /// <summary>
    /// The build's artifact: the one in the excerpt that unlocks the most of its artifact perks (the rules' and the ones
    /// a DIM loadout had that the rules don't know).
    /// </summary>
    public static Optional<ManifestItem> FindArtifact(ValidatedBuild build) =>
        build.Catalog.Manifest.FindArtifactOf(build.Build.ArtifactPerks
            .SelectMany(id => build.Catalog.Elements.TryGetValue(id, out var element) ? element.Hashes : [])
            .Concat(build.Build.LeftOut.Where(entry => entry.Part == LoadoutPart.ArtifactPerk).Select(entry => entry.Hash)));

    private static Optional<ItemDetails> ToElementDetails(ElementId id) => Optional.Some<ItemDetails>(new ItemDetails.OfElement(id));

    private static IconTile DescribeUnknownHash(RuleCatalog catalog, ItemHash hash) =>
        new(catalog.Manifest.DescribeItemHash(hash), Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
            Optional.Some("Not in Loopsmith's rules or its manifest excerpt yet."), 1, Optional.None<ItemDetails>());

    private static IconTile DescribeItem(ManifestItem item, string name, bool inRules, Optional<string> description, Affinity affinity, Optional<ItemDetails> details) =>
        new(name, item.Icon.Map(ToIconUrl), affinity, item.Tier, inRules, description, 1, details);

    /// <summary>
    /// A hash the build left out: the manifest's name and icon when the excerpt has it, else just the hash. It opens its
    /// manifest details, unless it is legendary armor or a legendary weapon (DIM's perks and mods say more).
    /// </summary>
    private static IconTile DescribeLeftOut(RuleCatalog catalog, LeftOutEntry entry) =>
        entry.Item.Match(
            known => DescribeItem(known.Value, known.Value.Name, false, Optional.Some("Not in Loopsmith's rules yet: left out of the build."), ToAffinity(known.Value),
                IsLegendaryGear(known.Value) ? Optional.None<ItemDetails>() : Optional.Some<ItemDetails>(new ItemDetails.OfItem(known.Value.Hash))),
            _ => DescribeUnknownHash(catalog, entry.Hash));

    private static bool IsLegendaryGear(ManifestItem item) =>
        item.Kind is ManifestKind.Armor or ManifestKind.Weapon && item.Tier != Optional.Some(ItemTier.Exotic);

    /// <summary>
    /// An ability slot: its element; or, when the build has "?", the left-out subclass plug of that kind the manifest
    /// names (dimmed); or a "?" tile.
    /// </summary>
    private static IconTile DescribeAbility<TKind>(RuleCatalog catalog, Optional<ElementId> ability, ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        ability.Match(
            known => DescribeElement(catalog, known.Value),
            _ => DescribeLeftOutPlugs<TKind>(catalog, plugs)
                .Select(Optional.Some)
                .FindFirstSome()
                .UnwrapOr(new IconTile(DomainPhrasing.Unknown, Optional.None<string>(), Affinity.Neutral, Optional.None<ItemTier>(), false,
                    Optional.Some("Not known: not in the rule catalog yet, so it sets nothing off."), 1, Optional.None<ItemDetails>())));

    private static IEnumerable<IconTile> DescribeLeftOutPlugs<TKind>(RuleCatalog catalog, ImmutableArray<LeftOutEntry> plugs)
        where TKind : ManifestKind =>
        plugs.Where(entry => IsKind<TKind>(entry.Item)).Select(entry => DescribeLeftOut(catalog, entry));

    private static WeaponView DescribeWeapon(ValidatedBuild build, WeaponLoadout weapon, int index)
    {
        var catalog = build.Catalog;
        var item = weapon.Hash.Bind(hash => catalog.Manifest.FindItem(hash));
        var details = item.Bind(known => known.Tier == Optional.Some(ItemTier.Exotic)
            ? Optional.Some<ItemDetails>(new ItemDetails.OfWeapon(index))
            : Optional.None<ItemDetails>());
        var tile = item.Match(
            known => DescribeItem(known.Value, weapon.Name, true, Optional.None<string>(), weapon.Type.ToAffinity(), details),
            _ => new IconTile(weapon.Name, Optional.None<string>(), weapon.Type.ToAffinity(), Optional.None<ItemTier>(), true, Optional.None<string>(), 1, details));
        var damageIcon = catalog.Manifest.DamageTypeIcons.TryGetValue(weapon.Type, out var icon) ? Optional.Some(ToIconUrl(icon)) : Optional.None<string>();
        var slots = LoopDesigning.ListPerkSlots(build, weapon);
        return new WeaponView(
            index,
            tile,
            weapon.Hash.Bind(hash => ToPageUrl(catalog, hash)),
            weapon.Slot,
            weapon.Archetype,
            weapon.Type,
            damageIcon,
            [.. slots.SelectMany(slot => DescribePerkSlot(catalog, slot))],
            slots.OfType<WeaponPerkSlot.Catalyst>().Select(catalyst => Optional.Some(DescribeCatalyst(catalog, catalyst, index))).FindFirstSome());
    }

    /// <summary>A perk square: the intrinsic and the catalyst have none (the weapon's details show the intrinsic; the catalyst has its own square).</summary>
    private static IEnumerable<PerkSquare> DescribePerkSlot(RuleCatalog catalog, WeaponPerkSlot slot) =>
        slot.Match<IEnumerable<PerkSquare>>(
            _ => [],
            named => [new PerkSquare.Shown(DescribeElement(catalog, named.Perk))],
            fixedPerk => [new PerkSquare.Shown(DescribeTrait(catalog, fixedPerk.Perk))],
            rolling =>
            [
                new PerkSquare.Pickable(
                    rolling.Column,
                    rolling.Perk.Map(hash => new PickedPerk(hash, DescribeTrait(catalog, hash))),
                    [.. rolling.Options.Select(hash => new PerkOption(hash, DescribeTrait(catalog, hash), catalog.Manifest.FindItem(hash).Bind(item => item.Type)))]),
            ],
            _ => []);

    private static CatalystSquare DescribeCatalyst(RuleCatalog catalog, WeaponPerkSlot.Catalyst catalyst, int weapon)
    {
        var details = Optional.Some<ItemDetails>(new ItemDetails.OfCatalyst(weapon));
        var element = catalog.FindElementByHash(catalyst.Plug);
        var tile = catalog.Manifest.FindItem(catalyst.Plug).Match(
            item => DescribeItem(item.Value, item.Value.Name, element.IsSome(), element.Bind(known => known.Description), Affinity.Neutral, details),
            _ => DescribeUnknownHash(catalog, catalyst.Plug) with { Details = details });
        return new CatalystSquare(tile, catalyst.On);
    }

    private static ArmorSetView DescribeArmorSet(RuleCatalog catalog, ArmorSetStatus set) =>
        new(set.Set.Name, set.Pieces.Map(pieces => pieces.Value), [.. set.Perks.Select(perk => new SetPerkView(
            perk.Element.Match(
                id => DescribeElement(catalog, id.Value),
                _ => catalog.Manifest.FindItem(perk.Perk.Perk).Match(
                    item => DescribeItem(item.Value, item.Value.Name, false, Optional.Some("Not in Loopsmith's rules yet."), Affinity.Neutral,
                        Optional.Some<ItemDetails>(new ItemDetails.OfItem(item.Value.Hash))),
                    _ => DescribeUnknownHash(catalog, perk.Perk.Perk))),
            perk.Perk.Pieces.Value,
            perk.Active))]);

    /// <summary>A perk of a weapon's roll by hash: its catalog element when the rules have it, else the manifest's (dimmed).</summary>
    private static IconTile DescribeTrait(RuleCatalog catalog, ItemHash hash) =>
        catalog.FindElementByHash(hash).Match(
            element => DescribeElement(catalog, element.Value.Id),
            _ => DescribeLeftOut(catalog, new LeftOutEntry(LoadoutPart.Item, catalog.Manifest.FindItem(hash), hash)));

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
