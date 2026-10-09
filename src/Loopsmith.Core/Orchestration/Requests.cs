using System.Collections.Immutable;
using Loopsmith.Core.BuildExplanation;
using Loopsmith.Core.Functional;
using Loopsmith.Core.TraceRendering;

namespace Loopsmith.Core.Orchestration;

/// <summary>Where to load from: the build file and (optionally) an explicit rules directory.</summary>
public sealed record LoadRequest(string BuildPath, Optional<string> RulesDirectory);

public sealed record ExplainRequest(LoadRequest Load, ExplanationStyle Style);

public sealed record SimulateRequest(LoadRequest Load, ImmutableArray<string> ActionTokens, Optional<string> ScenarioPath, TraceOptions Options);

public enum GraphFormat { Loops, Mermaid }

public sealed record GraphRequest(LoadRequest Load, GraphFormat Format, bool LoopsOnly, int Limit);

/// <summary>Analyse one loop file: run it up to <see cref="MaxCycles"/> back to back; <see cref="Trace"/> adds the full trace of cycle 1.</summary>
public sealed record LoopRequest(string LoopPath, Optional<string> RulesDirectory, int MaxCycles, Optional<TraceOptions> Trace);

/// <summary>Two loop files side by side (the rules are located from the first one).</summary>
public sealed record CompareRequest(string LeftPath, string RightPath, Optional<string> RulesDirectory, int MaxCycles);

/// <summary>Interactive design of a loop: <see cref="SavePath"/> receives the loop file on quit when it has steps, and on <c>w</c>.</summary>
public sealed record PlayRequest(LoadRequest Load, TraceOptions Options, Optional<string> SavePath, Optional<string> LoopName);
