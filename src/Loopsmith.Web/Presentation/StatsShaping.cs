using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Web.Presentation;

/// <summary>
/// Pure: how the Stats section words what it shows — the six stats in the game's order, a piece's or a total's points,
/// where a copy is, a slot.
/// </summary>
public static class StatsShaping
{
    /// <summary>The six stats in the order Core reads the inputs in (the game's), with their names.</summary>
    public static readonly ImmutableArray<(StatKind Stat, string Name)> Stats =
        [.. LoopDesigning.StatInputOrder.Select(stat => (stat, stat.ToString()))];

    /// <summary>The build's stats as the six inputs show them: a number, or empty for "?".</summary>
    public static ImmutableArray<string> ToInputs(StatLine stats) =>
        [.. Stats.Select(stat => stats.ReadStat(stat.Stat).Match(value => value.Value.Value.ToString(CultureInfo.InvariantCulture), _ => ""))];

    /// <summary>"Weapons 47 · Health ? · Class 104 · …": a stat line, "?" where it doesn't say.</summary>
    public static string DescribeStatLine(StatLine stats) =>
        string.Join(" · ", Stats.Select(stat => $"{stat.Name} {stats.ReadStat(stat.Stat).Match(value => value.Value.Value.ToString(CultureInfo.InvariantCulture), _ => "?")}"));

    /// <summary>"Weapons 5 · Health 30 · …": a copy's or a part's points (a part may be negative: "Grenade −10").</summary>
    public static string DescribePoints(StatPoints points) =>
        string.Join(" · ", Stats
            .Select(stat => (stat.Name, Value: points.ReadPoints(stat.Stat)))
            .Where(stat => stat.Value != 0)
            .Select(stat => $"{stat.Name} {(stat.Value < 0 ? "−" : "+")}{Math.Abs(stat.Value).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>A copy's points, every stat shown (its roll has 0 in three): "Weapons 0 · Health 30 · …".</summary>
    public static string DescribeRoll(StatPoints points) =>
        string.Join(" · ", Stats.Select(stat => $"{stat.Name} {points.ReadPoints(stat.Stat).ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>Where a copy is: "worn by your Hunter", "on your Hunter", "in the vault".</summary>
    public static string DescribePlace(ArmorPlace place) =>
        place.Match(
            worn => $"worn by your {worn.Class}",
            carried => $"on your {carried.Class}",
            _ => "in the vault");

    /// <summary>A slot as the Stats section names it: "Helmet", "Arms", "Chest", "Legs", "Class item".</summary>
    public static string DescribeSlot(ArmorSlot slot) =>
        slot switch
        {
            ArmorSlot.Helmet => "Helmet",
            ArmorSlot.Arms => "Arms",
            ArmorSlot.Chest => "Chest",
            ArmorSlot.Legs => "Legs",
            _ => "Class item",
        };
}
