using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>The command line: a sheet to download (into a folder given or a dated one), or a snapshot to rebuild.</summary>
public sealed class OptionsReadingTests
{
    [Fact]
    public void A_sheet_and_a_folder_download_into_that_folder()
    {
        var options = Read(["https://docs.google.com/spreadsheets/d/abc/edit", "snapshots/x", "--no-images"]);

        Assert.Equal(new DownloadCommand.DownloadSheet("https://docs.google.com/spreadsheets/d/abc/edit", Optional.Some("snapshots/x")), options.Command);
        Assert.True(options.NoImages);
        Assert.False(options.Zip);
    }

    [Fact]
    public void A_sheet_alone_downloads_into_a_dated_folder() =>
        Assert.Equal(new DownloadCommand.DownloadSheet("abc", Optional.None<string>()), Read(["abc"]).Command);

    [Fact]
    public void The_flags_go_anywhere()
    {
        var options = Read(["--zip", "--rebuild", "--no-images", "old"]);

        Assert.Equal(new DownloadCommand.RebuildSnapshot("old"), options.Command);
        Assert.True(options.NoImages);
        Assert.True(options.Zip);
    }

    [Fact]
    public void Rebuild_without_a_folder_rebuilds_the_current_one() =>
        Assert.Equal(new DownloadCommand.RebuildSnapshot("."), Read(["--rebuild"]).Command);

    [Fact]
    public void No_arguments_give_the_usage() =>
        Assert.Equal(new Result<DownloadOptions, string>.Error(OptionsReading.Usage), OptionsReading.ReadOptions([]));

    [Fact]
    public void An_unknown_option_is_named()
    {
        var read = OptionsReading.ReadOptions(["abc", "--images"]);

        Assert.StartsWith("Unknown option '--images'.", Assert.IsType<Result<DownloadOptions, string>.Error>(read).Failure);
    }

    private static DownloadOptions Read(string[] args) => Assert.IsType<Result<DownloadOptions, string>.Ok>(OptionsReading.ReadOptions(args)).Value;
}
