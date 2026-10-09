using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

/// <summary>The energy the rules refunded to one ability in the steady cycle (explanation — energy isn't simulated).</summary>
public sealed record EnergyRefundView(AbilityKind Kind, RefundTally Refund);

/// <summary>The first step a run could not perform, and in which cycle (<see cref="BlockedStep.StepIndex"/> is 0-based).</summary>
public sealed record CycleBlock(int Cycle, BlockedStep Step);

/// <summary>
/// Pure readings of a <see cref="LoopReport"/> for display. The arithmetic itself (repeatable, steady cycle, refunds,
/// uptime) is the core's <see cref="LoopReportArithmetic"/>; this only lays it out.
/// </summary>
public static class ReportReading
{
    /// <summary>"10+" when the loop sustained every requested cycle, else the cycles it completed.</summary>
    public static string DescribeSustainedCycles(LoopReport report) =>
        report.IsRepeatable() ? $"{report.MaxCycles}+" : $"{report.CompletedCycles}";

    /// <summary>The core's energy refunded per cycle, one entry per ability.</summary>
    public static ImmutableArray<EnergyRefundView> ListRefunds(LoopReport report) =>
        [.. LoopReportArithmetic.AbilityOrder.Select(kind => new EnergyRefundView(kind, report.Refunds.ReadRefund(kind)))];

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
