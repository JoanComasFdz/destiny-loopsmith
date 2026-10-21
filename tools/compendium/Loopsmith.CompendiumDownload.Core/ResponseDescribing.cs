using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>A redirect a request followed: its status and where it pointed.</summary>
public sealed record Redirect(int Status, string Location);

/// <summary>
/// What an answer said, for the log: its status, type and size, how long it took, the headers worth keeping
/// (<c>retry-after</c>, <c>x-ratelimit-remaining</c>, <c>server</c>, <c>cache-control</c>), the redirects it went
/// through and where it ended.
/// </summary>
public sealed record AnswerFacts(
    int Status, Optional<string> ContentType, long Bytes, TimeSpan Elapsed, ImmutableArray<(string Name, string Value)> Headers,
    ImmutableArray<Redirect> Redirects, string FinalUrl);

/// <summary>An answer → its line in the log (a redirect to a login page shows up there).</summary>
public static class ResponseDescribing
{
    public static readonly ImmutableArray<string> LoggedHeaders = ["retry-after", "x-ratelimit-remaining", "server", "cache-control"];

    public static string DescribeAnswer(AnswerFacts answer)
    {
        var contentType = answer.ContentType is Optional<string>.Some type ? type.Value : "-";
        var parts = new List<string>
        {
            $"HTTP {answer.Status}", $"type={contentType}", $"bytes={answer.Bytes}",
            $"elapsed={answer.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture)}s",
        };
        parts.AddRange(answer.Headers.Select(header => $"{header.Name}={header.Value}"));
        if (!answer.Redirects.IsEmpty)
        {
            parts.Add($"redirects=[{string.Join(" -> ", answer.Redirects.Select(hop => $"{hop.Status} {hop.Location}"))}] final={answer.FinalUrl}");
        }

        return string.Join(' ', parts);
    }
}
