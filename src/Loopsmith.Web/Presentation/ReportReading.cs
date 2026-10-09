using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Web.Presentation;

public sealed record EnergyChange(AbilityKind Kind, decimal Charges);

/// <summary>The first step a run could not perform, and in which cycle.</summary>
public sealed record CycleBlock(int Cycle, BlockedStep Step);

/// <summary>
/// Pure readings of a <see cref="LoopReport"/> for display. Net energy per cycle follows docs/loop-format.md
/// (the report carries the per-cycle energy, not the difference).
/// </summary>
public static class ReportReading
{
    public static bool IsSustainable(LoopReport report) =>
        report.MaxCycles > 0 && report.CompletedCycles >= report.MaxCycles;

    /// <summary>Energy at the end of the last completed cycle minus the cycle before it (or the spawn energy).</summary>
    public static Optional<ImmutableArray<EnergyChange>> ReadNetEnergyPerCycle(LoopReport report)
    {
        if (report.CompletedCycles <= 0 || report.Cycles.Length < report.CompletedCycles)
        {
            return Optional.None<ImmutableArray<EnergyChange>>();
        }

        var last = report.Cycles[report.CompletedCycles - 1].EnergyAtEnd;
        var before = report.CompletedCycles >= 2 ? report.Cycles[report.CompletedCycles - 2].EnergyAtEnd : report.EnergyAtStart;
        return Optional.Some<ImmutableArray<EnergyChange>>(
        [
            new EnergyChange(AbilityKind.Grenade, last.Grenade.Value - before.Grenade.Value),
            new EnergyChange(AbilityKind.Melee, last.Melee.Value - before.Melee.Value),
            new EnergyChange(AbilityKind.ClassAbility, last.ClassAbility.Value - before.ClassAbility.Value),
            new EnergyChange(AbilityKind.Super, last.Super.Value - before.Super.Value),
        ]);
    }

    public static Optional<CycleBlock> FindFirstBlock(LoopReport report) =>
        report.Cycles
            .Select(cycle => cycle.Blocked.Map(blocked => new CycleBlock(cycle.Number, blocked)))
            .FindFirstSome();

    public static ImmutableArray<SourceTally> ListTopSources(LoopReport report, int count) =>
        [.. report.Sources.OrderByDescending(source => source.Fired).ThenBy(source => source.SourceName, StringComparer.Ordinal).Take(count)];

    public static decimal ToRatio(int part, int whole) =>
        whole <= 0 ? 0m : Math.Clamp((decimal)part / whole, 0m, 1m);
}
