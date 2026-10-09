using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Core.ReportComparison;

/// <summary>
/// Two loop reports side by side, one row per metric with the better side (docs/loop-format.md, "Comparison").
/// Pure. The loops may use different builds: pickups, statuses and buffs are the union of both reports.
/// </summary>
public static class LoopComparing
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private enum Better { Higher, Lower, Unjudged }

    public static LoopComparison CompareLoops(LoopReport left, LoopReport right)
    {
        ImmutableArray<ComparisonRow> rows =
        [
            CreateRow("Steps per cycle", left.StepCount, right.StepCount, Better.Unjudged),
            CompareRepeats(left, right),
            .. CompareOutcomes(left, right),
            CreateRow("Wasted per cycle (doesn't stack)", left.CountWasted(), right.CountWasted(), Better.Lower),
            .. CompareUptime(left, right),
            CreateRow("Unknown values", left.UnknownValues, right.UnknownValues, Better.Lower),
            CreateRow("Chance bullets", left.ChanceRules, right.ChanceRules, Better.Lower),
        ];
        return new LoopComparison(left, right, rows);
    }

    /// <summary>More completed cycles is better; on a tie a repeatable loop beats one that broke.</summary>
    private static ComparisonRow CompareRepeats(LoopReport left, LoopReport right)
    {
        var byCycles = JudgeAdvantage(left.CompletedCycles, right.CompletedCycles, Better.Higher);
        var byRepeat = JudgeAdvantage(left.IsRepeatable() ? 1 : 0, right.IsRepeatable() ? 1 : 0, Better.Higher);
        return new ComparisonRow(
            "Repeatable cycles",
            FormatRepeats(left),
            FormatRepeats(right),
            byCycles == Advantage.None ? byRepeat : byCycles);
    }

    /// <summary>"10+" when every requested cycle completed, else the cycles completed.</summary>
    public static string FormatRepeats(LoopReport report) =>
        report.IsRepeatable()
            ? $"{report.MaxCycles}+"
            : report.CompletedCycles.ToString(Invariant);

    /// <summary>Kills, then every pickup spawned, then every status maxed — union of both reports, missing counts as 0.</summary>
    private static IEnumerable<ComparisonRow> CompareOutcomes(LoopReport left, LoopReport right) =>
        ListUnion(left.Outcomes.Select(o => o.Label), right.Outcomes.Select(o => o.Label))
            .Select((label, order) => (Label: label, Order: order))
            .OrderBy(x => RankOutcome(x.Label))
            .ThenBy(x => x.Order)
            .Select(x => CreateRow(
                $"{x.Label} per cycle",
                CountOutcome(left, x.Label),
                CountOutcome(right, x.Label),
                Better.Higher));

    /// <summary>Outcome labels are <c>Kills</c>, <c>&lt;Pickup&gt; spawned</c> and <c>&lt;Status&gt; maxed</c> (docs/loop-format.md).</summary>
    private static int RankOutcome(string label) =>
        label switch
        {
            "Kills" => 0,
            _ when label.EndsWith(" spawned", StringComparison.Ordinal) => 1,
            _ when label.EndsWith(" maxed", StringComparison.Ordinal) => 2,
            _ => 3,
        };

    private static int CountOutcome(LoopReport report, string label) =>
        report.Outcomes.Where(o => o.Label == label).Sum(o => o.Count);

    /// <summary>Uptime is compared as the fraction of the cycle (the loops may have different step counts).</summary>
    private static IEnumerable<ComparisonRow> CompareUptime(LoopReport left, LoopReport right) =>
        ListUnion(left.Uptime.Select(u => u.Status), right.Uptime.Select(u => u.Status))
            .Select(status =>
            {
                var leftUptime = FindUptime(left, status);
                var rightUptime = FindUptime(right, status);
                var name = left.Uptime.Concat(right.Uptime).First(u => u.Status == status).Name;
                return new ComparisonRow(
                    $"{name} uptime",
                    FormatUptime(leftUptime),
                    FormatUptime(rightUptime),
                    JudgeAdvantage(leftUptime.ComputeUptimeRatio(), rightUptime.ComputeUptimeRatio(), Better.Higher));
            });

    private static BuffUptime FindUptime(LoopReport report, StatusId status) =>
        Optional.FromNullable(report.Uptime.FirstOrDefault(u => u.Status == status))
            .UnwrapOr(new BuffUptime(status, status.Value, Affinity.Neutral, 0, report.StepCount));

    /// <summary><c>5/7</c> steps; <c>—</c> for a loop without steps.</summary>
    public static string FormatUptime(BuffUptime uptime) =>
        uptime.Steps == 0 ? "—" : $"{uptime.StepsActive}/{uptime.Steps}";

    private static ComparisonRow CreateRow(string metric, int left, int right, Better better) =>
        new(metric, left.ToString(Invariant), right.ToString(Invariant), JudgeAdvantage(left, right, better));

    private static Advantage JudgeAdvantage(decimal left, decimal right, Better better) =>
        (better, left.CompareTo(right)) switch
        {
            (Better.Unjudged, _) => Advantage.None,
            (_, 0) => Advantage.None,
            (Better.Higher, > 0) or (Better.Lower, < 0) => Advantage.Left,
            _ => Advantage.Right,
        };

    private static ImmutableArray<T> ListUnion<T>(IEnumerable<T> left, IEnumerable<T> right) =>
        [.. left.Concat(right).Distinct()];
}
