using System.Collections.Immutable;
using System.Reflection;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.RuleViewer.Page;

namespace Loopsmith.RuleViewer.Hosting;

/// <summary>
/// Impure boundary of the rule viewer: finds the repository, reads the rule files and the page's style and script embedded
/// in this assembly, writes the lanes' files and tells the console. A host calls only Orchestration, the kernel and its own pure project
/// (HostBoundaryTests), so it reads the rule files itself, as Core's SourceFetching does: every <c>*.yaml</c> under the
/// folder, named by its path from it.
/// </summary>
public static class RuleViewerFiles
{
    private const string StyleName = "lanes.css";
    private const string ScriptName = "lanes.js";
    private const string SolutionFile = "Loopsmith.slnx";

    /// <summary>The repository root: the nearest folder up from <paramref name="start"/> holding the solution, else <paramref name="start"/>.</summary>
    public static string FindRepoRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        return start;
    }

    public static Result<ImmutableArray<SourceText>, string> ReadRuleFiles(string rulesDirectory)
    {
        try
        {
            var root = Path.GetFullPath(rulesDirectory);
            var files = Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories)
                .Select(path => new SourceText(Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)))
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .ToImmutableArray();
            return new Result<ImmutableArray<SourceText>, string>.Ok(files);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<ImmutableArray<SourceText>, string>.Error($"Cannot read rules from '{rulesDirectory}': {exception.Message}");
        }
    }

    public static Result<PageAssets, string> ReadPageAssets(Assembly assembly) =>
        ReadResource(assembly, StyleName).Bind(style => ReadResource(assembly, ScriptName).Map(script => new PageAssets(style, script)));

    private static Result<string, string> ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return new Result<string, string>.Error($"The page's '{name}' is not embedded in the tool.");
        }

        using var reader = new StreamReader(stream);
        return new Result<string, string>.Ok(reader.ReadToEnd());
    }

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/>, creating its folder.</summary>
    public static Result<string, string> WriteFile(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, text);
            return new Result<string, string>.Ok(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<string, string>.Error($"Cannot write '{path}': {exception.Message}");
        }
    }

    public static void Report(string message) => Console.WriteLine(message);

    public static void ReportError(string message) => Console.Error.WriteLine(message);
}
