using System.Net;
using System.Net.Sockets;

namespace Loopsmith.Web.E2E.Infrastructure;

/// <summary>
/// Serves the local manifest cache over HTTP on 127.0.0.1, for the whole run: <see cref="BungieRouting"/> sends the
/// browser here for Bungie's files, because handing a 200 MB component through Playwright's own channel stalls the page.
/// Each file is served with CORS open, as bungie.net serves it.
/// </summary>
public static class ManifestCacheServer
{
    private static readonly Lazy<string> Started = new(Start);

    /// <summary>The server's address for <paramref name="cachedFile"/>, a file under the cache (starting the server once).</summary>
    public static string ToLocalUrl(string cachedFile) =>
        Started.Value + string.Join('/', Path.GetRelativePath(E2ESite.ManifestCache, cachedFile).Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString));

    private static string Start()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var prefix = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(() => ServeAsync(listener));
        return prefix;
    }

    private static async Task ServeAsync(HttpListener listener)
    {
        while (listener.IsListening)
        {
            var exchange = await listener.GetContextAsync();
            _ = Task.Run(() => AnswerAsync(exchange));
        }
    }

    private static async Task AnswerAsync(HttpListenerContext exchange)
    {
        var response = exchange.Response;
        try
        {
            response.AddHeader("Access-Control-Allow-Origin", "*");
            var relative = Uri.UnescapeDataString(exchange.Request.Url!.AbsolutePath.TrimStart('/'));
            var file = Path.GetFullPath(Path.Combine(E2ESite.ManifestCache, relative));
            if (!file.StartsWith(E2ESite.ManifestCache + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            {
                response.StatusCode = 404;
                return;
            }

            response.ContentType = BungieRouting.ToContentType(file);
            await using var body = File.OpenRead(file);
            response.ContentLength64 = body.Length;
            await body.CopyToAsync(response.OutputStream);
        }
        catch (HttpListenerException)
        {
            // The browser went away mid-file (its context closed): nothing to answer.
        }
        catch (IOException)
        {
            // Same, seen as a broken pipe.
        }
        finally
        {
            response.Close();
        }
    }
}
