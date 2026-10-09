using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;

namespace Loopsmith.Web.Presentation;

/// <summary>The first step a run could not perform, and in which cycle (<see cref="BlockedStep.StepIndex"/> is 0-based).</summary>
public sealed record CycleBlock(int Cycle, BlockedStep Step);

/// <summary>
/// Pure readings of a <see cref="LoopReport"/> for display. The arithmetic itself (repeatable, steady cycle, wasted,
/// uptime) is the core's <see cref="LoopReportArithmetic"/> and its wording the core's Phrasing; this only lays it out.
/// </summary>
public static class ReportReading
{
    /// <summary>"Repeatable · 10+ cycles", or "Repeats 3/10 cycles" when a step could not be played again.</summary>
    public static string DescribeRepeatability(LoopReport report) =>
        report.IsRepeatable()
            ? $"Repeatable · {report.MaxCycles}+ cycles"
            : $"Repeats {report.CompletedCycles}/{report.MaxCycles} cycles";

    public static Optional<CycleBlock> FindFirstBlock(LoopReport report) =>
        report.Cycles
            .Select(cycle => cycle.Blocked.Map(blocked => new CycleBlock(cycle.Number, blocked)))
            .FindFirstSome();

    /// <summary>The most fired sources (the report already orders them most fired first).</summary>
    public static ImmutableArray<SourceTally> ListTopSources(LoopReport report, int count) =>
        [.. report.Sources.Take(count)];

    public static decimal ToRatio(int part, int whole) =>
        whole <= 0 ? 0m : Math.Clamp((decimal)part / whole, 0m, 1m);
}
