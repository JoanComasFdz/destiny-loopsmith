using System.Text.RegularExpressions;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Docs;

/// <summary>
/// Every ```yaml example of the format docs is read by a test (its marker names the test class) or says why no test
/// reads it, so a new example can't go unchecked by accident.
/// </summary>
public sealed partial class DocExampleMarkerTests
{
    private const string Abridged = "<!-- abridged: no test reads the next yaml block -->";

    [Theory]
    [InlineData("docs/rule-format.md")]
    [InlineData("docs/loop-format.md")]
    public void Every_yaml_example_is_read_by_a_test_or_says_why_not(string docPath)
    {
        var examples = DocExamples.ReadYamlBlocks(docPath);                                                        // impure
        var unmarked = examples.Where(example => !IsMarked(example.Marker.UnwrapOr(""))).Select(DocExamples.ToLocation);  // pure

        Assert.True(
            !unmarked.Any(),
            $"Put '<!-- <TestClass> reads the next yaml block -->' or '{Abridged}' on the line above the fence of each of these examples (at their first line): {string.Join(", ", unmarked)}");
    }

    private static bool IsMarked(string marker) =>
        marker == Abridged || ReadByATest().IsMatch(marker);

    [GeneratedRegex(@"^<!-- [A-Z]\w*Tests reads the next yaml block( as [^>]+)? -->$")]
    private static partial Regex ReadByATest();
}
