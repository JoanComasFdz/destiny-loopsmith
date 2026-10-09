using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.SourceFetching;

/// <summary>
/// Impure boundary: the only slice that touches the file system, and it only reads — writing is the host's job
/// (<c>Effect.SaveFile</c>). Foreseeable I/O failures become <c>Result&lt;_, string&gt;</c>; anything unrecoverable bubbles.
/// </summary>
public static class FileSourceFetching
{
    public const string GlossaryFileName = "glossary.yaml";

    /// <summary>Rules for a build: the explicit directory, else the nearest <c>rules/</c> above the build file or the current directory.</summary>
    public static Result<ImmutableArray<SourceText>, string> ReadRuleFilesFor(Optional<string> rulesDirectory, string buildPath)
    {
        var located = rulesDirectory.IsSome() ? rulesDirectory : FindRulesDirectoryFor(buildPath);
        return located.Match(
            some => ReadRuleFiles(some.Value),
            _ => new Result<ImmutableArray<SourceText>, string>.Error(
                "No rules directory found (a 'rules/' folder with glossary.yaml above the build or the current directory). Use --rules <dir>."));
    }

    public static Result<ImmutableArray<SourceText>, string> ReadRuleFiles(string rulesDirectory)
    {
        try
        {
            var root = Path.GetFullPath(rulesDirectory);
            var paths = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            var files = ImmutableArray.CreateBuilder<SourceText>();
            foreach (var path in paths)
            {
                var text = File.ReadAllText(path);
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                files.Add(new SourceText(relative, text));
            }

            return new Result<ImmutableArray<SourceText>, string>.Ok([.. files.OrderBy(file => file.Path, StringComparer.Ordinal)]);
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
            var text = File.ReadAllText(path);
            return new Result<SourceText, string>.Ok(new SourceText(path, text));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<SourceText, string>.Error($"Cannot read '{path}': {exception.Message}");
        }
    }

    /// <summary>Reads the file when a path is given; no path is not an error.</summary>
    public static Result<Optional<SourceText>, string> ReadOptionalTextFile(Optional<string> path) =>
        path.Match(
            some => ReadTextFile(some.Value).Map(Optional.Some),
            _ => new Result<Optional<SourceText>, string>.Ok(Optional.None<SourceText>()));

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

    public static Optional<string> FindRulesDirectoryFor(string buildPath)
    {
        var buildDirectory = Path.GetDirectoryName(Path.GetFullPath(buildPath)) ?? ".";
        var currentDirectory = Directory.GetCurrentDirectory();
        return FindRulesDirectory([buildDirectory, currentDirectory]);
    }
}
