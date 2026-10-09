using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>A step that could not be performed (nothing to pick up, no weapon in that slot).</summary>
public sealed record BlockedStep(int StepIndex, PlayerAction Action, string Reason);

/// <summary>One pass through the designed steps; cycle N starts from the state cycle N-1 ended in.</summary>
public sealed record CycleRun(int Number, ImmutableArray<Resolution> Resolutions, Optional<BlockedStep> Blocked);

public sealed record SourceTally(ElementId Source, string SourceName, Affinity Affinity, int Fired);

/// <summary>
/// How often in one cycle an element's rule gave nothing because it doesn't stack with <see cref="PartnerName"/>'s
/// (<see cref="Rule.DoesNotStackWith"/>): potential the build wastes.
/// </summary>
public sealed record WastedTally(ElementId Source, string SourceName, Affinity Affinity, string PartnerName, int Count);

/// <summary>How often something happened in one cycle ("Orb of Power spawned", "Bolt Charge maxed", "Kills").</summary>
public sealed record OutcomeTally(string Label, int Count);

/// <summary>After how many of the cycle's steps a buff was active.</summary>
public sealed record BuffUptime(StatusId Status, string Name, Affinity Affinity, int StepsActive, int Steps);

/// <summary>
/// What a designed loop does when run back to back: how many cycles repeat, what fired, what was wasted (rules that
/// don't stack), what's unknown. The steady-state figures (<see cref="Sources"/> onwards) describe one cycle.
/// Ability energy isn't simulated or added up (ADRs D21).
/// </summary>
public sealed record LoopReport(
    string LoopName,
    string BuildName,
    int StepCount,
    ImmutableArray<CycleRun> Cycles,
    int CompletedCycles,
    int MaxCycles,
    ImmutableArray<SourceTally> Sources,
    ImmutableArray<WastedTally> Wasted,
    ImmutableArray<OutcomeTally> Outcomes,
    ImmutableArray<BuffUptime> Uptime,
    int UnknownValues,
    int ChanceRules);

public enum Advantage { None, Left, Right }

public sealed record ComparisonRow(string Metric, string Left, string Right, Advantage Better);

/// <summary>Two loops side by side (they may use different builds).</summary>
public sealed record LoopComparison(LoopReport Left, LoopReport Right, ImmutableArray<ComparisonRow> Rows);
