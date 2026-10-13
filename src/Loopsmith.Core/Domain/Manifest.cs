using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// What an item of the Bungie manifest is, as Loopsmith shows it (the manifest's own item types, grouped). A weapon has
/// its slot, damage type, trait columns (the perks a roll selects; none when the excerpt doesn't say), intrinsic perk
/// (its frame, or an exotic's own perk), the weapon mods its mod socket takes and, for an exotic, its catalyst; armor its slot, intrinsic perk (an exotic's),
/// the armor set it belongs to and the class that wears it (none: any class); an armor mod the slot it only fits (none: a general mod); a catalyst the perks it
/// grants. A <see cref="Perk"/> is one of the manifest's sandbox perks: what a catalyst or an armor set grants.
/// </summary>
[Union]
public partial record ManifestKind
{
    partial record Subclass(ImmutableArray<ItemHash> Plugs);   // everything it can hold (an editable subclass's; else none)
    partial record Super();
    partial record Grenade();
    partial record Melee();
    partial record ClassAbility();
    partial record Movement();
    partial record Aspect();
    partial record Fragment();
    partial record Weapon(
        WeaponSlot Slot,
        Optional<DamageType> DamageType,
        ImmutableArray<TraitColumn> Traits,
        Optional<ItemHash> Intrinsic,
        Optional<ItemHash> Catalyst,
        Optional<ItemHash> Breaker,    // the champion it breaks, by its breaker type: its own (an exotic's), else its intrinsic frame's
        ImmutableArray<ItemHash> Mods); // the weapon mods its mod socket takes (none: it has no mod socket, or the excerpt doesn't say)
    partial record WeaponPerk();
    partial record WeaponMod(bool Enhanced);   // Enhanced: an enhanced weapon mod, which only a Tier 4 or 5 weapon takes
    partial record IntrinsicPerk();
    partial record ExoticCatalyst(ImmutableArray<ItemHash> Perks);
    partial record Armor(ArmorSlot Slot, Optional<ItemHash> Intrinsic, Optional<ItemHash> Set, Optional<GuardianClass> Class);
    partial record ArmorMod(Optional<ArmorSlot> Slot);
    partial record ArtifactPerk();
    partial record Artifact(ImmutableArray<ItemHash> Perks);   // a season's artifact and every perk it unlocks
    partial record Perk();
    partial record Other();
}

/// <summary>
/// One trait column of a weapon: the perks it can roll with (the manifest's plug set; enhanced versions included), or the
/// one perk it always has (an exotic's, or a column that doesn't roll).
/// </summary>
public sealed record TraitColumn(ImmutableArray<ItemHash> Options);

/// <summary>The armor slot of an armor piece, or of a mod that only fits that piece.</summary>
public enum ArmorSlot { Helmet, Arms, Chest, Legs, ClassItem }

/// <summary>An item's rarity in the manifest: exotics are framed in gold, legendaries in purple.</summary>
public enum ItemTier { Basic, Common, Rare, Legendary, Exotic }

/// <summary>
/// One item of the manifest excerpt (<c>rules/manifest.yaml</c>, ADRs D14): its official name, what it is, its type as
/// the game words it (a sandbox perk has none), its rarity, Bungie's description, its icon (a bungie.net path) and, for
/// a weapon or an armor piece, its season watermark (the overlay with its season's or expansion's icon in its top-left
/// corner, as the game and DIM draw it).
/// </summary>
public sealed record ManifestItem(
    ItemHash Hash,
    string Name,
    ManifestKind Kind,
    Optional<string> Type,
    Optional<ItemTier> Tier,
    Optional<string> Description,
    Optional<string> Icon,
    Optional<string> Watermark);

/// <summary>A perk an armor set grants once that many of its pieces are worn (2: its 2-piece perk).</summary>
public sealed record SetPerk(PieceCount Pieces, ItemHash Perk);

/// <summary>A set perk with the armor set it belongs to.</summary>
public sealed record PerkOfSet(ArmorSet Set, SetPerk Perk);

/// <summary>An armor set of the manifest (Luminopotent): its name and the perks its piece counts grant, fewest pieces first.</summary>
public sealed record ArmorSet(ItemHash Hash, string Name, ImmutableArray<SetPerk> Perks);

/// <summary>A breaker type of the manifest (Shield Piercing, Disruption, Stagger): the champion a weapon breaks, with its icon.</summary>
public sealed record BreakerType(ItemHash Hash, string Name, string Icon);

/// <summary>
/// The overlays Bungie draws on an item's icon, as DIM does: a gear tier's diamonds (tier 1 first) and the masterwork
/// glow (an exotic's its own) — bungie.net paths; none when the excerpt doesn't have them.
/// </summary>
public sealed record ItemOverlays(ImmutableArray<string> Tiers, Optional<string> Masterwork, Optional<string> ExoticMasterwork);
/// <summary>A trait of the manifest: the game's own keyword (Jolted, Bolt Charge, Woven Mail), its name and icon.</summary>
public sealed record KeywordTrait(ItemHash Hash, string Name, string Icon);

/// <summary>An armor stat as the manifest names and draws it (Weapons, Health…): an Armor 3.0 piece's highest shows on its square.</summary>
public sealed record ArmorStatIcon(StatKind Stat, string Name, string Icon);

/// <summary>
/// Loopsmith's excerpt of the Bungie manifest: the items the rules and the saved DIM shares name (and the perks,
/// catalysts and sets those have), the armor sets, the damage types' icons, every plug that changes an armor stat
/// (<see cref="StatPlugs"/>, for adding up a player's own armor), the breaker types, the overlays of an item's icon,
/// the traits the glossary's keywords are (<see cref="KeywordTraits"/>: their icons) and the armor stats' names and icons
/// (<see cref="ArmorStats"/>). Generated by
/// <c>tools/manifest/extract_manifest.py</c>; empty when the rules have none.
/// </summary>
public sealed record ManifestExcerpt(
    Optional<string> Version,
    ImmutableDictionary<DamageType, string> DamageTypeIcons,
    ImmutableDictionary<ItemHash, ArmorSet> ArmorSets,
    ImmutableDictionary<ItemHash, ManifestItem> Items,
    ImmutableDictionary<ItemHash, StatPlug> StatPlugs,
    ImmutableDictionary<ItemHash, BreakerType> BreakerTypes,
    ItemOverlays Overlays,
    ImmutableDictionary<ItemHash, KeywordTrait> KeywordTraits,
    ImmutableDictionary<StatKind, ArmorStatIcon> ArmorStats);

/// <summary>
/// Every weapon and armor piece of Bungie's manifest a build can wear — legendary and exotic, every version of each (a
/// season's reissue is an item of its own) — as the pickers' "Every item" lists them (read from the manifest's item
/// table): each a manifest item with its slot, damage type (and a weapon's breaker: its own, else its frame's) or class,
/// armor set and season watermark, without its perks (a pick reads those with the join); and the armor sets by hash.
/// With them, every armor mod an armor piece's mod sockets take (the mod picker's list) and, by the piece's hash, the
/// plug category of each of its mod sockets, in the game's order (a general socket, then those of its slot, then an
/// artifice, raid or activity socket when it has one).
/// </summary>
public sealed record ItemListing(
    ImmutableArray<ManifestItem> Items,
    ImmutableDictionary<ItemHash, ArmorSet> Sets,
    ImmutableArray<ListedMod> Mods,
    ImmutableDictionary<ItemHash, ImmutableArray<PlugCategoryId>> ModSockets);

/// <summary>
/// An armor mod of the listing: its manifest item (an <see cref="ManifestKind.ArmorMod"/> with the slot its category
/// fits; none for a general mod or one of a piece's own sockets), the plug category of the sockets it goes in, its
/// energy cost (Bungie gives none: it costs none) and what Bungie says it needs before it can be slotted ("Must Be
/// Selected in the Seasonal Artifact": the cheaper copy of a mod), Guardian Rank aside.
/// </summary>
public sealed record ListedMod(ManifestItem Item, PlugCategoryId Category, ArmorEnergy Cost, ImmutableArray<string> Requirements);
