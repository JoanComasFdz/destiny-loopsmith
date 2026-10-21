namespace Loopsmith.ManifestTools.Tests.Support;

/// <summary>The repository's root, found from the test's output folder (the folder holding Loopsmith.slnx).</summary>
internal static class RepoFiles
{
    public static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);   // impure
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Loopsmith.slnx")))   // impure
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("No Loopsmith.slnx above the test's folder.");
    }
}
