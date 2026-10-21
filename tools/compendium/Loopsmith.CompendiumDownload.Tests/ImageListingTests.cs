using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>The images the tabs use: their links, their files, when to ask again, and the files that record them.</summary>
public sealed class ImageListingTests
{
    [Fact]
    public void A_pages_image_links_are_read_with_their_entities_decoded() =>
        Assert.Equal(
            ["https://lh3.googleusercontent.com/a?w=1&h=2", "http://x/b.png"],
            ImageListing.FindImageUrls("""<img alt="" src="https://lh3.googleusercontent.com/a?w=1&amp;h=2"><img src="data:x"><IMG class="c" src="http://x/b.png">""".Replace("IMG", "img")));

    [Fact]
    public void Those_to_download_are_each_link_once_not_saved_sorted()
    {
        var urls = ImageListing.ListImageUrls(["<img src=\"https://b\"><img src=\"https://a\">", "<img src=\"https://b\"><img src=\"https://c\">"]);

        Assert.Equal(["https://a", "https://c"], ImageListing.ListImagesToDownload(urls, new Dictionary<string, string> { ["https://b"] = "x.png" }));
    }

    [Theory]
    [InlineData("image/png", "feec12a99d311a38.png")]
    [InlineData("image/jpeg", "feec12a99d311a38.jpg")]
    [InlineData("image/x-icon", "feec12a99d311a38.img")]
    public void An_image_is_named_by_its_links_hash_and_its_type(string mediaType, string file) =>
        Assert.Equal(file, ImageListing.NameImageFile("https://lh3.googleusercontent.com/a", mediaType));

    [Theory]
    [InlineData(404, 1, "")]
    [InlineData(410, 1, "")]
    [InlineData(500, 6, "")]
    public void A_link_gone_for_good_or_the_last_attempt_gives_up(int status, int attempt, string retryAfter) =>
        Assert.Equal(new ImageRetry.GiveUp(), ImageListing.DecideAfterAnswer(status, attempt, ToOptional(retryAfter)));

    [Theory]
    [InlineData(429, 1, "", 3)]
    [InlineData(429, 3, "", 12)]
    [InlineData(503, 5, "", 48)]
    [InlineData(429, 2, "7", 7)]
    [InlineData(429, 2, "600", 60)]
    [InlineData(429, 2, "Wed, 21 Oct 2026 07:28:00 GMT", 6)]
    public void Else_it_waits_what_Google_asks_or_doubles_at_most_a_minute(int status, int attempt, string retryAfter, int seconds) =>
        Assert.Equal(new ImageRetry.TryAgain(seconds), ImageListing.DecideAfterAnswer(status, attempt, ToOptional(retryAfter)));

    [Fact]
    public void No_answer_waits_longer_each_time_until_the_last_attempt()
    {
        Assert.Equal(new ImageRetry.TryAgain(4), ImageListing.DecideAfterError(2));
        Assert.Equal(new ImageRetry.GiveUp(), ImageListing.DecideAfterError(6));
    }

    [Fact]
    public void Map_json_is_on_one_line_and_reads_back()
    {
        var map = ImageListing.WriteImageMap([("https://a", "1.png"), ("https://b", "2.png")]);

        Assert.Equal("{\"https://a\": \"1.png\", \"https://b\": \"2.png\"}", map);
        Assert.Equal([("https://a", "1.png"), ("https://b", "2.png")], Assert.IsType<Result<ImmutableArray<(string Url, string File)>, string>.Ok>(ImageListing.ReadImageMap(map)).Value);
    }

    [Fact]
    public void The_viewer_leaves_out_only_the_links_Google_reports_gone()
    {
        var failed = ImageListing.WriteFailedList([new("HTTP 404 text/html", "https://a"), new("HTTP 503 text/html", "https://b"), new("HttpRequestException: refused", "https://c"), new("HTTP 410 ", "https://d")]);

        Assert.Equal(["https://a", "https://d"], ImageListing.ReadGoneImages(failed).Order());
    }

    [Fact]
    public void Failures_are_counted_by_host_and_kind_the_most_first() =>
        Assert.Equal(
            ["        2  b.com                        HTTP 404 text/html", "        1  a.com                        HttpRequestException"],
            ImageListing.SummarizeFailures([new("HttpRequestException: refused", "https://a.com/1"), new("HTTP 404 text/html", "https://b.com/1"), new("HTTP 404 text/html", "https://b.com/2")]));

    [Fact]
    public void One_link_of_each_host_is_asked_first_and_a_dead_hosts_are_dropped()
    {
        ImmutableArray<string> urls = ["https://b.com/1", "https://a.com/1", "https://b.com/2", "https://a.com/2"];

        Assert.Equal([("a.com", "https://a.com/1"), ("b.com", "https://b.com/1")], ImageListing.ListHostProbes(urls));
        Assert.Equal(["https://b.com/1", "https://b.com/2"], ImageListing.DropHost(urls, "a.com"));
    }

    [Theory]
    [InlineData(200, "image/png", true)]
    [InlineData(200, "text/html", false)]
    [InlineData(404, "image/png", false)]
    public void An_answer_is_saved_when_it_succeeded_and_is_an_image(int status, string mediaType, bool savable) =>
        Assert.Equal(savable, ImageListing.IsSavable(status, mediaType));

    [Fact]
    public void A_failed_answer_is_recorded_as_its_status_and_type_and_its_page_on_one_line()
    {
        Assert.Equal("HTTP 404 text/html", ImageListing.DescribeFailedAnswer(404, "text/html"));
        Assert.Equal("<p>gone </p>", ImageListing.ReadFailedBody("text/html", "<p>gone\n</p>"));
        Assert.Equal("", ImageListing.ReadFailedBody("image/png", "binary"));
    }

    private static Optional<string> ToOptional(string text) => text.Length > 0 ? Optional.Some(text) : Optional.None<string>();
}
