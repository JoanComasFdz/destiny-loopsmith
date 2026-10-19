using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>An element's damage bonus holding right then, for the copies equipped (<c>?</c> when the source gives none).</summary>
public sealed record DamageBonusSource(ElementId Element, string Name, Affinity Affinity, GameValue Change);

/// <summary>
/// The damage bonuses against one damage source ("Melee", "Arc weapon"): each source with its own value, never added up
/// (ADRs D1).
/// </summary>
public sealed record DamageBonusLine(string Against, ImmutableArray<DamageBonusSource> Sources);

/// <summary>
/// Pure: the passives holding at the end of a step → the step's damage bonuses, one line per damage source they make
/// harder, in the order the passives hold. None without a source.
/// </summary>
public static class DamageBonusShaping
{
    public static ImmutableArray<DamageBonusLine> ShapeDamageBonus(ValidatedBuild build, ImmutableArray<ActivePassive> passives)
    {
        var superType = build.ReadSuperType();
        return
        [
            .. passives
                .SelectMany(passive => ToBonus(build, passive))
                .GroupBy(bonus => bonus.Against)
                .Select(group => new DamageBonusLine(
                    DomainPhrasing.Capitalize(build.Catalog.Glossary.DescribeDamageSource(group.Key, superType)),
                    [.. group.Select(bonus => bonus.Source)])),
        ];
    }

    /// <summary>A source and the damage it makes harder.</summary>
    private sealed record ScopedBonus(DamageSource Against, DamageBonusSource Source);

    private static IEnumerable<ScopedBonus> ToBonus(ValidatedBuild build, ActivePassive passive) =>
        passive.Passive.Modifier is Passive.ModifyDamage modify
            ? [new ScopedBonus(modify.Against, new DamageBonusSource(passive.Source, passive.SourceName, passive.Affinity, modify.Change.NarrowToCopies(build.CountCopies(passive.Source))))]
            : [];
}
