using Loopsmith.E2ETools.Core;
using NetArchTest.Rules;

namespace Loopsmith.E2ETools.Tests.Architecture;

/// <summary>
/// docs/CODING_RULES.md: the e2e tools' logic is a pure project. It touches no files, processes, network or console (its
/// host does), and it never uses Loopsmith.Core: a development tool needs only Loopsmith.Functional, and CI builds it in seconds.
/// </summary>
public sealed class PureProjectTests
{
    [Fact]
    public void The_pure_project_uses_no_IO_processes_network_or_console() =>
        AssertNone(
            Types.InAssembly(typeof(PostWriting).Assembly).ShouldNot().HaveDependencyOnAny("System.IO", "System.Diagnostics.Process", "System.Net", "System.Console").GetResult(),
            "Loopsmith.E2ETools.Core must not use System.IO, System.Diagnostics.Process, System.Net or System.Console");

    [Fact]
    public void Neither_project_references_Core()
    {
        string[] projects = [typeof(PostWriting).Assembly.GetName().Name!, typeof(E2EToolsOptions).Assembly.GetName().Name!];

        var references = new[] { typeof(PostWriting).Assembly, typeof(E2EToolsOptions).Assembly }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name);

        Assert.DoesNotContain("Loopsmith.Core", references);
        Assert.Equal(["Loopsmith.E2ETools.Core", "Loopsmith.E2ETools"], projects);
    }

    private static void AssertNone(NetArchTest.Rules.TestResult result, string rule) =>
        Assert.True(result.IsSuccessful, $"{rule}: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
