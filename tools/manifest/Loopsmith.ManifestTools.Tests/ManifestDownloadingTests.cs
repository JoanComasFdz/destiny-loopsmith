using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>The download: the manifest index (or the version given in its place) → the components' addresses and version.txt.</summary>
public sealed class ManifestDownloadingTests
{
    [Fact]
    public void The_index_gives_the_version_and_the_English_content_id()
    {
        const string index = """
            {
              "Response": {
                "version": "245114.26.10.13.2000-1-bnet.66137",
                "jsonWorldComponentContentPaths": {
                  "de": { "DestinyInventoryItemDefinition": "/common/destiny2_content/json/de/DestinyInventoryItemDefinition-other.json" },
                  "en": { "DestinyInventoryItemDefinition": "/common/destiny2_content/json/en/DestinyInventoryItemDefinition-e677f6ea-2a1e-48d4-a8c8-700a39a62585.json" }
                }
              }
            }
            """;

        Assert.Equal(
            new ManifestVersion("245114.26.10.13.2000-1-bnet.66137", "e677f6ea-2a1e-48d4-a8c8-700a39a62585"),
            ManifestDownloading.ReadIndex(index).ToValue());
    }

    [Theory]
    [InlineData("<html>500</html>", "The manifest index isn't JSON with a Response")]
    [InlineData("""{ "Response": { "jsonWorldComponentContentPaths": {} } }""", "The manifest index has no version.")]
    [InlineData("""{ "Response": { "version": "1", "jsonWorldComponentContentPaths": { "en": { "DestinyInventoryItemDefinition": "/x.json" } } } }""", "The manifest index has no English")]
    public void An_index_that_lacks_what_the_download_needs_is_an_error_saying_what(string index, string failure) =>
        Assert.StartsWith(failure, ManifestDownloading.ReadIndex(index).ToFailure());

    [Fact]
    public void A_given_content_id_takes_the_index_place_with_an_unknown_version_when_none_is_given()
    {
        Assert.Equal(Optional.Some(new ManifestVersion("v1", "abc")), ManifestDownloading.ReadGivenVersion(Optional.Some("abc"), Optional.Some("v1")));
        Assert.Equal(Optional.Some(new ManifestVersion("unknown", "abc")), ManifestDownloading.ReadGivenVersion(Optional.Some("abc"), Optional.None<string>()));
        Assert.False(ManifestDownloading.ReadGivenVersion(Optional.Some(""), Optional.Some("v1")).IsSome());
        Assert.False(ManifestDownloading.ReadGivenVersion(Optional.None<string>(), Optional.Some("v1")).IsSome());
    }

    [Fact]
    public void Each_of_the_twelve_components_downloads_from_its_English_file_of_the_content_id()
    {
        var downloads = ManifestDownloading.ListDownloads("abc");

        Assert.Equal(12, downloads.Length);
        Assert.Equal(
            new ComponentDownload("DestinyStatDefinition.json", "https://www.bungie.net/common/destiny2_content/json/en/DestinyStatDefinition-abc.json"),
            downloads[^1]);
    }

    [Fact]
    public void The_version_file_is_the_version_on_one_line_and_reads_back_trimmed()
    {
        var written = ManifestDownloading.WriteVersionFile(new ManifestVersion("v1", "abc"));

        Assert.Equal("v1\n", written);
        Assert.Equal(Optional.Some("v1"), ManifestDownloading.ReadVersionFile(written));
        Assert.False(ManifestDownloading.ReadVersionFile("\n").IsSome());
    }
}
