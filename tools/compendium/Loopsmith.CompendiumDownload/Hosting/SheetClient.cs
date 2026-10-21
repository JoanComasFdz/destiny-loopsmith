using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Text;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Hosting;

/// <summary>What Google answered: its status and type, its body (as bytes and as text) and its facts for the log.</summary>
public sealed record SheetAnswer(int Status, string ContentType, byte[] Body, string Text, Optional<string> RetryAfter, AnswerFacts Facts);

/// <summary>
/// Impure boundary: HTTP requests to Google, as a browser makes them (its user agent, the cookies Google sets for a
/// viewer). Redirects are followed one by one so the log shows them (a redirect to a login page means the sheet isn't public).
/// </summary>
public sealed class SheetClient : IDisposable
{
    private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private readonly HttpClient client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    public SheetClient()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>The answer, whatever its status; an error when none came (no connection, a timeout).</summary>
    public async Task<Result<SheetAnswer, string>> GetAsync(string url, IReadOnlyList<(string Name, string Value)> headers, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        using var cancel = new CancellationTokenSource(timeout);
        var redirects = ImmutableArray.CreateBuilder<Redirect>();
        var at = new Uri(url);
        try
        {
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, at);
                foreach (var (name, value) in headers.Any(header => header.Name == "Accept") ? headers : [.. headers, ("Accept", "*/*")])
                {
                    request.Headers.TryAddWithoutValidation(name, value);
                }

                using var response = await client.SendAsync(request, cancel.Token);
                var status = (int)response.StatusCode;
                if (status is >= 300 and < 400 && response.Headers.Location is { } location && redirects.Count < 30)
                {
                    redirects.Add(new Redirect(status, location.OriginalString));
                    at = location.IsAbsoluteUri ? location : new Uri(at, location);
                    continue;
                }

                var body = await response.Content.ReadAsByteArrayAsync(cancel.Token);
                return new Result<SheetAnswer, string>.Ok(ReadAnswer(response, body, watch.Elapsed, redirects.ToImmutable(), at));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            var reason = exception is TaskCanceledException ? $"timed out after {timeout.TotalSeconds:0}s" : exception.Message;
            return new Result<SheetAnswer, string>.Error($"{exception.GetType().Name}: {reason}");
        }
    }

    public void Dispose() => client.Dispose();

    private static SheetAnswer ReadAnswer(HttpResponseMessage response, byte[] body, TimeSpan elapsed, ImmutableArray<Redirect> redirects, Uri final)
    {
        var contentType = response.Content.Headers.ContentType;
        var headers = ResponseDescribing.LoggedHeaders
            .Select(name => (Name: name, Values: response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values) ? values : null))
            .Where(header => header.Values is not null)
            .Select(header => (header.Name, string.Join(", ", header.Values!)))
            .ToImmutableArray();
        var retryAfter = headers.Where(header => header.Name == "retry-after").Select(header => Optional.Some(header.Item2)).DefaultIfEmpty(Optional.None<string>()).First();
        var encoding = ReadEncoding(contentType?.CharSet);
        var facts = new AnswerFacts(
            (int)response.StatusCode, Optional.FromNullable(contentType?.ToString()), body.LongLength, elapsed, headers, redirects, final.ToString());
        return new SheetAnswer((int)response.StatusCode, contentType?.ToString() ?? "", body, encoding.GetString(body), retryAfter, facts);
    }

    private static Encoding ReadEncoding(string? charSet)
    {
        try
        {
            return string.IsNullOrEmpty(charSet) ? Encoding.UTF8 : Encoding.GetEncoding(charSet.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
