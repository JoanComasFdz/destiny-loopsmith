using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// An element's damage resistance holding right then, for the copies equipped: its PvE value and, when it reduces any
/// damage, its PvP value (<c>?</c> when the source gives none). A resistance against combatants only has no PvP value: it
/// does nothing in PvP.
/// </summary>
public sealed record ResistanceSource(ElementId Element, string Name, Affinity Affinity, GameValue Pve, Optional<GameValue> Pvp);

/// <summary>
/// A line of the step's damage resistance (ADRs D29), its sources multiplied the way the game stacks them. The first line
/// is the resistance to all damage (<see cref="Against"/> none): every source that reduces any damage or all combatants'.
/// Each other line is against some damage only ("Arc combatants"): its total is the first line's sources and its own
/// together, its <see cref="Sources"/> only its own, and it has no PvP total (combatants only). The formulas show the
/// multiplication: "1 − (1 − 34.5%) × (1 − 45%) = 64%".
/// </summary>
public sealed record ResistanceLine(
    Optional<string> Against,
    GameValue Pve,
    Optional<GameValue> Pvp,
    ImmutableArray<ResistanceSource> Sources,
    string PveFormula,
    Optional<string> PvpFormula);

/// <summary>
/// Pure: the damage resistance passives holding at the end of a step → the step's resistance lines. A total is a fact
/// shown with its sources, never read to decide what happens (ADRs D29).
/// </summary>
public static class ResistanceShaping
{
    public static ImmutableArray<ResistanceLine> ShapeResistance(ValidatedBuild build, ImmutableArray<ActivePassive> passives)
    {
        var superType = build.ReadSuperType();
        var resistances = passives
            .SelectMany(passive => passive.Passive.Modifier is Passive.ResistDamage resist
                ? [ToScopedSource(build, passive, resist, superType)]
                : Array.Empty<ScopedSource>())
            .ToImmutableArray();
        var general = resistances
            .Where(entry => IsGeneral(entry.Scope))
            .OrderBy(entry => entry.Scope == ResistanceScope.Any)
            .Select(entry => entry.Source)
            .ToImmutableArray();
        var conditional = resistances
            .Where(entry => !IsGeneral(entry.Scope))
            .GroupBy(entry => entry.Scope)
            .OrderBy(group => group.Key)
            .Select(group => ShapeConditionalLine(general, group.Key, [.. group.Select(entry => entry.Source)], superType));
        return resistances.IsEmpty ? [] : [ShapeGeneralLine(general), .. conditional];
    }

    private sealed record ScopedSource(ResistanceScope Scope, ResistanceSource Source);

    private static ScopedSource ToScopedSource(ValidatedBuild build, ActivePassive passive, Passive.ResistDamage resist, Optional<DamageType> superType)
    {
        var copies = TraceRenderer.CountCopies(build, passive.Source);
        var pve = resist.Amount.Pve.NarrowToCopies(copies);
        var pvp = resist.Against == ResistanceScope.Any
            ? Optional.Some(resist.Amount.Pvp.Map(value => value.NarrowToCopies(copies)).UnwrapOr(new GameValue.Unknown()))
            : Optional.None<GameValue>();
        return new ScopedSource(
            ResolveHarmonic(resist.Against, superType),
            new ResistanceSource(passive.Source, passive.SourceName, passive.Affinity, pve, pvp));
    }

    private static bool IsGeneral(ResistanceScope scope) =>
        scope is ResistanceScope.Any or ResistanceScope.Combatants;

    /// <summary>The resistance to all damage: PvP counts only the sources that reduce any damage (none: 0%).</summary>
    private static ResistanceLine ShapeGeneralLine(ImmutableArray<ResistanceSource> sources)
    {
        var pve = sources.Select(source => source.Pve).ToImmutableArray();
        var pvp = sources.SelectMany(source => source.Pvp.Map(value => new[] { value }).UnwrapOr([])).ToImmutableArray();
        return new ResistanceLine(
            Optional.None<string>(),
            pve.CombineResistances(),
            Optional.Some(pvp.CombineResistances()),
            sources,
            DescribeFormula(pve),
            Optional.Some(DescribeFormula(pvp)));
    }

    private static ResistanceLine ShapeConditionalLine(
        ImmutableArray<ResistanceSource> general, ResistanceScope scope, ImmutableArray<ResistanceSource> sources, Optional<DamageType> superType)
    {
        var pve = general.Concat(sources).Select(source => source.Pve).ToImmutableArray();
        return new ResistanceLine(
            scope.DescribeResistanceScope(superType),
            pve.CombineResistances(),
            Optional.None<GameValue>(),
            sources,
            DescribeFormula(pve),
            Optional.None<string>());
    }

    /// <summary>"1 − (1 − 34.5%) × (1 − 45%) = 64%"; one source: "25%"; none: "0%: nothing reduces it".</summary>
    private static string DescribeFormula(ImmutableArray<GameValue> values) =>
        values.Length switch
        {
            0 => "0%: nothing reduces it",
            1 => values[0].FormatPercent(),
            _ => "1 − " + string.Join(" × ", values.Select(value => $"(1 − {value.FormatPercent()})")) + " = " + values.CombineResistances().FormatPercent(),
        };

    /// <summary>Harmonic Resistance is against the Super's element: on the line of that element when it is known.</summary>
    private static ResistanceScope ResolveHarmonic(ResistanceScope scope, Optional<DamageType> superType) =>
        scope == ResistanceScope.Harmonic
            ? superType.Map(ToResistanceScope).UnwrapOr(ResistanceScope.Harmonic)
            : scope;

    private static ResistanceScope ToResistanceScope(DamageType type) =>
        type switch
        {
            DamageType.Arc => ResistanceScope.Arc,
            DamageType.Solar => ResistanceScope.Solar,
            DamageType.Void => ResistanceScope.Void,
            DamageType.Stasis => ResistanceScope.Stasis,
            DamageType.Strand => ResistanceScope.Strand,
            _ => ResistanceScope.Harmonic,
        };
}
