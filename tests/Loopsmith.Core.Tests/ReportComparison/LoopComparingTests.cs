using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using Loopsmith.Core.ReportComparison;

namespace Loopsmith.Core.Tests.ReportComparison;

/// <summary>The rows and advantage rules of docs/loop-format.md "Comparison", on hand-built reports.</summary>
public sealed class LoopComparingTests
{
    private static EnergySnapshot ToEnergy(decimal grenade, decimal melee = 1m, decimal classAbility = 1m, decimal super = 0m) =>
        new(EnergyAmount.From(grenade), EnergyAmount.From(melee), EnergyAmount.From(classAbility), EnergyAmount.From(super));

    private static readonly EnergySnapshot Spawn = ToEnergy(1m);

    /// <summary>A report whose cycles end with <paramref name="ends"/>; when fewer than all of them completed, the last one is blocked.</summary>
    private static LoopReport CreateReport(
        string name,
        int steps = 4,
        int completed = 10,
        int maxCycles = 10,
        ImmutableArray<EnergySnapshot> ends = default,
        ImmutableArray<OutcomeTally> outcomes = default,
        ImmutableArray<BuffUptime> uptime = default,
        int unknown = 0,
        int chance = 0)
    {
        var cycleEnds = ends.IsDefault ? [.. Enumerable.Repeat(Spawn, completed)] : ends;
        var cycles = cycleEnds
            .Select((end, index) => new CycleRun(
                index + 1,
                [],
                index < completed ? Optional.None<BlockedStep>() : Optional.Some(new BlockedStep(0, new PlayerAction.UseClassAbility(), "blocked")),
                end))
            .ToImmutableArray();
        return new LoopReport(name, $"{name} build", steps, Spawn, cycles, completed, maxCycles, [],
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
                "Steps per cycle", "Sustained cycles",
                "Net grenade energy per cycle", "Net melee energy per cycle", "Net class ability energy per cycle", "Net super energy per cycle",
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
    public void More_sustained_cycles_win_and_a_sustainable_loop_reads_max_plus()
    {
        var row = FindRow(Compare(CreateReport("L"), CreateReport("R", completed: 3, ends: [Spawn, Spawn, Spawn, Spawn])), "Sustained cycles");

        Assert.Equal(new ComparisonRow("Sustained cycles", "10+", "3", Advantage.Left), row);
    }

    [Fact]
    public void On_equal_cycles_a_sustainable_loop_beats_one_that_broke()
    {
        var longerRun = CreateReport("R", completed: 10, maxCycles: 20, ends: [.. Enumerable.Repeat(Spawn, 11)]);

        var row = FindRow(Compare(CreateReport("L"), longerRun), "Sustained cycles");

        Assert.Equal(new ComparisonRow("Sustained cycles", "10+", "10", Advantage.Left), row);
    }

    [Fact]
    public void Higher_net_energy_wins_and_reads_signed()
    {
        var left = CreateReport("L", completed: 2, ends: [ToEnergy(1m), ToEnergy(0.75m)]);           // steady cycle 2: 0.75 − 1
        var right = CreateReport("R", completed: 1, ends: [ToEnergy(1.5m, melee: 0.5m), ToEnergy(1.5m)]);   // steady cycle 1 vs spawn

        var comparison = Compare(left, right);

        Assert.Equal(new ComparisonRow("Net grenade energy per cycle", "-0.25", "+0.5", Advantage.Right), FindRow(comparison, "Net grenade energy per cycle"));
        Assert.Equal(new ComparisonRow("Net melee energy per cycle", "0", "-0.5", Advantage.Left), FindRow(comparison, "Net melee energy per cycle"));
        Assert.Equal(Advantage.None, FindRow(comparison, "Net super energy per cycle").Better);
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
        var empty = new LoopReport("Empty", "B", 0, Spawn, [], 0, 10, [], [new OutcomeTally("Kills", 0)], [], 0, 0);

        var comparison = Compare(CreateReport("L", uptime: [ToUptime("amplified", 2, 4)]), empty);

        Assert.Equal(new ComparisonRow("Sustained cycles", "10+", "0", Advantage.Left), FindRow(comparison, "Sustained cycles"));
        Assert.Equal(new ComparisonRow("Amplified uptime", "2/4", "—", Advantage.Left), FindRow(comparison, "Amplified uptime"));
    }

    [Fact]
    public void The_table_marks_the_better_side()
    {
        var comparison = Compare(
            CreateReport("Skip loop", outcomes: [new("Kills", 4)]),
            CreateReport("Melee loop", completed: 1, ends: [Spawn, Spawn], outcomes: [new("Kills", 6)]));

        var lines = ComparisonRendering.RenderComparison(comparison).Select(line => line.ToPlainText()).ToImmutableArray();

        Assert.Equal("Skip loop  vs  Melee loop", lines[0]);
        Assert.Matches(@"^  Sustained cycles\s+10\+ ✓\s+1$", Assert.Single(lines, line => line.Contains("Sustained cycles")));
        Assert.Matches(@"^  Kills per cycle\s+4\s+6 ✓$", Assert.Single(lines, line => line.Contains("Kills per cycle")));
        Assert.Contains("Skip loop on 1 metric · Melee loop on 1 metric", lines[^1]);
    }
}
