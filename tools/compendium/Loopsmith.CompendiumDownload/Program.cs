using Loopsmith.CompendiumDownload.Core;
using Loopsmith.CompendiumDownload.Hosting;
using Loopsmith.Functional;

// Impure → pure → impure: read the options, then download the sheet's tabs (or take a snapshot already downloaded), its
// images, and build its INDEX.md, README.md and viewer.html (tools/compendium/README.md).
var options = OptionsReading.ReadOptions(args);   // pure
if (options is Result<DownloadOptions, string>.Error badOptions)
{
    Console.Error.WriteLine(badOptions.Failure);   // impure
    return 1;
}

var given = ((Result<DownloadOptions, string>.Ok)options).Value;
using var client = new SheetClient();   // impure
var started = given.Command is DownloadCommand.DownloadSheet download
    ? await TabDownloading.DownloadTabs(client, download.Sheet, download.Folder, args)   // impure
    : OpenSnapshot(((DownloadCommand.RebuildSnapshot)given.Command).Folder, args);   // impure
if (started is Result<(string Folder, SnapshotLog Log), string>.Error notStarted)
{
    Console.Error.WriteLine(notStarted.Failure);   // impure
    return 1;
}

var (folder, log) = ((Result<(string Folder, SnapshotLog Log), string>.Ok)started).Value;
var imaged = given.NoImages
    ? new Result<Unit, string>.Ok(new Unit.Value())
    : await ImageDownloading.DownloadImages(client, given.Command is DownloadCommand.RebuildSnapshot, folder, log);   // impure
var built = imaged.Bind(_ => SnapshotBuilding.BuildPages(folder, log));   // impure
var zipped = given.Zip ? built.Bind(_ => SnapshotFiles.ZipFolder(folder)) : built.Map(_ => "");   // impure
if (zipped is Result<string, string>.Error failed)
{
    log.Error(failed.Failure);   // impure
    Console.Error.WriteLine(failed.Failure);   // impure
    return 1;
}

var zip = ((Result<string, string>.Ok)zipped).Value;
if (zip.Length > 0)
{
    log.Say($"Zipped -> {zip}");   // impure
}

log.Say($"Done -> {Path.GetFullPath(folder)}  (details in {log.LogPath})");   // impure
return 0;

// A folder downloaded before: it must hold tabs.json (and raw/, to rebuild from).
static Result<(string Folder, SnapshotLog Log), string> OpenSnapshot(string folder, string[] args) =>
    SnapshotFiles.HasTabsJson(folder)
        ? new Result<(string, SnapshotLog), string>.Ok((folder, SnapshotLog.OpenLog(folder, args)))
        : new Result<(string, SnapshotLog), string>.Error(
            $"'{Path.GetFullPath(folder)}' has no tabs.json. Point --rebuild at the folder that holds tabs.json and raw/ (use '.' if you're already inside it).");
