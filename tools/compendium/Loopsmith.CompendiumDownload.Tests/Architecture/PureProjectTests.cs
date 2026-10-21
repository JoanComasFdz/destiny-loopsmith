using System.Text.RegularExpressions;
using Loopsmith.CompendiumDownload.Core;
using NetArchTest.Rules;

namespace Loopsmith.CompendiumDownload.Tests.Architecture;

/// <summary>
/// docs/CODING_RULES.md: the Compendium download's logic is a pure project. It touches no files, processes, network or
/// console (its host does), and it never uses Loopsmith.Core: a development tool needs only Loopsmith.Functional.
/// </summary>
public sealed class PureProjectTests
{
    [Fact]
    public void The_pure_project_uses_no_IO_processes_network_or_console() =>
        AssertNone(
            Types.InAssembly(typeof(GridReading).Assembly).ShouldNot().HaveDependencyOnAny("System.IO", "System.Diagnostics.Process", "System.Net", "System.Console").GetResult(),
            "Loopsmith.CompendiumDownload.Core must not use System.IO, System.Diagnostics.Process, System.Net or System.Console");

    [Fact]
    public void The_pure_project_does_not_reference_Core() =>
        Assert.DoesNotContain("Loopsmith.Core", typeof(GridReading).Assembly.GetReferencedAssemblies().Select(reference => reference.Name));

    [Fact]
    public void The_host_references_only_Functional_and_the_pure_project()
    {
        var project = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Loopsmith.CompendiumDownload", "Loopsmith.CompendiumDownload.csproj"));   // impure

        var references = Regex.Matches(project, "ProjectReference Include=\"([^\"]+)\"").Select(found => Path.GetFileName(found.Groups[1].Value.Replace('\\', '/')));   // pure

        Assert.Equal(["Loopsmith.Functional.csproj", "Loopsmith.CompendiumDownload.Core.csproj"], references);
    }

    private static void AssertNone(NetArchTest.Rules.TestResult result, string rule) =>
        Assert.True(result.IsSuccessful, $"{rule}: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
