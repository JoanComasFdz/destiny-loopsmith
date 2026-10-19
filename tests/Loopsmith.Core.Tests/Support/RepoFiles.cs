namespace Loopsmith.Core.Tests.Support;

/// <summary>Locates the repository (rules/, the e2e fixtures) from the test binaries.</summary>
public static class RepoFiles
{
    public static readonly string Root = FindRoot();

    public static string ToPath(string relative) => Path.Combine(Root, relative);

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
