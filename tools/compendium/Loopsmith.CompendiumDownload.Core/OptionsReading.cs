using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>What the tool does: download a sheet into a snapshot folder, or rebuild one it already downloaded.</summary>
[Union]
public partial record DownloadCommand
{
    /// <summary>Download the sheet <see cref="Sheet"/> (its URL or id) into <see cref="Folder"/>, by default a new dated one.</summary>
    partial record DownloadSheet(string Sheet, Optional<string> Folder);

    /// <summary>Download no tab again: fetch the images missing, rebuild INDEX.md, README.md and viewer.html.</summary>
    partial record RebuildSnapshot(string Folder);
}

/// <summary>The command line, read: what to do, whether to skip the images and whether to zip the folder after.</summary>
public sealed record DownloadOptions(DownloadCommand Command, bool NoImages, bool Zip);

/// <summary>The command line → the options, or the usage text.</summary>
public static class OptionsReading
{
    public const string Usage =
        """
        Dumps every tab of a view-only public Google Sheet (the Destiny Data Compendium) into a snapshot folder:
        one NN_<Tab>.md and .csv per tab, INDEX.md, README.md, an offline viewer.html, images/, Google's raw pages
        (raw/, gviz/), tabs.json, meta.json and download.log (tools/compendium/README.md).

        usage:
          dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- <sheet URL or id> [<folder>] [--no-images] [--zip]
          dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- --rebuild [<folder>] [--no-images] [--zip]

          <folder>      by default <sheet title>_<YYYY-MM-DD_HHMM>; --rebuild's by default the current folder
          --rebuild     download no tab again: fetch the images missing, rebuild INDEX.md, README.md and viewer.html
          --no-images   skip the images
          --zip         then write <folder>.zip, holding the folder
        """;

    public static Result<DownloadOptions, string> ReadOptions(IReadOnlyList<string> args)
    {
        var noImages = args.Contains("--no-images");
        var zip = args.Contains("--zip");
        var rest = args.Where(arg => arg is not "--no-images" and not "--zip").ToList();
        var unknown = rest.Where(arg => arg.StartsWith("--", StringComparison.Ordinal) && arg != "--rebuild").ToList();
        return (rest, unknown) switch
        {
            (_, [var first, ..]) => new Result<DownloadOptions, string>.Error($"Unknown option '{first}'.\n\n{Usage}"),
            ([], _) => new Result<DownloadOptions, string>.Error(Usage),
            (["--rebuild"], _) => new Result<DownloadOptions, string>.Ok(new DownloadOptions(new DownloadCommand.RebuildSnapshot("."), noImages, zip)),
            (["--rebuild", var folder, ..], _) => new Result<DownloadOptions, string>.Ok(new DownloadOptions(new DownloadCommand.RebuildSnapshot(folder), noImages, zip)),
            ([var sheet], _) => new Result<DownloadOptions, string>.Ok(new DownloadOptions(new DownloadCommand.DownloadSheet(sheet, Optional.None<string>()), noImages, zip)),
            ([var sheet, var folder, ..], _) => new Result<DownloadOptions, string>.Ok(new DownloadOptions(new DownloadCommand.DownloadSheet(sheet, Optional.Some(folder)), noImages, zip)),
        };
    }
}
