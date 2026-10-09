using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A signed change of ability energy, in charges (an <see cref="EnergyAmount"/> is never negative).</summary>
public sealed record EnergyDelta(decimal Grenade, decimal Melee, decimal ClassAbility, decimal Super);

/// <summary>
/// Pure value arithmetic over a <see cref="LoopReport"/>, defined once for every view of it
/// (docs/loop-format.md, "Analysis").
/// </summary>
public static class LoopReportArithmetic
{
    /// <summary>Every requested cycle completed: the loop feeds itself.</summary>
    public static bool IsSustainable(this LoopReport report) =>
        report.CompletedCycles > 0 && report.CompletedCycles == report.MaxCycles;

    /// <summary>The steady state: the last completed cycle, cycle 1 when none completed, none for an empty loop.</summary>
    public static Optional<CycleRun> FindSteadyCycle(this LoopReport report) =>
        report.Cycles.IsEmpty
            ? Optional.None<CycleRun>()
            : Optional.Some(report.Cycles[Math.Max(report.CompletedCycles, 1) - 1]);

    /// <summary>
    /// Net energy per cycle at the steady state: energy at the end of the steady cycle minus energy at the end of
    /// the cycle before it (minus <see cref="LoopReport.EnergyAtStart"/> when it is cycle 1). Zero for an empty loop.
    /// </summary>
    public static EnergyDelta ComputeNetEnergy(this LoopReport report) =>
        report.FindSteadyCycle().Match(
            steady => SubtractEnergy(
                steady.Value.EnergyAtEnd,
                steady.Value.Number >= 2 ? report.Cycles[steady.Value.Number - 2].EnergyAtEnd : report.EnergyAtStart),
            _ => new EnergyDelta(0m, 0m, 0m, 0m));

    public static EnergyDelta SubtractEnergy(EnergySnapshot after, EnergySnapshot before) =>
        new(
            after.Grenade.Value - before.Grenade.Value,
            after.Melee.Value - before.Melee.Value,
            after.ClassAbility.Value - before.ClassAbility.Value,
            after.Super.Value - before.Super.Value);

    /// <summary>The fraction of the cycle's steps a buff was active after (0 for a loop without steps).</summary>
    public static decimal ComputeUptimeRatio(this BuffUptime uptime) =>
        uptime.Steps == 0 ? 0m : (decimal)uptime.StepsActive / uptime.Steps;
}
