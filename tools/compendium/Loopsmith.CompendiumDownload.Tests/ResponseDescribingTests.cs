using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>An answer's line in download.log: its status, type, size and time, the headers worth keeping, its redirects.</summary>
public sealed class ResponseDescribingTests
{
    [Fact]
    public void An_answer_names_what_it_was_and_where_it_was_sent() =>
        Assert.Equal(
            "HTTP 200 type=text/html; charset=utf-8 bytes=1234 elapsed=0.35s server=GSE redirects=[302 https://accounts.google.com/x] final=https://accounts.google.com/x",
            ResponseDescribing.DescribeAnswer(new AnswerFacts(
                200, Optional.Some("text/html; charset=utf-8"), 1234, TimeSpan.FromMilliseconds(345), [("server", "GSE")],
                [new Redirect(302, "https://accounts.google.com/x")], "https://accounts.google.com/x")));

    [Fact]
    public void An_answer_without_a_type_says_so() =>
        Assert.Equal(
            "HTTP 404 type=- bytes=0 elapsed=1.00s",
            ResponseDescribing.DescribeAnswer(new AnswerFacts(404, Optional.None<string>(), 0, TimeSpan.FromSeconds(1), [], [], "https://x")));
}
