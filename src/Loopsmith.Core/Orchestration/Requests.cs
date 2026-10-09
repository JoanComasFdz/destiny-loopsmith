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
