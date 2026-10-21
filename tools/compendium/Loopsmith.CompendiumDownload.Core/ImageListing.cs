using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dunet;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>What to do after an image's request failed: try again after a wait, or give up on it.</summary>
[Union]
public partial record ImageRetry
{
    partial record TryAgain(int WaitSeconds);
    partial record GiveUp();
}

/// <summary>An image that couldn't be saved: why (<c>HTTP 404 text/html</c>, or the error), and its link.</summary>
public sealed record FailedImage(string Error, string Url);

/// <summary>
/// The images the tabs use, saved into <c>images/</c> so the viewer works offline: which links Google's pages hold, the
/// file each is saved as, when to try a failed one again, and the files that record them (<c>map.json</c>, <c>failed.txt</c>).
/// </summary>
public static partial class ImageListing
{
    /// <summary>How many times an image is asked for before it is given up.</summary>
    public const int Attempts = 6;

    /// <summary>The image links a tab's page holds (<c>&lt;img … src="https://…"&gt;</c>), its entities decoded.</summary>
    public static ImmutableArray<string> FindImageUrls(string rawHtml) =>
        [.. CreateImageSourcePattern().Matches(rawHtml).Select(found => DecodeEntities(found.Groups[1].Value))];

    /// <summary>Every image link the pages hold, each once.</summary>
    public static ImmutableArray<string> ListImageUrls(IEnumerable<string> rawHtmls) =>
        [.. rawHtmls.SelectMany(html => FindImageUrls(html)).Distinct()];

    /// <summary>The links still to download: those <paramref name="saved"/> hasn't, sorted.</summary>
    public static ImmutableArray<string> ListImagesToDownload(ImmutableArray<string> urls, IReadOnlyDictionary<string, string> saved) =>
        [.. urls.Where(url => !saved.ContainsKey(url)).Order(StringComparer.Ordinal)];

    /// <summary>The file an image is saved as: the start of its link's SHA-1, then its type's extension.</summary>
    public static string NameImageFile(string url, string contentType) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..16] + "." + ToExtension(contentType);

    /// <summary>An answer's media type without its parameters (<c>image/png; …</c> → <c>image/png</c>).</summary>
    public static string ReadMediaType(string contentType) => contentType.Split(';')[0].Trim();

    public static bool IsImage(string mediaType) => mediaType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>An answer is an image to save when it succeeded (below 400) and is an image.</summary>
    public static bool IsSavable(int status, string mediaType) => status < 400 && IsImage(mediaType);

    /// <summary>The error an answer that isn't an image is recorded with (failed.txt reads it back): <c>HTTP 404 text/html</c>.</summary>
    public static string DescribeFailedAnswer(int status, string mediaType) => $"HTTP {status} {mediaType}";

    /// <summary>What an answer that isn't an image said, for the log: its first 300 characters on one line; nothing for an image.</summary>
    public static string ReadFailedBody(string mediaType, string text) =>
        IsImage(mediaType) ? "" : TextTrimming.TakeCharacters(text, 300).Replace('\n', ' ');

    /// <summary>The first link of each host, by host name: one is asked first, so a host that never answers is found quickly.</summary>
    public static ImmutableArray<(string Host, string Url)> ListHostProbes(ImmutableArray<string> urls) =>
        [.. urls.GroupBy(ReadHost).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => (group.Key, group.First()))];

    /// <summary>The links without a host's.</summary>
    public static ImmutableArray<string> DropHost(ImmutableArray<string> urls, string host) => [.. urls.Where(url => ReadHost(url) != host)];

    /// <summary>
    /// After a failed answer: a 400, 403, 404 or 410 is a link gone for good, and the last attempt gives up too; else
    /// wait what the answer's <c>Retry-After</c> asks, or 3, 6, 12… seconds, at most a minute.
    /// </summary>
    public static ImageRetry DecideAfterAnswer(int status, int attempt, Optional<string> retryAfter) =>
        IsGone(status) || attempt >= Attempts
            ? new ImageRetry.GiveUp()
            : new ImageRetry.TryAgain(Math.Min(
                retryAfter is Optional<string>.Some wait && wait.Value.Length > 0 && wait.Value.All(char.IsAsciiDigit) && int.TryParse(wait.Value, out var seconds)
                    ? seconds
                    : 3 * (1 << (attempt - 1)),
                60));

    /// <summary>After a request that got no answer: wait 2, 4, 6… seconds, until the last attempt.</summary>
    public static ImageRetry DecideAfterError(int attempt) =>
        attempt < Attempts ? new ImageRetry.TryAgain(2 * attempt) : new ImageRetry.GiveUp();

    /// <summary>An error that says the host never answered (not an HTTP status): its other images are skipped.</summary>
    public static bool IsHostUnreachable(string error) => !error.StartsWith("HTTP", StringComparison.Ordinal);

    public static string ReadHost(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Authority : "";

    /// <summary><c>map.json</c>: each link → its file, in the order they were saved, on one line.</summary>
    public static string WriteImageMap(IEnumerable<(string Url, string File)> saved) =>
        JsonWriting.WriteObject(saved, asciiOnly: true, indented: false);

    public static Result<ImmutableArray<(string Url, string File)>, string> ReadImageMap(string json) =>
        MetaReading.ReadStringMembers(json, "images/map.json").Map(entries => entries.Select(entry => (entry.Key, entry.Value)).ToImmutableArray());

    /// <summary><c>failed.txt</c>: one line per image given up, its error, a tab character, then its link.</summary>
    public static string WriteFailedList(IEnumerable<FailedImage> failed) =>
        string.Join('\n', failed.Select(image => $"{image.Error}\t{image.Url}"));

    /// <summary>The links failed.txt records as gone for good (400, 403, 404, 410): the viewer leaves them out.</summary>
    public static ImmutableHashSet<string> ReadGoneImages(string failedList) =>
        [.. failedList.Split('\n').Select(line => line.TrimEnd('\r').Split('\t', 2)).Where(parts => CreateGoneErrorPattern().IsMatch(parts[0])).Select(parts => parts.Length > 1 ? parts[1] : "")];

    /// <summary>The failures counted by host and kind of error, the most first: <c>"   12  host  HTTP 404 text/html"</c>.</summary>
    public static ImmutableArray<string> SummarizeFailures(ImmutableArray<FailedImage> failed) =>
        [.. failed.Select(image => (Host: ReadHost(image.Url), Kind: TextTrimming.TakeCharacters(image.Error.Split(':')[0], 40)))
            .GroupBy(key => key)
            .Select((group, first) => (group.Key, Count: group.Count(), First: first))
            .OrderByDescending(group => group.Count).ThenBy(group => group.First)
            .Select(group => $"    {group.Count,5}  {group.Key.Host,-28} {group.Key.Kind}")];

    private static bool IsGone(int status) => status is 400 or 403 or 404 or 410;

    private static string ToExtension(string mediaType) => mediaType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/webp" => "webp",
        "image/svg+xml" => "svg",
        "image/avif" => "avif",
        _ => "img",
    };

    /// <summary>An attribute's text with its entities decoded, read the way a browser reads it.</summary>
    private static string DecodeEntities(string attribute) =>
        HtmlReading.ParseHtml($"<img src=\"{attribute}\">").QuerySelector("img")!.GetAttribute("src")!;

    [GeneratedRegex("<img[^>]+src=\"(https?://[^\"]+)\"")]
    private static partial Regex CreateImageSourcePattern();

    [GeneratedRegex(@"^HTTP (400|403|404|410)\b")]
    private static partial Regex CreateGoneErrorPattern();
}
