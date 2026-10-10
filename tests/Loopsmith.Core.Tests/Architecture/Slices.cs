using System.Collections.Immutable;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>
/// The slice map of <c>Loopsmith.Core</c> (CONVENTIONS.md "Architecture", ADRs D16).
/// Every rule iterates these lists, so a slice with no types yet passes trivially and starts being
/// checked the moment its first type appears. A new top-level namespace must be added here
/// (<see cref="SliceBoundaryTests.Every_core_type_lives_in_a_known_slice"/> fails until it is).
/// </summary>
internal static class Slices
{
    public const string Root = "Loopsmith.Core";

    public const string Domain = "Domain";
    public const string Functional = "Functional";
    public const string Phrasing = "Phrasing";
    public const string Causality = "Causality";

    public const string SourceFetching = "SourceFetching";

    public const string RuleParsing = "RuleParsing";
    public const string BuildParsing = "BuildParsing";
    public const string BuildComposition = "BuildComposition";
    public const string Simulation = "Simulation";
    public const string BuildExplanation = "BuildExplanation";
    public const string LoopGraphing = "LoopGraphing";
    public const string TraceRendering = "TraceRendering";
    public const string LoopFiles = "LoopFiles";
    public const string ReportComparison = "ReportComparison";
    public const string LoadoutImporting = "LoadoutImporting";

    public const string Orchestration = "Orchestration";

    /// <summary>Shared kernel: every slice may depend on it; it depends on no slice.</summary>
    public static readonly ImmutableArray<string> Kernel = [Domain, Functional, Phrasing, Causality];

    /// <summary>Pure feature slices.</summary>
    public static readonly ImmutableArray<string> Features =
        [RuleParsing, BuildParsing, BuildComposition, Simulation, BuildExplanation, LoopGraphing, TraceRendering, LoopFiles, ReportComparison,
            LoadoutImporting];

    /// <summary>Every known slice, kernel included.</summary>
    public static readonly ImmutableArray<string> All = [.. Kernel, SourceFetching, .. Features, Orchestration];

    /// <summary>Slices that must depend only on the kernel and themselves: the feature slices and the impure boundary.</summary>
    public static readonly ImmutableArray<string> KernelOnly = [SourceFetching, .. Features];

    /// <summary>The only slices allowed to reference YamlDotNet (LoopFiles both reads and writes loop files).</summary>
    public static readonly ImmutableArray<string> YamlParsers = [RuleParsing, BuildParsing, LoopFiles];

    /// <summary>
    /// The only slice that emits YAML — loop files and the build files they embed (a build from a DIM link) — so it may
    /// build the text in memory with a <c>StringWriter</c>.
    /// </summary>
    public static readonly ImmutableArray<string> YamlWriters = [LoopFiles];

    /// <summary>
    /// What each slice may depend on besides itself. Inside the kernel: Functional depends on nothing,
    /// Domain on Functional, Phrasing and Causality on Domain + Functional (not on each other).
    /// Orchestration may depend on every slice — the only exception.
    /// </summary>
    public static readonly ImmutableDictionary<string, ImmutableArray<string>> AllowedDependencies =
        ImmutableDictionary.CreateRange<string, ImmutableArray<string>>(
        [
            new(Functional, []),
            new(Domain, [Functional]),
            new(Phrasing, [Domain, Functional]),
            new(Causality, [Domain, Functional]),
            .. KernelOnly.Select(slice => new KeyValuePair<string, ImmutableArray<string>>(slice, Kernel)),
            new(Orchestration, All),
        ]);

    /// <summary>The slice a namespace belongs to: <c>Loopsmith.Core.Simulation.Phases</c> → <c>Simulation</c>.</summary>
    public static string? FindSlice(string @namespace)
    {
        if (!@namespace.StartsWith(Root + ".", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = @namespace[(Root.Length + 1)..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? rest : rest[..dot];
    }

    /// <summary>True for <c>Loopsmith.Core</c> itself and anything under it.</summary>
    public static bool IsCoreNamespace(string @namespace) =>
        @namespace == Root || @namespace.StartsWith(Root + ".", StringComparison.Ordinal);

    public static TheoryData<string> ToTheoryData(IEnumerable<string> slices) => [.. slices];
}
