using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.Support;

/// <summary>Locates the repository (rules/, builds/) from the test binaries and loads real builds.</summary>
public static class RepoFiles
{
    public static readonly string Root = FindRoot();

    public static string ToPath(string relative) => Path.Combine(Root, relative);

    public static ValidatedBuild LoadBuild(string relativeBuildPath) =>
        BuildLoading.LoadValidatedBuild(new LoadRequest(ToPath(relativeBuildPath), Optional.Some(ToPath("rules"))))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

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
