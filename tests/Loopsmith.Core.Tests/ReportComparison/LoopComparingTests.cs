using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.ReportComparison;

namespace Loopsmith.Core.Tests.ReportComparison;

/// <summary>The rows and advantage rules of docs/loop-format.md "Comparison", on hand-built reports.</summary>
public sealed class LoopComparingTests
{
    private static readonly RefundTally Nothing = new(0m, false, 0);

    private static readonly EnergyRefunds NoRefunds = new(Nothing, Nothing, Nothing, Nothing);

    /// <summary>A report that ran <paramref name="cycles"/> cycles (all completed by default); the ones after <paramref name="completed"/> are blocked.</summary>
    private static LoopReport CreateReport(
        string name,
        int steps = 4,
        int completed = 10,
        int maxCycles = 10,
        int? cycles = null,
        EnergyRefunds? refunds = null,
        ImmutableArray<OutcomeTally> outcomes = default,
        ImmutableArray<BuffUptime> uptime = default,
        int unknown = 0,
        int chance = 0)
    {
        var runs = Enumerable.Range(0, cycles ?? completed)
            .Select(index => new CycleRun(
                index + 1,
                [],
                index < completed ? Optional.None<BlockedStep>() : Optional.Some(new BlockedStep(0, new PlayerAction.CollectPickups(PickupId.From("orb-of-power")), "blocked"))))
            .ToImmutableArray();
        return new LoopReport(name, $"{name} build", steps, runs, completed, maxCycles, refunds ?? NoRefunds, [],
            outcomes.IsDefault ? [new OutcomeTally("Kills", 0)] : outcomes, uptime.IsDefault ? [] : uptime, unknown, chance);
    }

    private static BuffUptime ToUptime(string status, int active, int steps) =>
        new(StatusId.From(status), string.Join(' ', status.Split('-').Select(DomainPhrasing.Capitalize)), Affinity.Arc, active, steps);

    private static ComparisonRow FindRow(LoopComparison comparison, string metric) =>
        Assert.Single(comparison.Rows, row => row.Metric == metric);

    private static LoopComparison Compare(LoopReport left, LoopReport right) => LoopComparing.CompareLoops(left, right);

    [Fact]
    public void Rows_follow_the_spec_order_with_the_union_of_both_reports()
    {
        var left = CreateReport("Left",
            outcomes: [new("Kills", 5), new("Orb of Power spawned", 2), new("Bolt Charge maxed", 1)],
            uptime: [ToUptime("amplified", 4, 4)]);
        var right = CreateReport("Right",
            outcomes: [new("Kills", 3), new("Ionic Trace spawned", 3)],
            uptime: [ToUptime("bolt-charge", 2, 4), ToUptime("amplified", 1, 4)]);

        var comparison = Compare(left, right);

        Assert.Same(left, comparison.Left);
        Assert.Equal(
            [
                "Steps per cycle", "Repeatable cycles",
                "Grenade energy refunded per cycle", "Melee energy refunded per cycle",
                "Class ability energy refunded per cycle", "Super energy refunded per cycle",
                "Kills per cycle", "Orb of Power spawned per cycle", "Ionic Trace spawned per cycle", "Bolt Charge maxed per cycle",
                "Amplified uptime", "Bolt Charge uptime",
                "Unknown values", "Chance bullets",
            ],
            comparison.Rows.Select(row => row.Metric));
    }

    [Fact]
    public void What_one_report_lacks_counts_as_zero()
    {
        var left = CreateReport("Left", steps: 4, outcomes: [new("Kills", 5), new("Orb of Power spawned", 2)], uptime: [ToUptime("amplified", 3, 4)]);
        var right = CreateReport("Right", steps: 2, outcomes: [new("Kills", 5), new("Ionic Trace spawned", 3)]);

        var comparison = Compare(left, right);

        Assert.Equal(new ComparisonRow("Orb of Power spawned per cycle", "2", "0", Advantage.Left), FindRow(comparison, "Orb of Power spawned per cycle"));
        Assert.Equal(new ComparisonRow("Ionic Trace spawned per cycle", "0", "3", Advantage.Right), FindRow(comparison, "Ionic Trace spawned per cycle"));
        Assert.Equal(new ComparisonRow("Kills per cycle", "5", "5", Advantage.None), FindRow(comparison, "Kills per cycle"));
        Assert.Equal(new ComparisonRow("Amplified uptime", "3/4", "0/2", Advantage.Left), FindRow(comparison, "Amplified uptime"));
    }

    [Fact]
    public void Steps_per_cycle_are_shown_but_not_judged() =>
        Assert.Equal(
            new ComparisonRow("Steps per cycle", "3", "9", Advantage.None),
            FindRow(Compare(CreateReport("L", steps: 3), CreateReport("R", steps: 9)), "Steps per cycle"));

    [Fact]
    public void More_repeatable_cycles_win_and_a_repeatable_loop_reads_max_plus()
    {
        var row = FindRow(Compare(CreateReport("L"), CreateReport("R", completed: 3, cycles: 4)), "Repeatable cycles");

        Assert.Equal(new ComparisonRow("Repeatable cycles", "10+", "3", Advantage.Left), row);
    }

    [Fact]
    public void On_equal_cycles_a_repeatable_loop_beats_one_that_broke()
    {
        var longerRun = CreateReport("R", completed: 10, maxCycles: 20, cycles: 11);

        var row = FindRow(Compare(CreateReport("L"), longerRun), "Repeatable cycles");

        Assert.Equal(new ComparisonRow("Repeatable cycles", "10+", "10", Advantage.Left), row);
    }

    [Fact]
    public void More_energy_refunded_wins_then_more_refunds_of_unknown_size()
    {
        var left = CreateReport("L", refunds: new EnergyRefunds(new(0.46m, false, 3), new(0.15m, true, 0), Nothing, new(0m, false, 2)));
        var right = CreateReport("R", refunds: new EnergyRefunds(new(1.2m, false, 0), new(0.15m, true, 1), Nothing, new(0m, false, 5)));

        var comparison = Compare(left, right);

        Assert.Equal(
            new ComparisonRow("Grenade energy refunded per cycle", "+46% (+3 unknown)", "+120%", Advantage.Right),
            FindRow(comparison, "Grenade energy refunded per cycle"));
        Assert.Equal(
            new ComparisonRow("Melee energy refunded per cycle", "+~15%", "+~15% (+1 unknown)", Advantage.Right),
            FindRow(comparison, "Melee energy refunded per cycle"));
        Assert.Equal(
            new ComparisonRow("Class ability energy refunded per cycle", "0%", "0%", Advantage.None),
            FindRow(comparison, "Class ability energy refunded per cycle"));
        Assert.Equal(
            new ComparisonRow("Super energy refunded per cycle", "0% (+2 unknown)", "0% (+5 unknown)", Advantage.Right),
            FindRow(comparison, "Super energy refunded per cycle"));
    }

    [Fact]
    public void Uptime_is_judged_as_a_fraction_of_the_cycle()
    {
        var comparison = Compare(
            CreateReport("L", steps: 6, uptime: [ToUptime("amplified", 3, 6)]),
            CreateReport("R", steps: 3, uptime: [ToUptime("amplified", 2, 3)]));

        Assert.Equal(new ComparisonRow("Amplified uptime", "3/6", "2/3", Advantage.Right), FindRow(comparison, "Amplified uptime"));
    }

    [Fact]
    public void Fewer_unknown_values_and_chance_bullets_win()
    {
        var comparison = Compare(CreateReport("L", unknown: 3, chance: 1), CreateReport("R", unknown: 5, chance: 1));

        Assert.Equal(Advantage.Left, FindRow(comparison, "Unknown values").Better);
        Assert.Equal(Advantage.None, FindRow(comparison, "Chance bullets").Better);
    }

    [Fact]
    public void Comparing_with_an_empty_loop_does_not_crash()
    {
        var empty = new LoopReport("Empty", "B", 0, [], 0, 10, NoRefunds, [], [new OutcomeTally("Kills", 0)], [], 0, 0);

        var comparison = Compare(CreateReport("L", uptime: [ToUptime("amplified", 2, 4)]), empty);

        Assert.Equal(new ComparisonRow("Repeatable cycles", "10+", "0", Advantage.Left), FindRow(comparison, "Repeatable cycles"));
        Assert.Equal(new ComparisonRow("Amplified uptime", "2/4", "—", Advantage.Left), FindRow(comparison, "Amplified uptime"));
    }

    [Fact]
    public void The_table_marks_the_better_side()
    {
        var comparison = Compare(
            CreateReport("Skip loop", outcomes: [new("Kills", 4)]),
            CreateReport("Melee loop", completed: 1, cycles: 2, outcomes: [new("Kills", 6)]));

        var lines = ComparisonRendering.RenderComparison(comparison).Select(line => line.ToPlainText()).ToImmutableArray();

        Assert.Equal("Skip loop  vs  Melee loop", lines[0]);
        Assert.Matches(@"^  Repeatable cycles\s+10\+ ✓\s+1$", Assert.Single(lines, line => line.Contains("Repeatable cycles")));
        Assert.Matches(@"^  Kills per cycle\s+4\s+6 ✓$", Assert.Single(lines, line => line.Contains("Kills per cycle")));
        Assert.Contains("Skip loop on 1 metric · Melee loop on 1 metric", lines[^1]);
    }
}
