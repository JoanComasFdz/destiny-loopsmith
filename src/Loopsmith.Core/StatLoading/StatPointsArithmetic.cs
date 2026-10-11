using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.StatLoading;

/// <summary>Adding armor stat points: a piece's roll, a plug's stats, parts into a total.</summary>
internal static class StatPointsArithmetic
{
    internal static readonly StatPoints Zero = new(0, 0, 0, 0, 0, 0);

    /// <summary>The six stats in the game's order (Weapons first, Melee last).</summary>
    internal static readonly ImmutableArray<StatKind> AllStats =
        [StatKind.Weapons, StatKind.Health, StatKind.Class, StatKind.Grenade, StatKind.Super, StatKind.Melee];

    internal static int Read(this StatPoints points, StatKind stat) =>
        stat switch
        {
            StatKind.Weapons => points.Weapons,
            StatKind.Health => points.Health,
            StatKind.Class => points.Class,
            StatKind.Grenade => points.Grenade,
            StatKind.Super => points.Super,
            _ => points.Melee,
        };

    internal static StatPoints AddTo(this StatPoints points, StatKind stat, int value) =>
        stat switch
        {
            StatKind.Weapons => points with { Weapons = points.Weapons + value },
            StatKind.Health => points with { Health = points.Health + value },
            StatKind.Class => points with { Class = points.Class + value },
            StatKind.Grenade => points with { Grenade = points.Grenade + value },
            StatKind.Super => points with { Super = points.Super + value },
            _ => points with { Melee = points.Melee + value },
        };

    internal static StatPoints Add(this StatPoints points, StatPoints more) =>
        new(points.Weapons + more.Weapons, points.Health + more.Health, points.Class + more.Class,
            points.Grenade + more.Grenade, points.Super + more.Super, points.Melee + more.Melee);

    internal static StatPoints Sum(this IEnumerable<StatPoints> parts) =>
        parts.Aggregate(Zero, (total, part) => total.Add(part));

    /// <summary>A plug's stats as points (a piece's roll: no stat of it is for one class only).</summary>
    internal static StatPoints ToPoints(this StatPlug plug) =>
        plug.Stats.Aggregate(Zero, (points, stat) => points.AddTo(stat.Stat, stat.Value));

    /// <summary>A plug's stats as points, those for another class left out (Spark of Focus's −10 to a Titan's Health).</summary>
    internal static StatPoints ToPoints(this StatPlug plug, GuardianClass forClass) =>
        plug.Stats
            .Where(stat => stat.Class.Match(only => only.Value == forClass, _ => true))
            .Aggregate(Zero, (points, stat) => points.AddTo(stat.Stat, stat.Value));

    /// <summary>A plug's stats added only where <paramref name="roll"/> has 0 (a masterwork's, Balanced Tuning's).</summary>
    internal static StatPoints ToPointsOnEmpty(this StatPlug plug, StatPoints roll) =>
        plug.Stats
            .Where(stat => roll.Read(stat.Stat) == 0)
            .Aggregate(Zero, (points, stat) => points.AddTo(stat.Stat, stat.Value));

    internal static StatPoints Times(this StatPoints points, int copies) =>
        new(points.Weapons * copies, points.Health * copies, points.Class * copies,
            points.Grenade * copies, points.Super * copies, points.Melee * copies);
}
