using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of the manifest excerpt (ADRs D14) that the slices and the hosts share: what a hash, an element or a subclass is.</summary>
public static class ManifestReading
{
    public static Optional<ManifestItem> FindItem(this ManifestExcerpt manifest, ItemHash hash) =>
        manifest.Items.TryGetValue(hash, out var item) ? Optional.Some(item) : Optional.None<ManifestItem>();

    /// <summary>An element's manifest item: the first of its hashes the excerpt has (an element's copies share their look).</summary>
    public static Optional<ManifestItem> FindElementItem(this RuleCatalog catalog, ElementId id) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Select(hash => catalog.Manifest.FindItem(hash)).FindFirstSome()
            : Optional.None<ManifestItem>();

    /// <summary>The catalog element one of whose hashes this is (a hash names one element, the catalog checks it).</summary>
    public static Optional<BuildElement> FindElementByHash(this RuleCatalog catalog, ItemHash hash) =>
        catalog.Elements.Values
            .Where(element => element.Hashes.Contains(hash))
            .Select(Optional.Some)
            .FindFirstSome();

    /// <summary>The subclass item of a class's subclass (Arcstrider for an Arc Hunter), when the glossary and the excerpt have it.</summary>
    public static Optional<ManifestItem> FindSubclassItem(this RuleCatalog catalog, GuardianClass guardianClass, Subclass subclass) =>
        catalog.Glossary.Subclasses
            .Where(definition => definition.Class == guardianClass && definition.Subclass == subclass)
            .SelectMany(definition => definition.Hashes)
            .Select(hash => catalog.Manifest.FindItem(hash))
            .FindFirstSome();

    /// <summary>What the excerpt says about a build's weapon (its slot, columns, intrinsic, catalyst), when the build gives its hash.</summary>
    public static Optional<ManifestKind.Weapon> FindWeaponKind(this ManifestExcerpt manifest, WeaponLoadout weapon) =>
        weapon.Hash
            .Bind(hash => manifest.FindItem(hash))
            .Bind(item => item.Kind is ManifestKind.Weapon kind ? Optional.Some(kind) : Optional.None<ManifestKind.Weapon>());

    /// <summary>A weapon's trait columns: its manifest item's, when the build gives its hash and the excerpt has them; else none.</summary>
    public static ImmutableArray<TraitColumn> ListTraitColumns(this ManifestExcerpt manifest, WeaponLoadout weapon) =>
        manifest.FindWeaponKind(weapon).Match(kind => kind.Value.Traits, _ => ImmutableArray<TraitColumn>.Empty);

    /// <summary>The one of an element's hashes that is a perk of this trait column (Slice's, in Festival Flight's first), if any.</summary>
    public static Optional<ItemHash> FindColumnHash(this RuleCatalog catalog, ElementId id, TraitColumn column) =>
        catalog.Elements.TryGetValue(id, out var element)
            ? element.Hashes.Where(column.Options.Contains).Select(Optional.Some).FindFirstSome()
            : Optional.None<ItemHash>();

    /// <summary>True when one of an element's hashes is a perk of this trait column.</summary>
    public static bool IsInColumn(this RuleCatalog catalog, ElementId id, TraitColumn column) =>
        catalog.FindColumnHash(id, column).IsSome();

    /// <summary>The armor set a set perk belongs to, with the pieces it needs (Luminopotent's 2-piece, for Ionic Overclock).</summary>
    public static Optional<PerkOfSet> FindSetOfPerk(this ManifestExcerpt manifest, ItemHash perk) =>
        manifest.ArmorSets.Values
            .SelectMany(set => set.Perks.Where(setPerk => setPerk.Perk == perk).Select(setPerk => new PerkOfSet(set, setPerk)))
            .Select(Optional.Some)
            .FindFirstSome();

    /// <summary>The intrinsic perk of the first of these armor items that has one (an exotic's copies: only one carries it).</summary>
    public static Optional<ManifestItem> FindIntrinsicPerk(this ManifestExcerpt manifest, IEnumerable<ItemHash> armor) =>
        armor
            .Select(hash => manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Armor piece ? piece.Intrinsic : Optional.None<ItemHash>()))
            .FindFirstSome()
            .Bind(manifest.FindItem);

    /// <summary>The perks a catalyst grants (Return Stroke, for Thunderlord's), those the excerpt has.</summary>
    public static ImmutableArray<ManifestItem> ListCatalystPerks(this ManifestExcerpt manifest, ItemHash catalyst) =>
    [
        .. manifest.FindItem(catalyst)
            .Match(item => item.Value.Kind is ManifestKind.ExoticCatalyst granted ? granted.Perks : [], _ => ImmutableArray<ItemHash>.Empty)
            .SelectMany(perk => manifest.FindItem(perk).Match(found => new[] { found.Value }, _ => [])),
    ];

    /// <summary>The armor sets the items belong to, each with how many of them are its pieces (in the order first worn).</summary>
    public static ImmutableArray<WornSet> CountSetPieces(this ManifestExcerpt manifest, IEnumerable<ItemHash> items) =>
    [
        .. items
            .Distinct()
            .SelectMany(hash => manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Armor armor ? armor.Set : Optional.None<ItemHash>())
                .Match(set => new[] { set.Value }, _ => []))
            .GroupBy(set => set)
            .SelectMany(group => manifest.ArmorSets.TryGetValue(group.Key, out var set) ? new[] { new WornSet(set, PieceCount.From(group.Count())) } : []),
    ];

    /// <summary>The perks a set grants with that many pieces worn (2 pieces: its 2-piece perk).</summary>
    public static IEnumerable<SetPerk> ListActivePerks(this WornSet worn) =>
        worn.Set.Perks.Where(perk => perk.Pieces.Value <= worn.Pieces.Value);

    /// <summary>The excerpt with another's items and sets added (a joined part of the full manifest); what it has already stays.</summary>
    public static ManifestExcerpt AddExcerpt(this ManifestExcerpt excerpt, ManifestExcerpt more) =>
        excerpt with
        {
            Items = more.Items.Where(entry => !excerpt.Items.ContainsKey(entry.Key)).Aggregate(excerpt.Items, (items, entry) => items.Add(entry.Key, entry.Value)),
            ArmorSets = more.ArmorSets.Where(entry => !excerpt.ArmorSets.ContainsKey(entry.Key)).Aggregate(excerpt.ArmorSets, (sets, entry) => sets.Add(entry.Key, entry.Value)),
        };

    /// <summary>
    /// A weapon's roll from the plugs it holds (a DIM loadout's socket overrides, or a copy's sockets): each trait column's
    /// pick is the first of them it rolls with that an earlier column didn't take (a plug fills one column); none at all:
    /// no roll.
    /// </summary>
    public static ImmutableArray<Optional<ItemHash>> ReadRoll(this ImmutableArray<TraitColumn> columns, IEnumerable<ItemHash> plugs)
    {
        var held = plugs.ToImmutableArray();
        var roll = columns.Aggregate(ImmutableArray<Optional<ItemHash>>.Empty, (picked, column) => picked.Add(held
            .Where(column.Options.Contains)
            .Where(perk => !picked.Contains(Optional.Some(perk)))
            .Select(Optional.Some)
            .FindFirstSome()));
        return roll.Any(pick => pick.IsSome()) ? roll : [];
    }

    /// <summary>A weapon's archetype from its manifest type: "Grenade Launcher" → <c>grenade-launcher</c>; none when it isn't a slug.</summary>
    public static Optional<string> ToArchetype(string type)
    {
        var words = new string([.. type.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var slug = string.Join('-', words);
        return ElementId.IsSlug(slug) ? Optional.Some(slug) : Optional.None<string>();
    }

    /// <summary>A breaker type of the excerpt (Disruption), by its hash.</summary>
    public static Optional<BreakerType> FindBreakerType(this ManifestExcerpt manifest, ItemHash hash) =>
        manifest.BreakerTypes.TryGetValue(hash, out var breaker) ? Optional.Some(breaker) : Optional.None<BreakerType>();

    /// <summary>The overlay of a gear tier's diamonds (Bungie's; its tier-1 image is blank), when the excerpt has it.</summary>
    public static Optional<string> FindTierOverlay(this ItemOverlays overlays, GearTier tier) =>
        tier.Value <= overlays.Tiers.Length ? Optional.Some(overlays.Tiers[tier.Value - 1]) : Optional.None<string>();

    /// <summary>The masterwork glow Bungie draws on an item's icon: an exotic's its own.</summary>
    public static Optional<string> FindMasterworkOverlay(this ItemOverlays overlays, Optional<ItemTier> tier) =>
        tier == Optional.Some(ItemTier.Exotic) ? overlays.ExoticMasterwork : overlays.Masterwork;

    /// <summary>The armor slot of an armor piece, or of a mod that only fits it; none for anything else (a general mod).</summary>
    public static Optional<ArmorSlot> ReadArmorSlot(this ManifestItem item) =>
        item.Kind switch
        {
            ManifestKind.Armor armor => Optional.Some(armor.Slot),
            ManifestKind.ArmorMod mod => mod.Slot,
            _ => Optional.None<ArmorSlot>(),
        };
}
