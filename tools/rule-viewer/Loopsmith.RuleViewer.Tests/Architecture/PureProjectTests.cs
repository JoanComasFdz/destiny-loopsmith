using Loopsmith.Core.Domain;
using Loopsmith.RuleViewer.Core;
using NetArchTest.Rules;

namespace Loopsmith.RuleViewer.Tests.Architecture;

/// <summary>
/// ADRs D39: the rule viewer's logic is a pure project. It touches no files, network or console (its host does), and it
/// uses of Core only the kernel and Orchestration, as a host does (Core's HostBoundaryTests check its calls too).
/// </summary>
public sealed class PureProjectTests
{
    private static readonly string[] CoreAllowed =
        ["Loopsmith.Core.Domain", "Loopsmith.Core.Functional", "Loopsmith.Core.Phrasing", "Loopsmith.Core.Causality", "Loopsmith.Core.Orchestration"];

    /// <summary>Every other top-level namespace of Core: its feature slices and SourceFetching.</summary>
    private static readonly string[] CoreForbidden = [.. typeof(RuleCatalog).Assembly.GetTypes()
        .Select(type => string.Join('.', (type.Namespace ?? "").Split('.').Take(3)))
        .Where(name => name.StartsWith("Loopsmith.Core.", StringComparison.Ordinal) && !CoreAllowed.Contains(name))
        .Distinct()];

    [Fact]
    public void The_pure_project_uses_no_IO_network_or_console() =>
        AssertNone(Types.InAssembly(typeof(RuleViewing).Assembly).ShouldNot().HaveDependencyOnAny("System.IO", "System.Net", "System.Console").GetResult(),
            "Loopsmith.RuleViewer.Core must not use System.IO, System.Net or System.Console");

    [Fact]
    public void The_pure_project_uses_only_Cores_kernel_and_Orchestration() =>
        AssertNone(Types.InAssembly(typeof(RuleViewing).Assembly).ShouldNot().HaveDependencyOnAny(CoreForbidden).GetResult(),
            $"Loopsmith.RuleViewer.Core may use only {string.Join(", ", CoreAllowed)} of Core");

    private static void AssertNone(NetArchTest.Rules.TestResult result, string rule) =>
        Assert.True(result.IsSuccessful, $"{rule}: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
