using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Every bungie.net request a scenario's context makes, answered here, so no scenario reaches bungie.net except to fill
/// the local cache once:
/// - the manifest's index gets an error, so the app uses its pinned <c>ManifestContentId</c> (wwwroot/appsettings.json)
///   and every run reads the same manifest version;
/// - Bungie's static files (<c>/common/destiny2_content/…</c>: the manifest's components, and the icons the app shows)
///   come from the local cache (<see cref="E2ESite.ManifestCache"/>), keyed by their URL, which holds the version, served
///   by <see cref="ManifestCacheServer"/> (the request is redirected there). A file missing from the cache is fetched
///   from bungie.net once, saved and served; parallel scenarios wait for that one download;
/// - any other bungie.net request is refused.
/// What leaves for bungie.net, and what is refused, is logged.
/// </summary>
public static partial class BungieRouting
{
    private const string IndexPath = "/Platform/Destiny2/Manifest/";
    private const string StaticFolder = "/common/destiny2_content/";

    private static readonly HttpClient Bungie = new() { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> Downloads = new();
    private static readonly Lock LogGate = new();

    [GeneratedRegex(@"^https?://([a-z0-9-]+\.)?bungie\.net(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BungieUrl();

    /// <summary>Routes the context's bungie.net requests (<paramref name="scenario"/> names who asked, in the log).</summary>
    public static Task RouteAsync(IBrowserContext context, string scenario) =>
        context.RouteAsync(BungieUrl(), route => AnswerAsync(route, scenario));

    private static async Task AnswerAsync(IRoute route, string scenario)
    {
        var url = new Uri(route.Request.Url);
        if (route.Request.Method == "OPTIONS")
        {
            await route.FulfillAsync(new() { Status = 204, Headers = CorsHeaders });
        }
        else if (url.AbsolutePath.StartsWith(IndexPath, StringComparison.OrdinalIgnoreCase))
        {
            await route.FulfillAsync(new() { Status = 503, Headers = CorsHeaders, ContentType = "application/json", Body = "{}" });
        }
        else if (route.Request.Method == "GET" && url.AbsolutePath.StartsWith(StaticFolder, StringComparison.OrdinalIgnoreCase))
        {
            var download = Downloads.GetOrAdd(url.AbsoluteUri, key => new Lazy<Task<string>>(() => ReadCachedAsync(new Uri(key), scenario)));
            try
            {
                var file = await download.Value;
                var local = new Dictionary<string, string>(CorsHeaders) { ["Location"] = ManifestCacheServer.ToLocalUrl(file) };
                await route.FulfillAsync(new() { Status = 307, Headers = local });
            }
            catch (HttpRequestException failed)
            {
                Downloads.TryRemove(new(url.AbsoluteUri, download));
                Log($"failed to fetch {url.AbsoluteUri}: {failed.Message} ({scenario})");
                await route.AbortAsync("failed");
            }
        }
        else
        {
            Log($"refused {route.Request.Method} {url.AbsoluteUri} ({scenario})");
            await route.AbortAsync("blockedbyclient");
        }
    }

    // The cached copy of a static file, fetched from bungie.net and saved first when the cache lacks it.
    private static async Task<string> ReadCachedAsync(Uri url, string scenario)
    {
        var file = Path.Combine(E2ESite.ManifestCache, url.Host, url.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(file))
        {
            return file;
        }

        Log($"fetched {url.AbsoluteUri} from bungie.net into the cache ({scenario})");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var partial = $"{file}.{Guid.NewGuid():N}.part";
        using (var response = await Bungie.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync();
            await using var saved = File.Create(partial);
            await body.CopyToAsync(saved);
        }

        File.Move(partial, file, overwrite: true);
        return file;
    }

    private static readonly Dictionary<string, string> CorsHeaders = new()
    {
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Headers"] = "*",
        ["Access-Control-Allow-Methods"] = "GET, OPTIONS",
    };

    /// <summary>A cached file's content type, from its extension.</summary>
    public static string ToContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream",
    };

    private static void Log(string line)
    {
        var stamped = $"{DateTimeOffset.UtcNow:O} {line}";
        Console.Error.WriteLine($"[bungie.net] {stamped}");
        TestContext.Current.SendDiagnosticMessage($"[bungie.net] {stamped}");
        if (E2ESite.BungieLog is { } log)
        {
            lock (LogGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, stamped + Environment.NewLine);
            }
        }
    }
}
