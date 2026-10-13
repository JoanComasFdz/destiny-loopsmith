using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of a build the slices share; the kernel's matching and phrasing take the result as a parameter.</summary>
public static class BuildReading
{
    /// <summary>
    /// The damage type of the build's Super: its element's affinity in the catalog (Arc Staff: Arc). None when the build's
    /// Super is "?" or empty, the catalog doesn't have it, or its affinity is no damage type (neutral, prismatic): a "weapon of your
    /// Super's element" then matches nothing, and its phrase names no element.
    /// </summary>
    public static Optional<DamageType> ReadSuperType(this ValidatedBuild build) =>
        build.Build.ReadSuperType(build.Catalog);

    /// <inheritdoc cref="ReadSuperType(ValidatedBuild)"/>
    public static Optional<DamageType> ReadSuperType(this Build build, RuleCatalog catalog) =>
        build.Abilities.Super.ToElement()
            .Bind(super => catalog.Elements.TryGetValue(super, out var element) ? Optional.Some(element.Affinity) : Optional.None<Affinity>())
            .Bind(affinity => affinity is Affinity.Neutral or Affinity.Prismatic ? Optional.None<DamageType>() : Optional.Some(affinity.ToDamageType()));

    /// <summary>The slot's element of the rules; none when the slot holds "?" or nothing.</summary>
    public static Optional<ElementId> ToElement(this AbilityPlug plug) =>
        plug.Match(known => Optional.Some(known.Element), _ => Optional.None<ElementId>(), _ => Optional.None<ElementId>());

    /// <summary>True when the slot holds nothing (a build started from scratch); "?" is an ability the build has.</summary>
    public static bool IsEmpty(this AbilityPlug plug) =>
        plug is AbilityPlug.Empty;

    /// <summary>The four ability slots in the format's order: super, grenade, melee, class ability (not the jump: it sets nothing off).</summary>
    public static ImmutableArray<AbilityPlug> ListPlugs(this AbilityLoadout abilities) =>
        [abilities.Super, abilities.Grenade, abilities.Melee, abilities.ClassAbility];

    /// <summary>
    /// True when the element can go on a build of this class and subclass (<see cref="FitsClass"/> and
    /// <see cref="FitsSubclass"/>): an Arc fragment the rules author for the Hunter doesn't fit a Striker.
    /// </summary>
    public static bool Fits(this BuildElement element, GuardianClass guardianClass, Subclass subclass) =>
        element.FitsClass(guardianClass) && element.FitsSubclass(subclass);

    /// <summary>True when the element is the class's, or any class's.</summary>
    public static bool FitsClass(this BuildElement element, GuardianClass guardianClass) =>
        element.Class.Match(c => c.Value == guardianClass, _ => true);

    /// <summary>
    /// True when the element fits the subclass: a super, grenade, melee, jump, aspect or fragment of its element (or neutral
    /// or kinetic; Prismatic takes any); any other kind fits every subclass.
    /// </summary>
    public static bool FitsSubclass(this BuildElement element, Subclass subclass)
    {
        var isSubclassBound = element.Kind is ElementKind.Super or ElementKind.Grenade or ElementKind.Melee or ElementKind.Jump
            or ElementKind.Aspect or ElementKind.Fragment;
        var isNeutral = element.Affinity is Affinity.Neutral or Affinity.Kinetic;
        return !isSubclassBound || isNeutral || subclass == Subclass.Prismatic || element.Affinity == subclass.ToAffinity();
    }
}
