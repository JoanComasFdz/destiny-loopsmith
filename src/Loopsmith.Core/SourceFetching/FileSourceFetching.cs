using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.SourceFetching;

/// <summary>
/// Impure boundary: the only slice that touches the file system. Foreseeable I/O failures become
/// <c>Result&lt;_, string&gt;</c>; anything unrecoverable bubbles.
/// </summary>
public static class FileSourceFetching
{
    public const string GlossaryFileName = "glossary.yaml";

    public static Result<ImmutableArray<SourceText>, string> ReadRuleFiles(string rulesDirectory)
    {
        try
        {
            var root = Path.GetFullPath(rulesDirectory);
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
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

    public static Result<SourceText, string> ReadTextFile(string path)
    {
        try
        {
            return new Result<SourceText, string>.Ok(new SourceText(path, File.ReadAllText(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<SourceText, string>.Error($"Cannot read '{path}': {exception.Message}");
        }
    }

    /// <summary>Nearest <c>rules/</c> folder (containing glossary.yaml) at or above any of the start directories.</summary>
    public static Optional<string> FindRulesDirectory(ImmutableArray<string> startDirectories)
    {
        foreach (var start in startDirectories)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "rules");
                if (File.Exists(Path.Combine(candidate, GlossaryFileName)))
                {
                    return Optional.Some(candidate);
                }

                directory = directory.Parent;
            }
        }

        return Optional.None<string>();
    }

    /// <summary>Rules for a build: the nearest <c>rules/</c> above the build file, else above the current directory.</summary>
    public static Optional<string> FindRulesDirectoryFor(string buildPath)
    {
        var buildDirectory = Path.GetDirectoryName(Path.GetFullPath(buildPath)) ?? ".";
        return FindRulesDirectory([buildDirectory, Directory.GetCurrentDirectory()]);
    }
}
