using System.Collections.Immutable;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.RepoFileChecking;

/// <summary>
/// Every e2e fixture (<c>*.build.yaml</c>, <c>*.loop.yaml</c> under <c>tests/Loopsmith.Web.E2E/Scenarios</c> and
/// <c>Rules</c>) is named by a scenario class with its whole file name in double quotes
/// (<c>ImportBuildAsync("arc-staff.build.yaml")</c>): <c>tools/e2e/record.sh --changed-since</c> finds the classes that use
/// a changed fixture by that quoted name, so a fixture named any other way (built from pieces) would never re-record them.
/// </summary>
public sealed class FixtureNamingTests
{
    public static TheoryData<string> Fixtures => [.. ListFixtures()];   // impure

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_fixture_is_named_whole_and_quoted_by_a_scenario_class(string path)
    {
        var quoted = $"\"{Path.GetFileName(path)}\"";                                                      // pure
        var classes = ListScenarioClasses();                                                              // impure
        var texts = classes.Select(FileSourceFetching.ReadTextFile).ToImmutableArray();                   // impure

        var naming = texts.Count(text => text.Match(ok => ok.Value.Text.Contains(quoted, StringComparison.Ordinal), _ => false));

        Assert.True(naming > 0, $"{path}: no *Tests.cs names it as {quoted}, so tools/e2e/record.sh --changed-since can't find the scenarios that use it");
    }

    [Fact]
    public void Every_fixture_is_found()
    {
        var fixtures = ListFixtures();   // impure

        Assert.Contains(fixtures, path => path.EndsWith("/arc-staff.build.yaml", StringComparison.Ordinal));
    }

    private static ImmutableArray<string> ListScenarioRoots() =>
        [RepoFiles.ToPath("tests/Loopsmith.Web.E2E/Scenarios"), RepoFiles.ToPath("tests/Loopsmith.Web.E2E/Rules")];   // pure

    /// <summary>The scenario classes, as full paths.</summary>
    private static ImmutableArray<string> ListScenarioClasses()
    {
        var roots = ListScenarioRoots();                                                                                  // pure
        var classes = roots.SelectMany(root => Directory.EnumerateFiles(root, "*Tests.cs", SearchOption.AllDirectories)).ToImmutableArray();  // impure

        return classes;
    }

    /// <summary>The build and loop fixture files, relative to the repository's root and sorted.</summary>
    private static ImmutableArray<string> ListFixtures()
    {
        var roots = ListScenarioRoots();                                                                                  // pure
        var files = roots.SelectMany(root => Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)).ToImmutableArray();  // impure

        return [.. files                                                                                                  // pure
            .Where(file => file.EndsWith(".build.yaml", StringComparison.Ordinal) || file.EndsWith(".loop.yaml", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
    }
}
