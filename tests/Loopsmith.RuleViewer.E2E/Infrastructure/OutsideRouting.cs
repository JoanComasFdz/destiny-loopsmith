using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;

namespace Loopsmith.RuleViewer.E2E.Infrastructure;

/// <summary>
/// Every request the rules page makes beyond the local server, answered here, so the page looks as it does for a reader
/// (its icons, its fonts) and no run depends on the network:
/// - Bungie's icons (<c>www.bungie.net/common/…</c>, <c>/img/…</c>, named by the fixture's manifest excerpt) and the page's
///   Google Fonts (<c>fonts.googleapis.com</c>'s stylesheet, <c>fonts.gstatic.com</c>'s font files) come from the local
///   cache (<see cref="E2ERuleViewer.PageCache"/>), keyed by their URL. A file missing from it is fetched once, with the
///   browser's own user agent (Google Fonts answers by it), saved and served; parallel scenarios wait for that one download;
/// - any other request is refused (the page logs it to its console, which fails the scenario: BaseE2E).
/// </summary>
public static class OutsideRouting
{
    private static readonly string[] CachedHosts = ["www.bungie.net", "fonts.googleapis.com", "fonts.gstatic.com"];
    private static readonly HttpClient Outside = new() { Timeout = TimeSpan.FromMinutes(2) };
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Downloads = new();

    /// <summary>Routes the context's requests to anywhere but the machine itself.</summary>
    public static Task RouteAsync(IBrowserContext context) =>
        context.RouteAsync(url => new Uri(url).Host is not ("localhost" or "127.0.0.1"), AnswerAsync);

    private static async Task AnswerAsync(IRoute route)
    {
        var url = new Uri(route.Request.Url);
        if (route.Request.Method != "GET" || !CachedHosts.Contains(url.Host))
        {
            await route.AbortAsync("blockedbyclient");
            return;
        }

        var agent = route.Request.Headers.TryGetValue("user-agent", out var sent) ? sent : "";
        var download = Downloads.GetOrAdd(url.AbsoluteUri, key => new Lazy<Task<string>>(() => ReadCachedAsync(new Uri(key), agent)));
        try
        {
            var file = await download.Value;
            await route.FulfillAsync(new()
            {
                Status = 200,
                Path = file,
                ContentType = ToContentType(url),
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            });
        }
        catch (HttpRequestException failed)
        {
            Downloads.TryRemove(new(url.AbsoluteUri, download));
            TestContext.Current.SendDiagnosticMessage($"failed to fetch {url.AbsoluteUri}: {failed.Message}");
            await route.AbortAsync("failed");
        }
    }

    // The cached copy of a file, fetched and saved first when the cache lacks it. A URL with a query (Google Fonts'
    // stylesheet) is saved under its path and a hash of its query.
    private static async Task<string> ReadCachedAsync(Uri url, string agent)
    {
        var name = url.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        if (url.Query.Length > 0)
        {
            name += "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.Query)))[..16].ToLowerInvariant();
        }

        var file = Path.Combine(E2ERuleViewer.PageCache, url.Host, name);
        if (File.Exists(file))
        {
            return file;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var partial = $"{file}.{Guid.NewGuid():N}.part";
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", agent);
            using var response = await Outside.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync();
            await using var saved = File.Create(partial);
            await body.CopyToAsync(saved);
        }

        File.Move(partial, file, overwrite: true);
        return file;
    }

    private static string ToContentType(Uri url) =>
        url.Host == "fonts.googleapis.com"
            ? "text/css"
            : Path.GetExtension(url.AbsolutePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".woff2" => "font/woff2",
                ".woff" => "font/woff",
                ".ttf" => "font/ttf",
                _ => "application/octet-stream",
            };
}
