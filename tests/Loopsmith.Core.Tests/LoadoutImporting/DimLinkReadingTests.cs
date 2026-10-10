using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoadoutImporting;

namespace Loopsmith.Core.Tests.LoadoutImporting;

/// <summary>docs/loop-format.md "Starting from a DIM link": the two kinds of link DIM itself opens.</summary>
public sealed class DimLinkReadingTests
{
    [Theory]
    [InlineData("https://dim.gg/4j5nz4q", "https://dim.gg/4j5nz4q")]
    [InlineData("https://dim.gg/4j5nz4q/Skip-Grenade-Hunter", "https://dim.gg/4j5nz4q/Skip-Grenade-Hunter")]
    [InlineData("  http://dim.gg/4j5nz4q/  ", "http://dim.gg/4j5nz4q/")]
    [InlineData("dim.gg/4j5nz4q", "https://dim.gg/4j5nz4q")]
    [InlineData("4j5nz4q", "https://dim.gg/4j5nz4q")]
    public void A_dim_gg_share_is_read_as_its_id_and_an_absolute_link(string text, string link)
    {
        var shared = Assert.IsType<DimLink.Shared>(Read(text));

        Assert.Equal(("4j5nz4q", link), (shared.ShareId.Value, shared.Link));
    }

    [Fact]
    public void A_share_id_is_asked_for_at_the_DIM_API()
    {
        Assert.Equal("https://api.destinyitemmanager.com/loadout_share?shareId=4j5nz4q", DimLinkReading.ToShareRequestUrl(DimShareId.From("4j5nz4q")));
    }

    [Fact]
    public void A_link_to_DIMs_loadouts_page_carries_its_loadout_percent_encoded()
    {
        const string link = "https://app.destinyitemmanager.com/loadouts?loadout=%7B%22name%22%3A%22Skip+Grenade%22%2C%22classType%22%3A1%7D";

        var inline = Assert.IsType<DimLink.Inline>(Read(link));

        Assert.Equal(("""{"name":"Skip Grenade","classType":1}""", link), (inline.Loadout, inline.Link));
    }

    [Fact]
    public void A_loadouts_link_may_leave_its_JSON_unencoded_and_name_an_account_in_its_path()
    {
        var inline = Assert.IsType<DimLink.Inline>(Read("""https://app.destinyitemmanager.com/4611686018483139177/d2/loadouts?loadout={%22name%22:%22A%22}&other=1#top"""));

        Assert.Equal("""{"name":"A"}""", inline.Loadout);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dim.gg/4j5")]
    [InlineData("https://www.youtube.com/watch?v=zvd6sNS463E")]
    [InlineData("https://app.destinyitemmanager.com/loadouts")]
    [InlineData("https://app.destinyitemmanager.com/inventory?loadout=%7B%7D")]
    public void Anything_else_is_not_a_DIM_link(string text)
    {
        var error = Assert.IsType<Result<DimLink, string>.Error>(DimLinkReading.ReadDimLink(text));

        Assert.Equal(DimLinkReading.NotADimLink, error.Failure);
    }

    private static DimLink Read(string text) =>
        DimLinkReading.ReadDimLink(text).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
