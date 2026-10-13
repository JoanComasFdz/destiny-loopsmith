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

    /// <summary>
    /// Damage resistances together, the way the game stacks them: each multiplies what the others let through,
    /// 1 − (1 − a) × (1 − b) × … (compendium Armor Mods#7). An unknown one makes the total unknown (never a total without
    /// it), an approximate one makes it approximate; none is 0. Each value is one copy's (<see cref="NarrowToCopies"/>).
    /// </summary>
    public static GameValue CombineResistances(this IEnumerable<GameValue> resistances) =>
        resistances.Aggregate<GameValue, GameValue>(new GameValue.Known(0m), CombineTwoResistances);

    private static GameValue CombineTwoResistances(GameValue total, GameValue next) =>
        (total, next) switch
        {
            (GameValue.Unknown, _) or (_, GameValue.Unknown) or (GameValue.PerModCount, _) or (_, GameValue.PerModCount) => new GameValue.Unknown(),
            (GameValue.Known known, GameValue.Known other) => new GameValue.Known(MultiplyResistances(known.Value, other.Value)),
            _ => new GameValue.Approximate(MultiplyResistances(ReadNumber(total), ReadNumber(next))),
        };

    private static decimal MultiplyResistances(decimal first, decimal second) =>
        1m - ((1m - first) * (1m - second));

    private static decimal ReadNumber(GameValue value) =>
        value is GameValue.Known known ? known.Value : value is GameValue.Approximate approximate ? approximate.Value : 0m;

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
