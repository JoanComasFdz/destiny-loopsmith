using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.RepoFileChecking;

/// <summary>
/// Every build and loop file of the repository opens against its rules, as the designer's import opens it: the
/// builds (<c>builds/*/build.yaml</c>), their designed loops (<c>builds/*/loops/*.loop.yaml</c>) and the e2e
/// scenarios' fixtures (<c>*.build.yaml</c>, <c>*.loop.yaml</c> under <c>tests/Loopsmith.Web.E2E/Scenarios</c>).
/// A fixture meant to be refused is listed in <see cref="Refused"/> with the error it must give.
/// Run it alone: <c>dotnet test --filter RepoFileValidationTests</c>.
/// </summary>
public sealed class RepoFileValidationTests
{
    /// <summary>The fixtures an e2e scenario imports to see them refused, each with a part of the error it shows.</summary>
    private static readonly ImmutableDictionary<string, string> Refused = new Dictionary<string, string>
    {
        ["tests/Loopsmith.Web.E2E/Scenarios/ImportExport/crisis-inverted-without-archetype-or-hash.build.yaml"] = "weapon 'Crisis Inverted' needs an `archetype` or a `hash`",
        ["tests/Loopsmith.Web.E2E/Scenarios/ImportExport/unknown-weapon-hash.build.yaml"] = "1234567",
    }.ToImmutableDictionary();

    public static TheoryData<string> Opening => [.. ListRepoFiles().Where(path => !Refused.ContainsKey(path))];

    public static TheoryData<string> Refusing => [.. Refused.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Opening))]
    public void A_repository_file_opens_against_the_rules(string path)
    {
        var file = FileSourceFetching.ReadTextFile(RepoFiles.ToPath(path));   // impure

        var opened = file.Bind(read => OpenFile(read with { Path = path }));

        Assert.True(opened is Result<DesignSession, string>.Ok, $"{path}: {opened.Match(_ => "", error => error.Failure)}");
    }

    [Theory]
    [MemberData(nameof(Refusing))]
    public void A_fixture_meant_to_be_refused_is_refused_with_its_error(string path)
    {
        var file = FileSourceFetching.ReadTextFile(RepoFiles.ToPath(path));   // impure

        var opened = file.Bind(read => OpenFile(read with { Path = path }));

        var error = Assert.IsType<Result<DesignSession, string>.Error>(opened).Failure;
        Assert.Contains($"{path}:", error);
        Assert.Contains(Refused[path], error);
    }

    [Fact]
    public void Every_kind_of_repository_file_is_found()
    {
        var files = ListRepoFiles();

        Assert.Contains(files, path => path.StartsWith("builds/", StringComparison.Ordinal) && path.EndsWith("/build.yaml", StringComparison.Ordinal));
        Assert.Contains(files, path => path.StartsWith("builds/", StringComparison.Ordinal) && path.EndsWith(".loop.yaml", StringComparison.Ordinal));
        Assert.Contains(files, path => path.StartsWith("tests/", StringComparison.Ordinal) && path.EndsWith(".build.yaml", StringComparison.Ordinal));
        Assert.All(Refused.Keys, path => Assert.Contains(path, files));
    }

    private static Result<DesignSession, string> OpenFile(SourceText file) =>
        file.Path.EndsWith(".loop.yaml", StringComparison.Ordinal)
            ? LoopDesigning.ImportLoop(RepoCatalog.Catalog, file)
            : LoopDesigning.StartDesign(RepoCatalog.Catalog, file, Path.GetFileName(file.Path));

    private static ImmutableArray<string> ListRepoFiles()
    {
        var builds = Directory.EnumerateDirectories(RepoFiles.ToPath("builds"))
            .SelectMany(build => Directory.EnumerateFiles(build, "build.yaml")
                .Concat(Directory.Exists(Path.Combine(build, "loops")) ? Directory.EnumerateFiles(Path.Combine(build, "loops"), "*.loop.yaml") : []));
        var fixtures = Directory.EnumerateFiles(RepoFiles.ToPath("tests/Loopsmith.Web.E2E/Scenarios"), "*.yaml", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".build.yaml", StringComparison.Ordinal) || file.EndsWith(".loop.yaml", StringComparison.Ordinal));
        return [.. builds.Concat(fixtures)
            .Select(file => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
    }
}
