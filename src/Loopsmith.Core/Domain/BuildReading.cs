using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure reads of a build the slices share; the kernel's matching and phrasing take the result as a parameter.</summary>
public static class BuildReading
{
    /// <summary>
    /// The damage type of the build's Super: its element's affinity in the catalog (Arc Staff: Arc). None when the build's
    /// Super is "?", the catalog doesn't have it, or its affinity is no damage type (neutral, prismatic): a "weapon of your
    /// Super's element" then matches nothing, and its phrase names no element.
    /// </summary>
    public static Optional<DamageType> ReadSuperType(this ValidatedBuild build) =>
        build.Build.ReadSuperType(build.Catalog);

    /// <inheritdoc cref="ReadSuperType(ValidatedBuild)"/>
    public static Optional<DamageType> ReadSuperType(this Build build, RuleCatalog catalog) =>
        build.Abilities.Super
            .Bind(super => catalog.Elements.TryGetValue(super, out var element) ? Optional.Some(element.Affinity) : Optional.None<Affinity>())
            .Bind(affinity => affinity is Affinity.Neutral or Affinity.Prismatic ? Optional.None<DamageType>() : Optional.Some(affinity.ToDamageType()));
}
