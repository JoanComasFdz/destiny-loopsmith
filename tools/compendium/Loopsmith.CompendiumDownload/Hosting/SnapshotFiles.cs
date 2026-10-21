using System.Collections.Immutable;
using System.IO.Compression;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>
/// Impure boundary: the snapshot folder's files (written as UTF-8 without a byte-order mark, line breaks as given) and
/// the console's error stream. A read that the system refuses is an error, not an exception.
/// </summary>
public static class SnapshotFiles
{
    public static void WriteText(string folder, string file, string text) => File.WriteAllText(Path.Combine(folder, file), text);

    public static void WriteBytes(string folder, string file, byte[] bytes) => File.WriteAllBytes(Path.Combine(folder, file), bytes);

    public static void CreateFolder(string folder, string subfolder) => Directory.CreateDirectory(Path.Combine(folder, subfolder));

    public static void DeleteFile(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool HasTabsJson(string folder) => File.Exists(Path.Combine(folder, "tabs.json"));

    public static string ReadFullPath(string folder) => Path.GetFullPath(folder);

    /// <summary>The folder's own name, however it was given (<c>.</c>, a trailing slash).</summary>
    public static string ReadFolderName(string folder) =>
        Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static long ReadSize(string folder, string file) => new FileInfo(Path.Combine(folder, file)).Length;

    public static Result<Optional<string>, string> ReadText(string folder, string file) =>
        AttemptRead(Path.Combine(folder, file), path => File.Exists(path) ? Optional.Some(File.ReadAllText(path)) : Optional.None<string>());

    /// <summary>tabs.json, meta.json and the tab-list page, the folder's name and tabs.json's date.</summary>
    public static Result<SnapshotFolder, string> ReadSnapshotFolder(string folder) =>
        AttemptRead(folder, _ =>
        {
            var tabsJson = Path.Combine(folder, "tabs.json");
            return new SnapshotFolder(
                ReadIfPresent(tabsJson),
                ReadIfPresent(Path.Combine(folder, "meta.json")),
                ReadIfPresent(Path.Combine(folder, "htmlview_raw.html")),
                ReadFolderName(folder),
                DateOnly.FromDateTime(File.GetLastWriteTime(tabsJson)));
        });

    /// <summary>Each tab's files: its <c>.md</c>'s size, its <c>.csv</c> and its <c>raw/</c> page, each none when missing.</summary>
    public static Result<ImmutableArray<SnapshotTab>, string> ReadTabFiles(string folder, ImmutableArray<SheetTab> tabs) =>
        AttemptRead(folder, _ => tabs.Select((tab, at) =>
        {
            var stem = SheetNaming.ToStem(at + 1, tab.Name);
            var markdown = new FileInfo(Path.Combine(folder, $"{stem}.md"));
            return new SnapshotTab(
                at + 1,
                tab,
                markdown.Exists ? Optional.Some(markdown.Length) : Optional.None<long>(),
                ReadIfPresent(Path.Combine(folder, $"{stem}.csv")),
                ReadIfPresent(Path.Combine(folder, "raw", $"{stem}.html")));
        }).ToImmutableArray());

    /// <summary>Every tab page Google gave (<c>raw/*.html</c>), in name order.</summary>
    public static Result<ImmutableArray<string>, string> ReadRawPages(string folder) =>
        AttemptRead(Path.Combine(folder, "raw"), raw => Directory.Exists(raw)
            ? Directory.EnumerateFiles(raw, "*.html").Order(StringComparer.Ordinal).Select(File.ReadAllText).ToImmutableArray()
            : []);

    /// <summary><c>&lt;folder&gt;.zip</c>, holding the folder (replacing an earlier one); its path.</summary>
    public static Result<string, string> ZipFolder(string folder)
    {
        var full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var zip = full + ".zip";
        try
        {
            File.Delete(zip);
            ZipFile.CreateFromDirectory(full, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
            return new Result<string, string>.Ok(zip);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<string, string>.Error($"Cannot write {zip}: {exception.Message}");
        }
    }

    public static void ReportError(string message) => Console.Error.WriteLine(message);

    private static Optional<string> ReadIfPresent(string path) => File.Exists(path) ? Optional.Some(File.ReadAllText(path)) : Optional.None<string>();

    private static Result<T, string> AttemptRead<T>(string path, Func<string, T> read)
    {
        try
        {
            return new Result<T, string>.Ok(read(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new Result<T, string>.Error($"Cannot read {path}: {exception.Message}");
        }
    }
}
