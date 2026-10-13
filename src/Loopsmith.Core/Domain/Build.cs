using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// The four ability slots and the jump, each with what it holds (<see cref="AbilityPlug"/>). A jump is an element of the
/// rules like the abilities (<c>kind: jump</c>), but it is <c>inert</c>: movement has no part in a loop, so it sets
/// nothing off and the engine never plays it.
/// </summary>
public sealed record AbilityLoadout(
    AbilityPlug Super,
    AbilityPlug Grenade,
    AbilityPlug Melee,
    AbilityPlug ClassAbility,
    AbilityPlug Jump);

/// <summary>
/// What an ability slot holds: an element of the rules; an ability the build has but Loopsmith can't name (<c>"?"</c> in
/// the build file: a DIM link's super that isn't in the catalog yet; unknown is data, so the slot shows "?" and sets
/// nothing off); or nothing yet (a build started from scratch: the build file leaves its key out, the slot shows empty).
/// </summary>
[Union]
public partial record AbilityPlug
{
    partial record Known(ElementId Element);
    partial record Unknown;
    partial record Empty;
}

/// <summary>
/// An equipped weapon. <see cref="Perks"/> are named by catalog id; <see cref="Hash"/> is its manifest item when known (a
/// DIM loadout's, or written in the build file); <see cref="Roll"/> is the perk picked in each of its trait columns, by
/// manifest hash, in column order (none: not picked, "?" in the build file); <see cref="Catalyst"/> says the build turns
/// an exotic's catalyst on (a DIM loadout doesn't say, so it starts off); <see cref="Instance"/> is the copy it is, by
/// instance id (a dim.gg share's): after the player's Bungie login its tier, masterwork and breaker show when the copy
/// is theirs, else their own copy's. <see cref="Mod"/> is the weapon mod in its mod socket, by manifest hash (none: no
/// mod, or the build doesn't say).
/// </summary>
public sealed record WeaponLoadout(
    WeaponSlot Slot,
    string Name,
    DamageType Type,
    Optional<string> Archetype,
    ImmutableArray<ElementId> Perks,
    Optional<ItemHash> Hash,
    ImmutableArray<Optional<ItemHash>> Roll,
    bool Catalyst,
    Optional<ItemInstanceId> Instance,
    Optional<ItemHash> Mod);

/// <summary>
/// Where a weapon picked from the player's own goes in a build: in place of its weapon number <see cref="InPlaceOf.Weapon"/>
/// (0-based, in the build's order; it takes that weapon's slot), or into a slot the build has no weapon in.
/// </summary>
[Union]
public partial record WeaponTarget
{
    partial record InPlaceOf(int Weapon);
    partial record IntoEmpty(WeaponSlot Slot);
}

/// <summary>
/// One perk of a weapon as its build holds it (<c>BuildComposition.WeaponRolling.ListPerkSlots</c>): its intrinsic perk
/// (its frame, or an exotic's own perk); a perk the build names that is in no trait column the manifest excerpt gives;
/// a column that doesn't roll, with its one perk; a column that rolls, with what it rolls with and the perk there — the
/// roll's pick, else the perk the build names in it — or none ("?"); an exotic's catalyst, on or off. A column holds
/// one perk.
/// </summary>
[Union]
public partial record WeaponPerkSlot
{
    partial record Intrinsic(ItemHash Perk);
    partial record Named(ElementId Perk);
    partial record Fixed(int Column, ItemHash Perk);
    partial record Rolling(int Column, ImmutableArray<ItemHash> Options, Optional<ItemHash> Perk);
    partial record Catalyst(ItemHash Plug, bool On);
}

/// <summary>
/// A weapon mod a weapon's mod picker lists: the manifest's mod, whether the rules know it, whether it is an enhanced
/// one, and whether the weapon can take it — an enhanced mod only on a weapon known to be Tier 4 or 5 (its copy's tier,
/// after the player's Bungie login); a weapon whose tier isn't known takes the others only.
/// </summary>
public sealed record WeaponModOption(ManifestItem Mod, bool InRules, bool Enhanced, bool CanTake);

/// <summary>A perk in one of a weapon's trait columns (<see cref="Column"/> 0-based, in column order).</summary>
public sealed record ColumnPerk(int Column, ItemHash Perk);

/// <summary>How many pieces of an armor set some items are (a DIM loadout's armor).</summary>
public sealed record WornSet(ArmorSet Set, PieceCount Pieces);

/// <summary>
/// An armor set as a build shows it: the set, how many of its pieces the build wears (none: the build doesn't say —
/// a build file names its set bonuses, not its armor) and each of its perks.
/// </summary>
public sealed record ArmorSetStatus(ArmorSet Set, Optional<PieceCount> Pieces, ImmutableArray<SetPerkStatus> Perks);

/// <summary>A set perk: active when the build has it as a set bonus or wears enough pieces; its element when the rules know it.</summary>
public sealed record SetPerkStatus(SetPerk Perk, bool Active, Optional<ElementId> Element);

/// <summary>A build's artifact (the season's, from the manifest excerpt) and the build's perks it unlocks, in the build's order.</summary>
public sealed record BuildArtifact(ManifestItem Item, ImmutableArray<ArtifactPerkOfBuild> Perks);

/// <summary>One of the build's artifact perks: its hash in the artifact, and its element when the rules know it (else left out).</summary>
public sealed record ArtifactPerkOfBuild(ItemHash Hash, Optional<ElementId> Element);

/// <summary>
/// An artifact and its perks as the designer's artifact editor holds them, by manifest hash (no artifact: no perks).
/// </summary>
public sealed record ArtifactChoice(Optional<ItemHash> Artifact, ImmutableArray<ItemHash> Perks);

/// <summary>
/// One of an artifact's limits (Artifacts 2.0): at most <see cref="Capacity"/> of its perks from Tier
/// <see cref="FromTier"/> to <see cref="ToTier"/> (its last) — as many as its perk slots that take Tier
/// <see cref="FromTier"/> — of which <see cref="Used"/> are picked.
/// </summary>
public sealed record ArtifactLimit(int FromTier, int ToTier, int Used, int Capacity);

public sealed record StatLine(
    Optional<StatValue> Weapons,
    Optional<StatValue> Health,
    Optional<StatValue> Class,
    Optional<StatValue> Grenade,
    Optional<StatValue> Super,
    Optional<StatValue> Melee);

/// <summary>
/// Which copy of one of the build's armor pieces it wears: the piece's item hash and the copy's instance id (a dim.gg
/// share names each item's; picking a piece from the player's own armor records theirs). Unknown to the rules; the
/// Stats section adds the build's stats up from these copies when the logged-in player owns them.
/// </summary>
public sealed record ArmorCopy(ItemHash Piece, ItemInstanceId Instance);

/// <summary>
/// A build as authored (DIM-like composition), not yet validated against the catalog. <see cref="LeftOut"/> is what the
/// DIM loadout it came from had that the catalog doesn't know yet (empty for a build written by hand);
/// <see cref="ArmorCopies"/> which copy of its armor pieces it wears, when it says (a dim.gg share's, or picked);
/// <see cref="ArmorModPlugs"/> which copy of an armor mod the rules know it holds, for an element one of whose copies
/// isn't its first hash (the cheaper copy the seasonal artifact allows): each of the element's mods, in order, holds the
/// next of these that is one of its element's hashes, else the element's first hash; <see cref="Artifact"/> the artifact
/// it equips, by manifest hash, when it says (picked in the designer's artifact editor; none: the one that unlocks the
/// most of its artifact perks, <c>BuildComposition.ArtifactFinding</c>).
/// </summary>
public sealed record Build(
    string Name,
    Optional<string> Author,
    Optional<string> SourceUrl,
    Optional<CatalogVersion> PinnedCatalog,
    GuardianClass Class,
    Subclass Subclass,
    AbilityLoadout Abilities,
    ImmutableArray<ElementId> Aspects,
    ImmutableArray<ElementId> Fragments,
    Optional<ElementId> ExoticArmor,
    ImmutableArray<ElementId> ArmorSetBonuses,
    ImmutableArray<ElementId> ArmorMods,
    ImmutableArray<ElementId> ArtifactPerks,
    ImmutableArray<WeaponLoadout> Weapons,
    StatLine Stats,
    ImmutableArray<LeftOutItem> LeftOut,
    ImmutableArray<ArmorCopy> ArmorCopies,
    ImmutableArray<ItemHash> ArmorModPlugs,
    Optional<ItemHash> Artifact);

public sealed record BuildIssue(Severity Severity, string Message);

/// <summary>An element as equipped: mods stack (<see cref="Count"/> = how many copies).</summary>
public sealed record EquippedElement(BuildElement Element, int Count);

/// <summary>
/// Typestate: the only input Simulation accepts. Constructed only by BuildComposition
/// (architecture test), so "play steps before validation" cannot happen.
/// </summary>
public sealed record ValidatedBuild(
    Build Build,
    RuleCatalog Catalog,
    ImmutableArray<EquippedElement> Equipped,
    ImmutableArray<BuildIssue> Issues);
