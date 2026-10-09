using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A step that could not be performed (nothing to pick up, no weapon in that slot).</summary>
public sealed record BlockedStep(int StepIndex, PlayerAction Action, string Reason);

/// <summary>One pass through the designed steps; cycle N starts from the state cycle N-1 ended in.</summary>
public sealed record CycleRun(int Number, ImmutableArray<Resolution> Resolutions, Optional<BlockedStep> Blocked);

/// <summary>
/// Energy the rules refunded to one ability in one cycle (explanation — ability energy isn't simulated, ADRs D21):
/// the sum of the known and approximate amounts as a fraction of one charge (1.46 = 146 %), whether any of them was
/// approximate, and how many refunds had an unknown ("?") amount.
/// </summary>
public sealed record RefundTally(decimal Amount, bool IsApproximate, int UnknownCount);

/// <summary>Exactly one refund tally per ability — a missing or duplicate ability is not representable.</summary>
public sealed record EnergyRefunds(RefundTally Grenade, RefundTally Melee, RefundTally ClassAbility, RefundTally Super);

public sealed record SourceTally(ElementId Source, string SourceName, Affinity Affinity, int Fired);

/// <summary>How often something happened in one cycle ("Orb of Power spawned", "Bolt Charge maxed", "Kills").</summary>
public sealed record OutcomeTally(string Label, int Count);

/// <summary>After how many of the cycle's steps a buff was active.</summary>
public sealed record BuffUptime(StatusId Status, string Name, Affinity Affinity, int StepsActive, int Steps);

/// <summary>
/// What a designed loop does when run back to back: how many cycles repeat, the energy its rules refund, what fired,
/// what's unknown. The steady-state figures (<see cref="Refunds"/> onwards) describe one cycle.
/// </summary>
public sealed record LoopReport(
    string LoopName,
    string BuildName,
    int StepCount,
    ImmutableArray<CycleRun> Cycles,
    int CompletedCycles,
    int MaxCycles,
    EnergyRefunds Refunds,
    ImmutableArray<SourceTally> Sources,
    ImmutableArray<OutcomeTally> Outcomes,
    ImmutableArray<BuffUptime> Uptime,
    int UnknownValues,
    int ChanceRules);

public enum Advantage { None, Left, Right }

public sealed record ComparisonRow(string Metric, string Left, string Right, Advantage Better);

/// <summary>Two loops side by side (they may use different builds).</summary>
public sealed record LoopComparison(LoopReport Left, LoopReport Right, ImmutableArray<ComparisonRow> Rows);
