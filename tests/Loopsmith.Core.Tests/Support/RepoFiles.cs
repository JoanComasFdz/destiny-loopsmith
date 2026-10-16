using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.SourceFetching;

namespace Loopsmith.Core.Tests.Support;

/// <summary>Locates the repository (rules/, builds/) from the test binaries and loads real builds.</summary>
public static class RepoFiles
{
    public static readonly string Root = FindRoot();

    public static string ToPath(string relative) => Path.Combine(Root, relative);

    /// <summary>A build file of the repository, parsed and validated against its rules (<see cref="RepoCatalog"/>).</summary>
    public static ValidatedBuild LoadBuild(string relativeBuildPath)
    {
        var path = ToPath(relativeBuildPath);                                                       // pure
        var file = FileSourceFetching.ReadTextFile(path);                                           // impure

        var built = file                                                                            // pure
            .Bind(read => BuildFileParsing.ParseBuildFile(read, RepoCatalog.Manifest))
            .Bind(build => BuildValidation.ValidateBuild(build, RepoCatalog.Catalog));
        return built.Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Loopsmith.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Loopsmith.slnx) not found.");
    }
}
