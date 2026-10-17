using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.SourceFetching;

/// <summary>
/// Impure boundary: the only slice that touches the file system, and it only reads. Foreseeable I/O failures become <c>Result&lt;_, string&gt;</c>; anything unrecoverable bubbles.
/// </summary>
public static class FileSourceFetching
{
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

    /// <summary>
    /// A Compendium snapshot's tab files (<c>snapshots/compendium/&lt;date&gt;/NN_&lt;Tab&gt;.csv</c>), each named by its file name.
    /// </summary>
    public static Result<ImmutableArray<SourceText>, string> ReadCompendiumSnapshot(string snapshotDirectory)
    {
        try
        {
            var paths = Directory.EnumerateFiles(snapshotDirectory, "*.csv", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            var files = ImmutableArray.CreateBuilder<SourceText>();
            foreach (var path in paths)
            {
                var text = File.ReadAllText(path);
                files.Add(new SourceText(Path.GetFileName(path), text));
            }

            return files.Count == 0
                ? new Result<ImmutableArray<SourceText>, string>.Error($"No tab files (*.csv) in '{snapshotDirectory}': is it a snapshot folder (tools/compendium/README.md)?")
                : new Result<ImmutableArray<SourceText>, string>.Ok(files.ToImmutable());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<ImmutableArray<SourceText>, string>.Error($"Cannot read the Compendium snapshot '{snapshotDirectory}': {exception.Message}");
        }
    }
}
