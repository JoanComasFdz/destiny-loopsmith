using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// Cost model of an ability, straight from the Compendium (e.g. Threaded Spike: 145.2 s · 0.8x). Parsed and kept as
/// data; the engine doesn't use it while ability energy isn't simulated (ADRs D5).
/// </summary>
public sealed record AbilityProfile(AbilityKind Kind, int Charges, GameValue ChunkScalar, GameValue BaseCooldownSeconds);

/// <summary>
/// An ability, aspect, fragment, exotic, armor-set bonus, mod, artifact perk, weapon trait or keyword: all "an element
/// with rules". <see cref="Hashes"/> are its manifest hashes (ADRs D14): the first is its own, the others copies of
/// the same item with the same text (a mod's artifice copy, a class ability's copy on each subclass). Empty when not
/// known yet.
/// </summary>
public sealed record BuildElement(
    ElementId Id,
    string Name,
    ElementKind Kind,
    Affinity Affinity,
    Optional<GuardianClass> Class,
    ImmutableArray<ItemHash> Hashes,
    Optional<string> Description,
    ImmutableArray<Rule> Rules,
    ImmutableArray<PassiveRule> Passives,
    Optional<AbilityProfile> Ability,
    Optional<int> FragmentSlots,
    Provenance Source);

/// <summary>Entry of the keyword glossary (the Compendium's "Statuses").</summary>
public sealed record StatusDefinition(
    StatusId Id,
    string Name,
    KeywordKind Kind,
    Affinity Affinity,
    Optional<StackCount> MaxStacks,
    Optional<Seconds> Duration);

public sealed record PickupDefinition(PickupId Id, string Name, Affinity Affinity, bool CollectsAutomatically);

public sealed record SummonDefinition(SummonId Id, string Name, DamageType DamageType);

public sealed record KeywordGlossary(
    ImmutableDictionary<StatusId, StatusDefinition> Statuses,
    ImmutableDictionary<PickupId, PickupDefinition> Pickups,
    ImmutableDictionary<SummonId, SummonDefinition> Summons,
    ImmutableArray<SubclassDefinition> Subclasses);

/// <summary>
/// A subclass item of the manifest (Arcstrider: the Arc Hunter subclass), so a DIM loadout's subclass is recognised by
/// its hash (ADRs D27). Only the subclasses whose hashes a source gives are listed.
/// </summary>
public sealed record SubclassDefinition(GuardianClass Class, Subclass Subclass, string Name, ImmutableArray<ItemHash> Hashes);

/// <summary>
/// Every authored element plus the keyword glossary, and the manifest excerpt that names their hashes (and those of the
/// saved DIM shares). Keyword elements are active in every build.
/// </summary>
public sealed record RuleCatalog(
    CatalogVersion Version,
    KeywordGlossary Glossary,
    ImmutableDictionary<ElementId, BuildElement> Elements,
    ManifestExcerpt Manifest);
