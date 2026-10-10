using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// The four ability slots. None is "?" in the build file: an ability the build has but Loopsmith can't name (a DIM
/// link's super that isn't in the catalog yet). Unknown is data: the slot shows "?" and sets nothing off.
/// </summary>
public sealed record AbilityLoadout(
    Optional<ElementId> Super,
    Optional<ElementId> Grenade,
    Optional<ElementId> Melee,
    Optional<ElementId> ClassAbility);

/// <summary>
/// An equipped weapon. <see cref="Perks"/> are named by catalog id; <see cref="Hash"/> is its manifest item when known (a
/// DIM loadout's, or written in the build file); <see cref="Roll"/> is the perk picked in each of its trait columns, by
/// manifest hash, in column order (none: not picked, "?" in the build file).
/// </summary>
public sealed record WeaponLoadout(
    WeaponSlot Slot,
    string Name,
    DamageType Type,
    Optional<string> Archetype,
    ImmutableArray<ElementId> Perks,
    Optional<ItemHash> Hash,
    ImmutableArray<Optional<ItemHash>> Roll);

public sealed record StatLine(
    Optional<StatValue> Weapons,
    Optional<StatValue> Health,
    Optional<StatValue> Class,
    Optional<StatValue> Grenade,
    Optional<StatValue> Super,
    Optional<StatValue> Melee);

/// <summary>
/// A build as authored (DIM-like composition), not yet validated against the catalog. <see cref="LeftOut"/> is what the
/// DIM loadout it came from had that the catalog doesn't know yet (empty for a build written by hand).
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
    ImmutableArray<LeftOutItem> LeftOut);

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
