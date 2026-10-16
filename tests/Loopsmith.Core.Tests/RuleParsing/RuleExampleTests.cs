using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.RuleParsing;
using Loopsmith.Core.Tests.Support;
using Xunit.Sdk;

namespace Loopsmith.Core.Tests.RuleParsing;

/// <summary>
/// The yaml examples of docs/rule-format.md parse, read from the doc itself so the doc and the parser can't drift apart.
/// A block's marker says what it is; a fragment (an element, an element's keys, a rule) is parsed inside the smallest
/// rules file that holds it, against the repo's glossary.
/// </summary>
public sealed partial class RuleExampleTests
{
    private const string SpecPath = "docs/rule-format.md";

    private const string MarkerStart = "<!-- RuleExampleTests reads the next yaml block as ";

    private const string MarkerEnd = " -->";

    private const string StubsPath = "doc-example-stubs.yaml";

    private static readonly ImmutableArray<string> ElementHeader =
    [
        "elements:",
        "  - id: doc-example",
        "    name: Doc example",
        "    kind: weaponPerk",
        "    affinity: neutral",
    ];

    public static TheoryData<int, string> ReadExamples()
    {
        var examples = DocExamples.ReadYamlBlocks(SpecPath);                                                      // impure
        return [.. examples.SelectMany(ToCase)];                                                                  // pure
    }

    [Theory]
    [MemberData(nameof(ReadExamples))]
    public void Parses_the_example_of_the_spec(int line, string shape)
    {
        var examples = DocExamples.ReadYamlBlocks(SpecPath);                                                      // impure
        var example = examples.Single(block => block.FirstLine == line);                                           // pure
        var glossary = ReadRepoGlossary();                                                                         // impure
        var files = ToRuleFiles(example, shape, glossary);                                                         // pure
        var parsed = RuleCatalogParsing.ParseCatalog(files);                                                       // pure
        AssertParses(example, shape, parsed);                                                                      // impure
    }

    /// <summary>A marker misspelt would leave its block unread: the count says how many the spec marks for this class.</summary>
    [Fact]
    public void Reads_every_example_the_spec_marks_for_it() =>
        Assert.Equal(11, ReadExamples().Count);

    private static IEnumerable<TheoryDataRow<int, string>> ToCase(DocExample example)
    {
        var marker = example.Marker.UnwrapOr("");
        var reads = marker.StartsWith(MarkerStart, StringComparison.Ordinal) && marker.EndsWith(MarkerEnd, StringComparison.Ordinal);
        return reads ? [new TheoryDataRow<int, string>(example.FirstLine, marker[MarkerStart.Length..^MarkerEnd.Length])] : [];
    }

    private static SourceText ReadRepoGlossary()
    {
        var file = RepoFiles.ToPath("rules/" + GlossaryParsing.GlossaryPath);                                      // pure
        var text = File.ReadAllText(file);                                                                         // impure
        return new SourceText(GlossaryParsing.GlossaryPath, text);                                                 // pure
    }

    /// <summary>The example in the smallest rules file that holds it, the repo's glossary, and a stub for every element it says it doesn't stack with.</summary>
    private static ImmutableArray<SourceText> ToRuleFiles(DocExample example, string shape, SourceText glossary) =>
        shape switch
        {
            "a glossary" => [DocExamples.ToSourceText(example, GlossaryParsing.GlossaryPath, [], 0)],
            "an elements file" => [glossary, DocExamples.ToSourceText(example), ToStubs(example)],
            "an element" => [glossary, DocExamples.ToSourceText(example, SpecPath, ["elements:"], 2), ToStubs(example)],
            "an element's keys" => [glossary, DocExamples.ToSourceText(example, SpecPath, ElementHeader, 4), ToStubs(example)],
            "a rule" => [glossary, DocExamples.ToSourceText(example, SpecPath, [.. ElementHeader, "    rules:"], 6), ToStubs(example)],
            _ => throw new XunitException($"{DocExamples.ToLocation(example)}: unknown marker '{MarkerStart}{shape}{MarkerEnd}' (a glossary, an elements file, an element, an element's keys or a rule)."),
        };

    /// <summary>The elements named by the example's <c>doesNotStackWith</c>, which the catalog checks exist.</summary>
    private static SourceText ToStubs(DocExample example)
    {
        var ids = example.Lines
            .SelectMany(line => DoesNotStackWith().Matches(line))
            .SelectMany(match => match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var elements = ids.Select(id => $"  - {{ id: {id}, name: {id}, kind: aspect, affinity: neutral }}");
        return new SourceText(StubsPath, string.Join('\n', ["elements:", .. elements]) + (ids.Any() ? "" : " []"));
    }

    private static void AssertParses(DocExample example, string shape, Result<RuleCatalog, string> parsed) =>
        parsed.Match(
            _ => new Unit.Value(),
            error => throw new XunitException($"{DocExamples.ToLocation(example)}, read as {shape}, doesn't parse:\n{error.Failure}"));

    [GeneratedRegex(@"doesNotStackWith:\s*\[([^\]]*)\]")]
    private static partial Regex DoesNotStackWith();
}
