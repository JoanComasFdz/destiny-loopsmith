using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// Pure value arithmetic over a <see cref="LoopReport"/>, defined once for every view of it
/// (docs/loop-format.md, "Analysis").
/// </summary>
public static class LoopReportArithmetic
{
    /// <summary>
    /// Every requested cycle completed back to back. Ability energy isn't simulated (ADRs D5), so a cycle only breaks
    /// on a step that can't happen at all (nothing to pick up, no weapon in that slot).
    /// </summary>
    public static bool IsRepeatable(this LoopReport report) =>
        report.CompletedCycles > 0 && report.CompletedCycles == report.MaxCycles;

    /// <summary>The steady state: the last completed cycle, cycle 1 when none completed, none for an empty loop.</summary>
    public static Optional<CycleRun> FindSteadyCycle(this LoopReport report) =>
        report.Cycles.IsEmpty
            ? Optional.None<CycleRun>()
            : Optional.Some(report.Cycles[Math.Max(report.CompletedCycles, 1) - 1]);

    /// <summary>Every time a rule gave nothing in one cycle because it doesn't stack (0 when everything stacked).</summary>
    public static int CountWasted(this LoopReport report) =>
        report.Wasted.Sum(tally => tally.Count);

    /// <summary>The fraction of the cycle's steps a buff was active after (0 for a loop without steps).</summary>
    public static decimal ComputeUptimeRatio(this BuffUptime uptime) =>
        uptime.Steps == 0 ? 0m : (decimal)uptime.StepsActive / uptime.Steps;
}
