using System.Collections.Immutable;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Domain;

/// <summary>Ability energy at one moment, in charges.</summary>
public sealed record EnergySnapshot(EnergyAmount Grenade, EnergyAmount Melee, EnergyAmount ClassAbility, EnergyAmount Super);

/// <summary>A step that could not be performed (not enough energy, nothing to pick up…).</summary>
public sealed record BlockedStep(int StepIndex, PlayerAction Action, string Reason);

/// <summary>One pass through the designed steps; cycle N starts from the state cycle N-1 ended in.</summary>
public sealed record CycleRun(int Number, ImmutableArray<Resolution> Resolutions, Optional<BlockedStep> Blocked, EnergySnapshot EnergyAtEnd);

public sealed record SourceTally(ElementId Source, string SourceName, Affinity Affinity, int Fired);

/// <summary>How often something happened in one cycle ("Orb of Power spawned", "Bolt Charge maxed", "Kills").</summary>
public sealed record OutcomeTally(string Label, int Count);

/// <summary>After how many of the cycle's steps a buff was active.</summary>
public sealed record BuffUptime(StatusId Status, string Name, Affinity Affinity, int StepsActive, int Steps);

/// <summary>What a designed loop does when run: cycles it sustains, energy, what fired, what's unknown.</summary>
public sealed record LoopReport(
    string LoopName,
    string BuildName,
    int StepCount,
    EnergySnapshot EnergyAtStart,
    ImmutableArray<CycleRun> Cycles,
    int CompletedCycles,
    int MaxCycles,
    ImmutableArray<SourceTally> Sources,
    ImmutableArray<OutcomeTally> Outcomes,
    ImmutableArray<BuffUptime> Uptime,
    int UnknownValues,
    int ChanceRules);

public enum Advantage { None, Left, Right }

public sealed record ComparisonRow(string Metric, string Left, string Right, Advantage Better);

/// <summary>Two loops side by side (they may use different builds).</summary>
public sealed record LoopComparison(LoopReport Left, LoopReport Right, ImmutableArray<ComparisonRow> Rows);
