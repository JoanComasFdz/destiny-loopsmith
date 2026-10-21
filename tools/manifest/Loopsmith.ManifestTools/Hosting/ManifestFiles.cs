using System.Collections.Immutable;
using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;

namespace Loopsmith.ManifestTools.Hosting;

/// <summary>
/// Impure boundary: the manifest folder (its components and version.txt), the repository's rule files,
/// tools/manifest/test-items.yaml and rules/manifest.yaml, and the console.
/// </summary>
public static class ManifestFiles
{
    /// <summary>Each component's JSON, in <see cref="ManifestTableParsing.ComponentNames"/>' order, as bytes (the parser reads UTF-8).</summary>
    public static Result<ImmutableArray<(string Name, byte[] Json)>, string> ReadComponents(string folder) =>
        ManifestTableParsing.ComponentNames
            .Select(name => ReadBytes(Path.Combine(folder, $"{name}.json")).Map(json => (name, json)))
            .CombineAll()
            .MapError(failures => string.Join("\n", failures));

    /// <summary>The folder's version.txt, as written by the download.</summary>
    public static Result<string, string> ReadVersionFile(string folder) => ReadText(Path.Combine(folder, "version.txt"));

    /// <summary>The text of every file under <c>&lt;root&gt;/rules/</c> the hashes are read from (<see cref="NamedHashReading.IsRuleSource"/>).</summary>
    public static Result<ImmutableArray<string>, string> ReadRuleTexts(string root)
    {
        var rules = Path.Combine(root, "rules");
        if (!Directory.Exists(rules))
        {
            return new Result<ImmutableArray<string>, string>.Error($"{rules} isn't there: pass the repository with --root.");
        }

        return Directory.EnumerateFiles(rules, "*.yaml", SearchOption.AllDirectories)
            .Where(path => NamedHashReading.IsRuleSource(Path.GetRelativePath(root, path).Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .Select(ReadText)
            .CombineAll()
            .MapError(failures => string.Join("\n", failures));
    }

    public static Result<string, string> ReadTestItems(string root) => ReadText(Path.Combine(root, "tools", "manifest", "test-items.yaml"));

    /// <summary>Writes rules/manifest.yaml (UTF-8, no byte order mark, <c>\n</c> line ends as given).</summary>
    public static Result<Unit, string> WriteExcerpt(string root, string text) => WriteText(Path.Combine(root, "rules", "manifest.yaml"), text);

    public static Result<Unit, string> WriteText(string path, string text)
    {
        try
        {
            File.WriteAllText(path, text);
            return new Result<Unit, string>.Ok(new Unit.Value());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<Unit, string>.Error($"Cannot write {path}: {exception.Message}");
        }
    }

    public static Result<Unit, string> CreateFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            return new Result<Unit, string>.Ok(new Unit.Value());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<Unit, string>.Error($"Cannot create {folder}: {exception.Message}");
        }
    }

    public static void Report(string line) => Console.WriteLine(line);

    public static void ReportError(string message) => Console.Error.WriteLine(message);

    public static void ReportMissing(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Console.Error.WriteLine(line);
        }
    }

    private static Result<string, string> ReadText(string path)
    {
        try
        {
            return new Result<string, string>.Ok(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Result<string, string>.Error($"{path} is missing.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<string, string>.Error($"Cannot read {path}: {exception.Message}");
        }
    }

    private static Result<byte[], string> ReadBytes(string path)
    {
        try
        {
            return new Result<byte[], string>.Ok(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Result<byte[], string>.Error($"{path} is missing: download the manifest first (download <dir>).");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<byte[], string>.Error($"Cannot read {path}: {exception.Message}");
        }
    }
}
