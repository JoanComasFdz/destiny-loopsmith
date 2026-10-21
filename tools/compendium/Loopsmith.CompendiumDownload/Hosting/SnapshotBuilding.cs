using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>Impure: a snapshot's INDEX.md, README.md and viewer.html built from the files it holds (downloading nothing).</summary>
public static class SnapshotBuilding
{
    public static Result<Unit, string> BuildPages(string folder, SnapshotLog log)
    {
        var snapshot = SnapshotFiles.ReadSnapshot(folder);   // impure
        if (snapshot is Result<SnapshotContents, string>.Error unreadable)
        {
            return new Result<Unit, string>.Error(unreadable.Failure);
        }

        var (meta, tabs) = ((Result<SnapshotContents, string>.Ok)snapshot).Value;
        var index = IndexWriting.WriteIndex(meta, tabs);   // pure
        SnapshotFiles.WriteText(folder, "INDEX.md", index);   // impure
        SnapshotFiles.DeleteFile(folder, "ALL.md");   // impure: an older snapshot's, all tabs in one file
        log.Say($"Wrote {Path.Combine(folder, "INDEX.md")}");   // impure

        var folderName = Path.GetFileName(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));   // impure
        var readme = ReadmeWriting.WriteReadme(meta, tabs, folderName);   // pure
        SnapshotFiles.WriteText(folder, "README.md", readme);   // impure
        log.Say($"Wrote {Path.Combine(folder, "README.md")}");   // impure

        var mapJson = SnapshotFiles.ReadText(folder, Path.Combine("images", "map.json"));   // impure
        var failedList = SnapshotFiles.ReadText(folder, Path.Combine("images", "failed.txt"));   // impure
        var images = mapJson is Optional<string>.Some json ? ImageListing.ReadImageMap(json.Value) : new Result<ImmutableArray<(string Url, string File)>, string>.Ok([]);   // pure
        if (images is Result<ImmutableArray<(string Url, string File)>, string>.Error badMap)
        {
            return new Result<Unit, string>.Error(badMap.Failure);
        }

        var saved = ((Result<ImmutableArray<(string Url, string File)>, string>.Ok)images).Value.DistinctBy(image => image.Url).ToDictionary(image => image.Url, image => image.File);   // pure
        var gone = failedList is Optional<string>.Some failed ? ImageListing.ReadGoneImages(failed.Value) : [];   // pure
        var viewer = ViewerWriting.WriteViewer(meta, tabs, saved, gone);   // pure
        SnapshotFiles.WriteText(folder, "viewer.html", viewer);   // impure
        if (gone.Count > 0)
        {
            log.Say($"Viewer: left out {gone.Count} images Google reports as missing");   // impure
        }

        var megabytes = SnapshotFiles.ReadSize(folder, "viewer.html") / 1e6;   // impure
        log.Say($"Wrote {Path.Combine(folder, "viewer.html")} ({megabytes:0.0} MB)");   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }
}
