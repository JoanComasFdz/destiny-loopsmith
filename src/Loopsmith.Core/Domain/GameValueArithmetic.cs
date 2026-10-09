using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A number resolved for use, with how far to trust it. <c>None</c> = unknown, never 0.</summary>
public sealed record ResolvedValue(Optional<decimal> Value, Certainty Certainty);

/// <summary>Pure, dependency-free value arithmetic over <see cref="GameValue"/> (the allowed Domain exception).</summary>
public static class GameValueArithmetic
{
    /// <summary>Picks the value for <paramref name="copies"/> equipped copies (PerModCount: N-th value, clamped to the last).</summary>
    public static ResolvedValue ResolveForCopies(this GameValue value, int copies) =>
        value.Match(
            known => new ResolvedValue(Optional.Some(known.Value), Certainty.Known),
            perModCount => new ResolvedValue(
                Optional.Some(perModCount.Values[Math.Clamp(copies, 1, perModCount.Values.Length) - 1]),
                Certainty.Known),
            approximate => new ResolvedValue(Optional.Some(approximate.Value), Certainty.Approximate),
            _ => new ResolvedValue(Optional.None<decimal>(), Certainty.Unknown));

    /// <summary>The same value narrowed to <paramref name="copies"/> equipped copies (PerModCount → that copy's Known value).</summary>
    public static GameValue NarrowToCopies(this GameValue value, int copies) =>
        value is GameValue.PerModCount perModCount
            ? new GameValue.Known(perModCount.Values[Math.Clamp(copies, 1, perModCount.Values.Length) - 1])
            : value;

    public static Certainty CombineCertainty(this Certainty left, Certainty right) =>
        (Certainty)Math.Max((int)left, (int)right);

    public static DamageType ToDamageType(this Affinity affinity) =>
        affinity switch
        {
            Affinity.Arc => DamageType.Arc,
            Affinity.Solar => DamageType.Solar,
            Affinity.Void => DamageType.Void,
            Affinity.Stasis => DamageType.Stasis,
            Affinity.Strand => DamageType.Strand,
            _ => DamageType.Kinetic,
        };

    public static DamageType ToDamageType(this Subclass subclass) =>
        subclass switch
        {
            Subclass.Arc => DamageType.Arc,
            Subclass.Solar => DamageType.Solar,
            Subclass.Void => DamageType.Void,
            Subclass.Stasis => DamageType.Stasis,
            Subclass.Strand => DamageType.Strand,
            _ => DamageType.Kinetic,   // Prismatic: per-ability type is not modelled in v1
        };

    public static Affinity ToAffinity(this Subclass subclass) =>
        subclass switch
        {
            Subclass.Arc => Affinity.Arc,
            Subclass.Solar => Affinity.Solar,
            Subclass.Void => Affinity.Void,
            Subclass.Stasis => Affinity.Stasis,
            Subclass.Strand => Affinity.Strand,
            _ => Affinity.Prismatic,
        };

    public static Affinity ToAffinity(this DamageType type) =>
        type switch
        {
            DamageType.Arc => Affinity.Arc,
            DamageType.Solar => Affinity.Solar,
            DamageType.Void => Affinity.Void,
            DamageType.Stasis => Affinity.Stasis,
            DamageType.Strand => Affinity.Strand,
            _ => Affinity.Kinetic,
        };

    public static AbilityKind ToAbilityKind(this OffensiveAbility ability) =>
        ability switch
        {
            OffensiveAbility.Grenade => AbilityKind.Grenade,
            OffensiveAbility.Melee => AbilityKind.Melee,
            _ => AbilityKind.Super,
        };
}
