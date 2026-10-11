using System.Collections.Immutable;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Causality;

/// <summary>
/// What a passive reaches in a build: you, or — a change to weapon stats — the build's weapons of the archetypes it
/// names (by index), or none of them (it does nothing in this build).
/// </summary>
[Union]
public partial record PassiveReach
{
    partial record Player();
    partial record Weapons(ImmutableArray<int> Indices);
    partial record NoWeapon();
}

/// <summary>An equipped element's passive that a status switches on (its condition names it: "while Amplified"), the element's kind, and what it reaches.</summary>
public sealed record SwitchedOnPassive(ActivePassive Passive, ElementKind SourceKind, PassiveReach Reach);

/// <summary>
/// Kernel: which passives a status switches on — the state, the build check and an item's details read the same
/// answer. A status's own passive (Woven Mail's damage resistance is the woven-mail element's) is what the status does,
/// not something it switches on.
/// </summary>
public static class PassiveReaching
{
    /// <summary>Every equipped element's passive that this status switches on, in the build's order: an item's details.</summary>
    public static ImmutableArray<SwitchedOnPassive> ListSwitchedOn(this ValidatedBuild build, StatusId status) =>
    [
        .. build.Equipped
            .SelectMany(equipped => equipped.Element.Passives.Select(passive => (
                Passive: new ActivePassive(equipped.Element.Id, equipped.Element.Name, equipped.Element.Affinity, passive),
                equipped.Element.Kind)))
            .Where(switched => IsSwitchedOnBy(switched.Passive, status))
            .Select(switched => new SwitchedOnPassive(switched.Passive, switched.Kind, FindReach(build.Build, switched.Passive.Passive.Modifier))),
    ];

    /// <summary>A passive holding now that this status switches on (not the status's own).</summary>
    public static bool IsSwitchedOnBy(this ActivePassive passive, StatusId status) =>
        !passive.Source.IsStatusElement(status) && passive.Passive.When.Any(condition => IsNaming(condition, status));

    /// <summary>A change to weapon stats reaches the build's weapons of its archetypes (none: nothing here); anything else reaches you.</summary>
    public static PassiveReach FindReach(Build build, Passive passive) =>
        passive.Match<PassiveReach>(
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            weapons => ToWeaponReach(ListWeaponsOf(build, weapons.Archetypes)));

    /// <summary>Whether every weapon's archetype is known, so "no weapon is one of them" is a fact, not a guess.</summary>
    public static bool HasKnownArchetypes(this Build build) =>
        build.Weapons.All(weapon => weapon.Archetype.IsSome());

    private static ImmutableArray<int> ListWeaponsOf(Build build, ImmutableArray<string> archetypes) =>
    [
        .. build.Weapons
            .Select((weapon, index) => (Index: index, Matches: weapon.Archetype.Match(archetype => archetypes.Contains(archetype.Value), _ => false)))
            .Where(weapon => weapon.Matches)
            .Select(weapon => weapon.Index),
    ];

    private static PassiveReach ToWeaponReach(ImmutableArray<int> indices) =>
        indices.IsEmpty ? new PassiveReach.NoWeapon() : new PassiveReach.Weapons(indices);

    private static bool IsNaming(Condition condition, StatusId status) =>
        condition.Match(
            has => has.Status == status,
            _ => false,
            target => target.Status == status,
            atMax => atMax.Status == status);
}
