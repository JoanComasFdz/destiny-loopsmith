using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>
/// Pure value arithmetic over a <see cref="LoopReport"/>, defined once for every view of it
/// (docs/loop-format.md, "Analysis").
/// </summary>
public static class LoopReportArithmetic
{
    /// <summary>The abilities in the order every view lists them.</summary>
    public static readonly ImmutableArray<AbilityKind> AbilityOrder =
        [AbilityKind.Grenade, AbilityKind.Melee, AbilityKind.ClassAbility, AbilityKind.Super];

    /// <summary>
    /// Every requested cycle completed back to back. Ability energy isn't simulated (ADRs D21), so a cycle only breaks
    /// on a step that can't happen at all (nothing to pick up, no weapon in that slot).
    /// </summary>
    public static bool IsRepeatable(this LoopReport report) =>
        report.CompletedCycles > 0 && report.CompletedCycles == report.MaxCycles;

    /// <summary>The steady state: the last completed cycle, cycle 1 when none completed, none for an empty loop.</summary>
    public static Optional<CycleRun> FindSteadyCycle(this LoopReport report) =>
        report.Cycles.IsEmpty
            ? Optional.None<CycleRun>()
            : Optional.Some(report.Cycles[Math.Max(report.CompletedCycles, 1) - 1]);

    public static RefundTally ReadRefund(this EnergyRefunds refunds, AbilityKind ability) =>
        ability switch
        {
            AbilityKind.Grenade => refunds.Grenade,
            AbilityKind.Melee => refunds.Melee,
            AbilityKind.ClassAbility => refunds.ClassAbility,
            _ => refunds.Super,
        };

    /// <summary>
    /// The energy the <paramref name="refunds"/> granted, per ability: known and approximate amounts summed, unknown
    /// ones counted. No refund at all is <c>0</c> with nothing unknown.
    /// </summary>
    public static EnergyRefunds SumRefunds(IEnumerable<EnergyRefund> refunds)
    {
        var all = refunds.ToImmutableArray();
        RefundTally SumFor(AbilityKind ability)
        {
            var mine = all.Where(refund => refund.Ability == ability).ToImmutableArray();
            var known = mine.SelectMany(refund => refund.Amount.Value.Match(some => new[] { some.Value }, _ => [])).Sum();
            var approximate = mine.Any(refund => refund.Amount.Certainty == Certainty.Approximate && refund.Amount.Value.IsSome());
            return new RefundTally(known, approximate, mine.Count(refund => !refund.Amount.Value.IsSome()));
        }

        return new EnergyRefunds(SumFor(AbilityKind.Grenade), SumFor(AbilityKind.Melee), SumFor(AbilityKind.ClassAbility), SumFor(AbilityKind.Super));
    }

    /// <summary>The fraction of the cycle's steps a buff was active after (0 for a loop without steps).</summary>
    public static decimal ComputeUptimeRatio(this BuffUptime uptime) =>
        uptime.Steps == 0 ? 0m : (decimal)uptime.StepsActive / uptime.Steps;
}
