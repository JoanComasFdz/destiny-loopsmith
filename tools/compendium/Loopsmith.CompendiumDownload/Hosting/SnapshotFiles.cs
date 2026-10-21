using System.Collections.Immutable;
using System.IO.Compression;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>What a snapshot folder holds, read for building its INDEX, README and viewer.</summary>
public sealed record SnapshotContents(SheetMeta Meta, ImmutableArray<SnapshotTab> Tabs);

/// <summary>Impure boundary: the snapshot folder's files (written as UTF-8 without a byte-order mark, line breaks as given).</summary>
public static class SnapshotFiles
{
    public static void WriteText(string folder, string file, string text) => File.WriteAllText(Path.Combine(folder, file), text);

    public static void WriteBytes(string folder, string file, byte[] bytes) => File.WriteAllBytes(Path.Combine(folder, file), bytes);

    public static Optional<string> ReadText(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        return File.Exists(path) ? Optional.Some(File.ReadAllText(path)) : Optional.None<string>();
    }

    public static void CreateFolder(string folder, string subfolder) => Directory.CreateDirectory(Path.Combine(folder, subfolder));

    public static void DeleteFile(string folder, string file)
    {
        var path = Path.Combine(folder, file);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static long ReadSize(string folder, string file) => new FileInfo(Path.Combine(folder, file)).Length;

    public static bool HasTabsJson(string folder) => File.Exists(Path.Combine(folder, "tabs.json"));

    /// <summary>Every tab page Google gave (<c>raw/*.html</c>), in name order.</summary>
    public static ImmutableArray<string> ReadRawPages(string folder)
    {
        var raw = Path.Combine(folder, "raw");
        return Directory.Exists(raw)
            ? [.. Directory.EnumerateFiles(raw, "*.html").Order(StringComparer.Ordinal).Select(File.ReadAllText)]
            : [];
    }

    /// <summary>The sheet's meta (meta.json, or for an older snapshot what its tab-list page says) and every tab's files.</summary>
    public static Result<SnapshotContents, string> ReadSnapshot(string folder)
    {
        var tabsJson = ReadText(folder, "tabs.json");
        if (tabsJson is not Optional<string>.Some tabsText)
        {
            return new Result<SnapshotContents, string>.Error($"No tabs.json in {folder}.");
        }

        var metaJson = ReadText(folder, "meta.json");
        var tabListHtml = ReadText(folder, "htmlview_raw.html");
        var tabsJsonDate = DateOnly.FromDateTime(File.GetLastWriteTime(Path.Combine(folder, "tabs.json")));
        var meta = metaJson is Optional<string>.Some metaText
            ? MetaReading.ReadMetaJson(metaText.Value)
            : new Result<SheetMeta, string>.Ok(MetaReading.BuildFallbackMeta(tabListHtml is Optional<string>.Some html ? html.Value : "", Path.GetFileName(Path.GetFullPath(folder)), tabsJsonDate));
        var tabs = MetaReading.ReadTabsJson(tabsText.Value);
        return meta.Bind(found => tabs.Map(listed => new SnapshotContents(found, [.. listed.Select((tab, at) => ReadTab(folder, at + 1, tab))])));
    }

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

    private static SnapshotTab ReadTab(string folder, int place, SheetTab tab)
    {
        var stem = SheetNaming.ToStem(place, tab.Name);
        var markdown = new FileInfo(Path.Combine(folder, $"{stem}.md"));
        return new SnapshotTab(
            place,
            tab,
            markdown.Exists ? Optional.Some(markdown.Length) : Optional.None<long>(),
            ReadText(folder, $"{stem}.csv"),
            ReadText(folder, Path.Combine("raw", $"{stem}.html")));
    }
}
