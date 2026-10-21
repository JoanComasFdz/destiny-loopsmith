using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.TraceRendering;

/// <summary>
/// An element's damage resistance holding right then, for the copies equipped: its PvE value and, when it reduces any
/// damage, its PvP value (<c>?</c> when the source gives none). A resistance against combatants only has no PvP value: it
/// does nothing in PvP.
/// </summary>
public sealed record ResistanceSource(ElementId Element, string Name, Affinity Affinity, GameValue Pve, Optional<GameValue> Pvp);

/// <summary>
/// The resistance to all damage: every source that reduces any damage or all combatants' damage, multiplied. PvP counts
/// only the sources that reduce any damage (none: 0%). The formulas show the multiplication: "1 − (1 − 34.5%) × (1 − 45%) = 64%".
/// </summary>
public sealed record AllDamageResistance(GameValue Pve, GameValue Pvp, ImmutableArray<ResistanceSource> Sources, string PveFormula, string PvpFormula);

/// <summary>
/// A resistance against some combatants' damage only ("Arc combatants"): all damage's sources and its own together, PvE
/// only; <see cref="Sources"/> are its own.
/// </summary>
public sealed record CombatantResistance(string Against, GameValue Pve, ImmutableArray<ResistanceSource> Sources, string PveFormula);

/// <summary>The step's damage resistance (ADRs D29): to all damage, and against some combatants' damage only.</summary>
public sealed record DamageResistance(AllDamageResistance AllDamage, ImmutableArray<CombatantResistance> AgainstSome);

/// <summary>
/// Pure: the damage resistance passives holding at the end of a step → the step's damage resistance, its sources
/// multiplied the way the game stacks them. A total is a fact shown with its sources, never read to decide what happens
/// (ADRs D29). None without a source.
/// </summary>
public static class ResistanceShaping
{
    public static Optional<DamageResistance> ShapeResistance(ValidatedBuild build, ImmutableArray<ActivePassive> passives)
    {
        var superType = build.ReadSuperType();
        var resistances = passives.SelectMany(passive => ToResistance(build, passive, superType)).ToImmutableArray();
        var general = resistances
            .Where(resistance => resistance.Against is CombatantDamage.All)
            .Select(resistance => resistance.Source)
            .OrderBy(source => source.Pvp.IsSome())
            .ToImmutableArray();
        var againstSome = resistances
            .Where(resistance => resistance.Against is not CombatantDamage.All)
            .GroupBy(resistance => resistance.Against)
            .Select(group => ShapeCombatantResistance(general, group.Key.DescribeCombatantDamage(superType), [.. group.Select(resistance => resistance.Source)]))
            .ToImmutableArray();
        return resistances.IsEmpty
            ? Optional.None<DamageResistance>()
            : Optional.Some(new DamageResistance(ShapeAllDamage(general), againstSome));
    }

    /// <summary>A source and the combatants' damage it reduces (<see cref="CombatantDamage.All"/>: any damage too).</summary>
    private sealed record ScopedSource(CombatantDamage Against, ResistanceSource Source);

    private static IEnumerable<ScopedSource> ToResistance(ValidatedBuild build, ActivePassive passive, Optional<DamageType> superType)
    {
        var copies = build.CountCopies(passive.Source);
        return passive.Passive.Modifier switch
        {
            Passive.ResistDamage any => [new ScopedSource(
                new CombatantDamage.All(),
                ToSource(passive, any.Amount.Pve.NarrowToCopies(copies), Optional.Some(any.Amount.Pvp.Map(pvp => pvp.NarrowToCopies(copies)).UnwrapOr(new GameValue.Unknown()))))],
            Passive.ResistCombatantDamage combatant => [new ScopedSource(
                ResolveSuperType(combatant.Against, superType),
                ToSource(passive, combatant.Amount.NarrowToCopies(copies), Optional.None<GameValue>()))],
            _ => [],
        };
    }

    private static ResistanceSource ToSource(ActivePassive passive, GameValue pve, Optional<GameValue> pvp) =>
        new(passive.Source, passive.SourceName, passive.Affinity, pve, pvp);

    private static AllDamageResistance ShapeAllDamage(ImmutableArray<ResistanceSource> sources)
    {
        var pve = sources.Select(source => source.Pve).ToImmutableArray();
        var pvp = sources.SelectMany(source => source.Pvp.Map(value => new[] { value }).UnwrapOr([])).ToImmutableArray();
        return new AllDamageResistance(pve.CombineResistances(), pvp.CombineResistances(), sources, DescribeFormula(pve), DescribeFormula(pvp));
    }

    private static CombatantResistance ShapeCombatantResistance(ImmutableArray<ResistanceSource> general, string against, ImmutableArray<ResistanceSource> sources)
    {
        var pve = general.Concat(sources).Select(source => source.Pve).ToImmutableArray();
        return new CombatantResistance(against, pve.CombineResistances(), sources, DescribeFormula(pve));
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
    private static CombatantDamage ResolveSuperType(CombatantDamage against, Optional<DamageType> superType) =>
        against is CombatantDamage.OfSuperType
            ? superType
                .Bind(type => type == DamageType.Kinetic ? Optional.None<CombatantDamage>() : Optional.Some<CombatantDamage>(new CombatantDamage.OfType(type)))
                .UnwrapOr(against)
            : against;
}
