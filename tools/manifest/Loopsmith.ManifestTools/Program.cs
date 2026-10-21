using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Hosting;

// Impure → pure → impure: read the command line, then download a manifest or write rules/manifest.yaml from one
// (tools/manifest/README.md).
var options = OptionsReading.ReadOptions(args);   // pure
if (options is Result<ManifestCommand, string>.Error badOptions)
{
    ManifestFiles.ReportError(badOptions.Failure);   // impure
    return 2;
}

var command = ((Result<ManifestCommand, string>.Ok)options).Value;
var done = command is ManifestCommand.Download download
    ? await ManifestDownload.DownloadManifest(download)
    : ExcerptExtraction.ExtractExcerpt((ManifestCommand.Extract)command);   // impure
if (done is Result<Unit, string>.Error failed)
{
    ManifestFiles.ReportError(failed.Failure);   // impure
    return 1;
}

return 0;
