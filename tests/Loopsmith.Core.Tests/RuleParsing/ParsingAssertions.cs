using Loopsmith.Core.Functional;
using Xunit.Sdk;

namespace Loopsmith.Core.Tests.RuleParsing;

/// <summary>Unwraps a parse result in a test, failing with the parser's own message.</summary>
internal static class ParsingAssertions
{
    internal static T AssertOk<T>(Result<T, string> result) =>
        result.Match(
            ok => ok.Value,
            error => throw new XunitException($"Expected a successful parse, but got:\n{error.Failure}"));

    internal static string AssertError<T>(Result<T, string> result) =>
        result.Match(
            ok => throw new XunitException($"Expected a parse error, but got: {ok.Value}"),
            error => error.Failure);
}
