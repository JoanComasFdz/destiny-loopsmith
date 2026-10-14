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
/// known yet. <see cref="Inert"/>: why it has no rules on purpose (damage only, outside the model), so the build check
/// doesn't take it for a gap. <see cref="StatChecks"/>: what the build check says about it at some values of a stat
/// (Gambler's Dodge below 50 Melee).
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
    Provenance Source,
    Optional<string> Inert,
    ImmutableArray<StatCheck> StatChecks);

/// <summary>
/// "With {value} Melee, a Gambler's Dodge gives back less than a full melee charge…": what the build check says when
/// the build's <see cref="Stat"/> is known and at least <see cref="From"/> (when given) and below <see cref="Below"/>;
/// <c>{value}</c> in <see cref="Says"/> is the build's value. A fact about the element at that stat, never counted.
/// </summary>
public sealed record StatCheck(StatKind Stat, Optional<StatValue> From, StatValue Below, StatCheckSeverity Severity, string Says);

/// <summary>How a stat check speaks: a warning or a note, never blocking (a stat doesn't keep a build from working).</summary>
public enum StatCheckSeverity { Warning, Info }

/// <summary>
/// Entry of the keyword glossary (the Compendium's "Statuses"); <see cref="Trait"/> is Bungie's trait for it (the
/// manifest's in-game keyword, with its icon), when it has one; <see cref="DrawnIcon"/> the shape the game draws for
/// it when Bungie's data has no icon for it (Unraveling Rounds); <see cref="Counted"/>: the engine counts its stacks
/// (Armor Charge, ADRs D33), where any other stacking status is only declared at its maximum (ADRs D3); <see cref="Decay"/>
/// how it loses stacks over time, as the source says it — a fact a loop never applies.
/// </summary>
public sealed record StatusDefinition(
    StatusId Id,
    string Name,
    KeywordKind Kind,
    Affinity Affinity,
    Optional<StackCount> MaxStacks,
    Optional<Seconds> Duration,
    Optional<ItemHash> Trait,
    Optional<DrawnIcon> DrawnIcon,
    bool Counted,
    Optional<string> Decay);

/// <summary>A pickup of the glossary (Ionic Trace, Orb of Power); <see cref="Trait"/> as for a status.</summary>
public sealed record PickupDefinition(PickupId Id, string Name, Affinity Affinity, bool CollectsAutomatically, Optional<ItemHash> Trait);

/// <summary>A summon of the glossary (Threadling); <see cref="Trait"/> as for a status.</summary>
public sealed record SummonDefinition(SummonId Id, string Name, DamageType DamageType, Optional<ItemHash> Trait);

public sealed record KeywordGlossary(
    ImmutableDictionary<StatusId, StatusDefinition> Statuses,
    ImmutableDictionary<PickupId, PickupDefinition> Pickups,
    ImmutableDictionary<SummonId, SummonDefinition> Summons,
    ImmutableArray<SubclassDefinition> Subclasses,
    ImmutableArray<FinisherDefinition> Finishers);

/// <summary>
/// A subclass item of the manifest (Arcstrider: the Arc Hunter subclass), so a DIM loadout's subclass is recognised by
/// its hash (ADRs D27). Only the subclasses whose hashes a source gives are listed.
/// </summary>
public sealed record SubclassDefinition(GuardianClass Class, Subclass Subclass, string Name, ImmutableArray<ItemHash> Hashes);

/// <summary>
/// A class's own finisher (Blade Carnival for a Hunter): the one that plays when the player has none equipped, by its
/// manifest hash. The designer shows its icon for a finisher until a Bungie login names the one equipped.
/// </summary>
public sealed record FinisherDefinition(GuardianClass Class, string Name, ItemHash Hash);

/// <summary>
/// Every authored element plus the keyword glossary, and the manifest excerpt that names their hashes (and those of the
/// saved DIM shares). Keyword elements are active in every build. <see cref="EnhancedPerks"/>: each weapon perk with an
/// enhanced trait (its own plug, with the Compendium's ↑ figures) → that trait's element; the excerpt's other plugs of the
/// same enhanced trait are that element's too (RuleParsing's <c>EnhancedPerkRecognition</c>).
/// </summary>
public sealed record RuleCatalog(
    CatalogVersion Version,
    KeywordGlossary Glossary,
    ImmutableDictionary<ElementId, BuildElement> Elements,
    ManifestExcerpt Manifest,
    ImmutableDictionary<ElementId, ElementId> EnhancedPerks);
