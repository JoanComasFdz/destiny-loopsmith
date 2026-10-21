using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>What a snapshot folder holds, read for building its INDEX, README and viewer.</summary>
public sealed record SnapshotContents(SheetMeta Meta, System.Collections.Immutable.ImmutableArray<SnapshotTab> Tabs);

/// <summary>Impure: a snapshot folder read whole, and one opened for a rebuild.</summary>
public static class SnapshotReading
{
    /// <summary>The sheet's meta (meta.json, or for an older snapshot what its tab-list page says) and every tab's files.</summary>
    public static Result<SnapshotContents, string> ReadSnapshot(string folder)
    {
        var read = SnapshotFiles.ReadSnapshotFolder(folder);   // impure
        var opened = read.Bind(SnapshotOpening.OpenSnapshot);   // pure
        if (opened is Result<(SheetMeta Meta, System.Collections.Immutable.ImmutableArray<SheetTab> Tabs), string>.Error unreadable)
        {
            return new Result<SnapshotContents, string>.Error(unreadable.Failure);
        }

        var (meta, tabs) = ((Result<(SheetMeta Meta, System.Collections.Immutable.ImmutableArray<SheetTab> Tabs), string>.Ok)opened).Value;
        var files = SnapshotFiles.ReadTabFiles(folder, tabs);   // impure
        return files.Map(snapshotTabs => new SnapshotContents(meta, snapshotTabs));
    }

    /// <summary>A folder downloaded before, and its log: it must hold tabs.json (and raw/, to rebuild from).</summary>
    public static Result<(string Folder, SnapshotLog Log), string> OpenForRebuild(string folder, IReadOnlyList<string> args)
    {
        var hasTabsJson = SnapshotFiles.HasTabsJson(folder);   // impure
        var fullPath = SnapshotFiles.ReadFullPath(folder);   // impure
        if (!hasTabsJson)
        {
            return new Result<(string, SnapshotLog), string>.Error(
                $"'{fullPath}' has no tabs.json. Point --rebuild at the folder that holds tabs.json and raw/ (use '.' if you're already inside it).");
        }

        var log = SnapshotLog.OpenLog(folder, args);   // impure
        return new Result<(string, SnapshotLog), string>.Ok((folder, log));
    }
}
