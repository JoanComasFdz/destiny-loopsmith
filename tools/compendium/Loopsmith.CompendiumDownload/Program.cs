using Loopsmith.CompendiumDownload.Core;
using Loopsmith.CompendiumDownload.Hosting;
using Loopsmith.Functional;

// Impure → pure → impure: read the options, then download the sheet's tabs (or take a snapshot already downloaded), its
// images, and build its INDEX.md, README.md and viewer.html (tools/compendium/README.md).
var options = OptionsReading.ReadOptions(args);   // pure
if (options is Result<DownloadOptions, string>.Error badOptions)
{
    SnapshotFiles.ReportError(badOptions.Failure);   // impure
    return 1;
}

var given = ((Result<DownloadOptions, string>.Ok)options).Value;
using var client = new SheetClient();   // impure
var started = given.Command is DownloadCommand.DownloadSheet download
    ? await TabDownloading.DownloadTabs(client, download.Sheet, download.Folder, args)   // impure
    : SnapshotReading.OpenForRebuild(((DownloadCommand.RebuildSnapshot)given.Command).Folder, args);   // impure
if (started is Result<(string Folder, SnapshotLog Log), string>.Error notStarted)
{
    SnapshotFiles.ReportError(notStarted.Failure);   // impure
    return 1;
}

var (folder, log) = ((Result<(string Folder, SnapshotLog Log), string>.Ok)started).Value;
if (!given.NoImages)
{
    var imaged = await ImageDownloading.DownloadImages(client, given.Command is DownloadCommand.RebuildSnapshot, folder, log);   // impure
    if (imaged is Result<Unit, string>.Error notImaged)
    {
        return Fail(log, notImaged.Failure);   // impure
    }
}

var built = SnapshotBuilding.BuildPages(folder, log);   // impure
if (built is Result<Unit, string>.Error notBuilt)
{
    return Fail(log, notBuilt.Failure);   // impure
}

if (given.Zip)
{
    var zipped = SnapshotFiles.ZipFolder(folder);   // impure
    if (zipped is Result<string, string>.Error notZipped)
    {
        return Fail(log, notZipped.Failure);   // impure
    }

    log.Say($"Zipped -> {((Result<string, string>.Ok)zipped).Value}");   // impure
}

var fullPath = SnapshotFiles.ReadFullPath(folder);   // impure
log.Say($"Done -> {fullPath}  (details in {log.LogPath})");   // impure
return 0;

static int Fail(SnapshotLog log, string failure)
{
    log.LogError(failure);   // impure
    SnapshotFiles.ReportError(failure);   // impure
    return 1;
}
