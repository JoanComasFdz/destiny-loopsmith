using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

public sealed record AbilityLoadout(ElementId Super, ElementId Grenade, ElementId Melee, ElementId ClassAbility);

public sealed record WeaponLoadout(WeaponSlot Slot, string Name, DamageType Type, Optional<string> Archetype, ImmutableArray<ElementId> Perks);

public sealed record StatLine(
    Optional<StatValue> Weapons,
    Optional<StatValue> Health,
    Optional<StatValue> Class,
    Optional<StatValue> Grenade,
    Optional<StatValue> Super,
    Optional<StatValue> Melee);

/// <summary>A build as authored (DIM-like composition), not yet validated against the catalog.</summary>
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
    StatLine Stats);

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
