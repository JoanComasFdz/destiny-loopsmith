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

/// <summary>
/// Kernel: which passives a status switches on and where a passive reaches — the state, the build check and a keyword's
/// "In this build" read the same answer. A status's own passive (Woven Mail's damage resistance is the woven-mail
/// element's) is what the status does, not something it switches on; a "without X" guard is not switched on by X.
/// </summary>
public static class PassiveReaching
{
    /// <summary>A passive holding now that this status switches on (not the status's own).</summary>
    public static bool IsSwitchedOnBy(this ActivePassive passive, StatusId status) =>
        !passive.Source.IsStatusElement(status) && passive.Passive.IsSwitchedOnBy(status);

    /// <summary>A passive whose condition this status meets by being there (has, at max, on the pack) — not a "without" guard.</summary>
    public static bool IsSwitchedOnBy(this PassiveRule passive, StatusId status) =>
        passive.When.Any(condition => condition.IsNaming(status));

    /// <summary>A guard the status meets by being there: you have it, it is at max, the pack has it ("without it" is not one).</summary>
    public static bool IsNaming(this Condition condition, StatusId status) =>
        condition.Match(
            has => has.Status == status,
            _ => false,
            target => target.Status == status,
            atMax => atMax.Status == status);

    /// <summary>A change to weapon stats reaches the build's weapons of its archetypes (none: nothing here); anything else reaches you.</summary>
    public static PassiveReach FindReach(Build build, Passive passive) =>
        passive.Match<PassiveReach>(
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            _ => new PassiveReach.Player(),
            weapons => ToWeaponReach(ListWeaponsOf(build, weapons.Archetypes)),
            _ => new PassiveReach.Player());

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

}
