using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>The command line: <c>download &lt;dir&gt;</c> or <c>extract &lt;dir&gt; [--version …] [--root …]</c>.</summary>
public sealed class OptionsReadingTests
{
    [Fact]
    public void Download_takes_the_folder() =>
        Assert.Equal(new ManifestCommand.Download("/tmp/manifest"), OptionsReading.ReadOptions(["download", "/tmp/manifest"]).ToValue());

    [Fact]
    public void Extract_reads_the_version_file_and_writes_into_this_folder_unless_told_otherwise()
    {
        var plain = (ManifestCommand.Extract)OptionsReading.ReadOptions(["extract", "/tmp/manifest"]).ToValue();
        var told = (ManifestCommand.Extract)OptionsReading.ReadOptions(["extract", "/tmp/manifest", "--root", "/repo", "--version", "v1"]).ToValue();

        Assert.Equal(("/tmp/manifest", false, "."), (plain.Folder, plain.Version.IsSome(), plain.Root));
        Assert.Equal(("/tmp/manifest", Optional.Some("v1"), "/repo"), (told.Folder, told.Version, told.Root));
    }

    [Theory]
    [InlineData(new string[0], "The command is 'download' or 'extract'.")]
    [InlineData(new[] { "post" }, "The command is 'download' or 'extract'.")]
    [InlineData(new[] { "download" }, "Give the manifest folder.")]
    [InlineData(new[] { "extract", "--root", "/repo" }, "Give the manifest folder.")]
    [InlineData(new[] { "extract", "/tmp/manifest", "--version" }, "'--version' needs a value.")]
    [InlineData(new[] { "extract", "/tmp/manifest", "--merged" }, "Unknown option '--merged'.")]
    public void A_wrong_command_line_says_what_is_wrong_and_how_to_call_it(string[] args, string failure)
    {
        var message = OptionsReading.ReadOptions(args).ToFailure();

        Assert.StartsWith(failure, message);
        Assert.EndsWith(OptionsReading.Usage, message);
    }
}
