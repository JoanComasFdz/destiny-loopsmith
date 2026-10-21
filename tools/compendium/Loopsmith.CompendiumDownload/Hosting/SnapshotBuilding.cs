using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>Impure: a snapshot's INDEX.md, README.md and viewer.html built from the files it holds (downloading nothing).</summary>
public static class SnapshotBuilding
{
    public static Result<Unit, string> BuildPages(string folder, SnapshotLog log)
    {
        var snapshot = SnapshotReading.ReadSnapshot(folder);   // impure
        var folderName = SnapshotFiles.ReadFolderName(folder);   // impure
        var mapJson = SnapshotFiles.ReadText(folder, Path.Combine("images", "map.json"));   // impure
        var failedList = SnapshotFiles.ReadText(folder, Path.Combine("images", "failed.txt"));   // impure
        var images = mapJson.Bind(ReadImageMap);   // pure
        var gone = failedList.Map(ReadGoneImages);   // pure
        var read = snapshot.Bind(contents => images.Bind(saved => gone.Map(goneImages => (Contents: contents, Saved: saved, Gone: goneImages))));   // pure
        if (read is Result<(SnapshotContents Contents, IReadOnlyDictionary<string, string> Saved, ImmutableHashSet<string> Gone), string>.Error unreadable)
        {
            return new Result<Unit, string>.Error(unreadable.Failure);
        }

        var (contents, saved, goneImages) = ((Result<(SnapshotContents Contents, IReadOnlyDictionary<string, string> Saved, ImmutableHashSet<string> Gone), string>.Ok)read).Value;
        var index = IndexWriting.WriteIndex(contents.Meta, contents.Tabs);   // pure
        SnapshotFiles.WriteText(folder, "INDEX.md", index);   // impure
        SnapshotFiles.DeleteFile(folder, "ALL.md");   // impure: an older snapshot's, all tabs in one file
        log.Say($"Wrote {Path.Combine(folder, "INDEX.md")}");   // impure

        var readme = ReadmeWriting.WriteReadme(contents.Meta, contents.Tabs, folderName);   // pure
        SnapshotFiles.WriteText(folder, "README.md", readme);   // impure
        log.Say($"Wrote {Path.Combine(folder, "README.md")}");   // impure

        var viewer = ViewerWriting.WriteViewer(contents.Meta, contents.Tabs, saved, goneImages);   // pure
        SnapshotFiles.WriteText(folder, "viewer.html", viewer);   // impure
        if (goneImages.Count > 0)
        {
            log.Say($"Viewer: left out {goneImages.Count} images Google reports as missing");   // impure
        }

        var bytes = SnapshotFiles.ReadSize(folder, "viewer.html");   // impure
        log.Say($"Wrote {Path.Combine(folder, "viewer.html")} ({bytes / 1e6:0.0} MB)");   // impure
        return new Result<Unit, string>.Ok(new Unit.Value());
    }

    private static Result<IReadOnlyDictionary<string, string>, string> ReadImageMap(Optional<string> mapJson) =>
        mapJson is Optional<string>.Some json
            ? ImageListing.ReadImageMap(json.Value).Map(IReadOnlyDictionary<string, string> (saved) => saved.DistinctBy(image => image.Url).ToDictionary(image => image.Url, image => image.File))
            : new Result<IReadOnlyDictionary<string, string>, string>.Ok(new Dictionary<string, string>());

    private static ImmutableHashSet<string> ReadGoneImages(Optional<string> failedList) =>
        failedList is Optional<string>.Some failed ? ImageListing.ReadGoneImages(failed.Value) : [];
}
