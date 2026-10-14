using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Pure, dependency-free value arithmetic over <see cref="GameValue"/> (the allowed Domain exception).</summary>
public static class GameValueArithmetic
{
    /// <summary>How far to trust a value shown as a fact: known (a per-copy value too), approximate, or unknown — never 0.</summary>
    public static Certainty ReadCertainty(this GameValue value) =>
        value.Match(
            _ => Certainty.Known,
            _ => Certainty.Known,
            _ => Certainty.Approximate,
            _ => Certainty.Unknown);

    /// <summary>The same value narrowed to <paramref name="copies"/> equipped copies (PerModCount → that copy's Known value).</summary>
    public static GameValue NarrowToCopies(this GameValue value, int copies) =>
        value is GameValue.PerModCount perModCount
            ? new GameValue.Known(perModCount.Values[Math.Clamp(copies, 1, perModCount.Values.Length) - 1])
            : value;

    /// <summary>The stacks a grant gives with <paramref name="copies"/> equipped copies (PerModCount → that copy's count, the last for more).</summary>
    public static StackCount ReadStacks(this StackGrant grant, int copies) =>
        grant.Match(
            fixedCount => fixedCount.Count,
            perModCount => perModCount.Counts[Math.Clamp(copies, 1, perModCount.Counts.Length) - 1]);

    /// <summary>The same grant narrowed to <paramref name="copies"/> equipped copies (PerModCount → that copy's fixed count).</summary>
    public static StackGrant NarrowToCopies(this StackGrant grant, int copies) =>
        new StackGrant.Fixed(grant.ReadStacks(copies));

    /// <summary>
    /// Damage resistances together, the way the game stacks them: each multiplies what the others let through,
    /// 1 − (1 − a) × (1 − b) × … (compendium Armor Mods#7). An unknown one — or a per-copy one not yet narrowed to its
    /// copies (<see cref="NarrowToCopies"/>) — makes the total unknown, never a total without it; an approximate one makes
    /// it approximate; none is 0.
    /// </summary>
    public static GameValue CombineResistances(this IEnumerable<GameValue> resistances) =>
        resistances.Aggregate<GameValue, GameValue>(new GameValue.Known(0m), CombineTwoResistances);

    private static GameValue CombineTwoResistances(GameValue total, GameValue next) =>
        (total, next) switch
        {
            (GameValue.Known known, GameValue.Known other) => new GameValue.Known(MultiplyResistances(known.Value, other.Value)),
            (GameValue.Known known, GameValue.Approximate other) => new GameValue.Approximate(MultiplyResistances(known.Value, other.Value)),
            (GameValue.Approximate approximate, GameValue.Known other) => new GameValue.Approximate(MultiplyResistances(approximate.Value, other.Value)),
            (GameValue.Approximate approximate, GameValue.Approximate other) => new GameValue.Approximate(MultiplyResistances(approximate.Value, other.Value)),
            _ => new GameValue.Unknown(),
        };

    private static decimal MultiplyResistances(decimal first, decimal second) =>
        1m - ((1m - first) * (1m - second));

    /// <summary>The fixed time a duration states; none when it states none, or goes by the stacks spent.</summary>
    public static Optional<Seconds> ReadFixedTime(this BuffDuration duration) =>
        duration is BuffDuration.Fixed fixedTime ? Optional.Some(fixedTime.Time) : Optional.None<Seconds>();

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
