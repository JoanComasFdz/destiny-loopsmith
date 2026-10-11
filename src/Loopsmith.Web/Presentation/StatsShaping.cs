using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Pure: how the Stats section words what it shows — the six stats in the game's order, a piece's or a total's points,
/// where a copy is, a slot — and the six typed values read into a stat line (Core reads each).
/// </summary>
public static class StatsShaping
{
    /// <summary>The six stats in the game's order, with their names.</summary>
    public static readonly ImmutableArray<(StatKind Stat, string Name)> Stats =
    [
        (StatKind.Weapons, "Weapons"), (StatKind.Health, "Health"), (StatKind.Class, "Class"),
        (StatKind.Grenade, "Grenade"), (StatKind.Super, "Super"), (StatKind.Melee, "Melee"),
    ];

    /// <summary>The build's stats as the six inputs show them: a number, or empty for "?".</summary>
    public static ImmutableArray<string> ToInputs(StatLine stats) =>
        [.. Stats.Select(stat => stats.ReadStat(stat.Stat).Match(value => value.Value.Value.ToString(CultureInfo.InvariantCulture), _ => ""))];

    /// <summary>The six inputs as a stat line, or what is wrong with each one that isn't a stat.</summary>
    public static Result<StatLine, ImmutableArray<string>> ReadInputs(ImmutableArray<string> inputs)
    {
        var read = Stats.Select((stat, index) => LoopDesigning.ReadStatInput(stat.Stat, index < inputs.Length ? inputs[index] : "")).ToImmutableArray();
        var problems = read.SelectMany(result => result.Match(_ => [], error => new[] { error.Failure })).ToImmutableArray();
        var values = read.Select(result => result.Match(ok => ok.Value, _ => Optional.None<StatValue>())).ToImmutableArray();
        return problems.IsEmpty
            ? new Result<StatLine, ImmutableArray<string>>.Ok(new StatLine(values[0], values[1], values[2], values[3], values[4], values[5]))
            : new Result<StatLine, ImmutableArray<string>>.Error(problems);
    }

    /// <summary>"Weapons 47 · Health ? · Class 104 · …": a stat line, "?" where it doesn't say.</summary>
    public static string DescribeStatLine(StatLine stats) =>
        string.Join(" · ", Stats.Select(stat => $"{stat.Name} {stats.ReadStat(stat.Stat).Match(value => value.Value.Value.ToString(CultureInfo.InvariantCulture), _ => "?")}"));

    /// <summary>"Weapons 5 · Health 30 · …": a copy's or a part's points (a part may be negative: "Grenade −10").</summary>
    public static string DescribePoints(StatPoints points) =>
        string.Join(" · ", Stats
            .Select(stat => (stat.Name, Value: ReadPoint(points, stat.Stat)))
            .Where(stat => stat.Value != 0)
            .Select(stat => $"{stat.Name} {(stat.Value < 0 ? "−" : "+")}{Math.Abs(stat.Value).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>A copy's points, every stat shown (its roll has 0 in three): "Weapons 0 · Health 30 · …".</summary>
    public static string DescribeRoll(StatPoints points) =>
        string.Join(" · ", Stats.Select(stat => $"{stat.Name} {ReadPoint(points, stat.Stat).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>Where a copy is: "worn by your Hunter", "on your Hunter", "in the vault".</summary>
    public static string DescribePlace(ArmorPlace place) =>
        place.Match(
            worn => $"worn by your {worn.Class}",
            carried => $"on your {carried.Class}",
            _ => "in the vault");

    public static string DescribeSlot(ArmorSlot slot) =>
        slot switch
        {
            ArmorSlot.Helmet => "Helmet",
            ArmorSlot.Arms => "Arms",
            ArmorSlot.Chest => "Chest",
            ArmorSlot.Legs => "Legs",
            _ => "Class item",
        };

    private static int ReadPoint(StatPoints points, StatKind stat) =>
        stat switch
        {
            StatKind.Weapons => points.Weapons,
            StatKind.Health => points.Health,
            StatKind.Class => points.Class,
            StatKind.Grenade => points.Grenade,
            StatKind.Super => points.Super,
            _ => points.Melee,
        };
}
