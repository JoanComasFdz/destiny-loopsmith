using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.Tests.Support;

/// <summary>
/// The ```yaml examples of a doc (docs/rule-format.md, docs/loop-format.md), read from the doc itself so a test checks
/// what the doc shows. A test finds its example through the marker comment on the line right above the fence
/// (<c>&lt;!-- BuildFileParsingTests reads the next yaml block --&gt;</c>).
/// </summary>
public static class DocExamples
{
    private const string Fence = "```yaml";

    /// <summary>The ```yaml block right after <paramref name="marker"/>, which must be the only one of its kind in the doc.</summary>
    public static DocExample ReadYamlBlock(string docPath, string marker)
    {
        var lines = File.ReadAllLines(RepoFiles.ToPath(docPath));                                                 // impure
        var example = FindYamlBlocks(docPath, lines).Where(block => block.Marker == Optional.Some(marker)).ToList(); // pure
        Assert.True(example.Count == 1, $"{docPath}: no single ```yaml block right after '{marker}'.");          // impure
        return example[0];                                                                                         // pure
    }

    /// <summary>Every ```yaml block of the doc, in order, with the comment on the line above its fence, if any.</summary>
    public static ImmutableArray<DocExample> ReadYamlBlocks(string docPath)
    {
        var lines = File.ReadAllLines(RepoFiles.ToPath(docPath));                                                 // impure
        return FindYamlBlocks(docPath, lines);                                                                     // pure
    }

    private static ImmutableArray<DocExample> FindYamlBlocks(string docPath, IReadOnlyList<string> lines) =>
    [
        .. lines
            .Select((line, index) => (Line: line, Index: index))
            .Where(line => line.Line.Trim() == Fence)
            .Select(fence => ToExample(docPath, lines, fence.Index)),
    ];

    private static DocExample ToExample(string docPath, IReadOnlyList<string> lines, int fenceIndex)
    {
        var above = fenceIndex > 0 ? lines[fenceIndex - 1].Trim() : "";
        var marker = above.StartsWith("<!--", StringComparison.Ordinal) ? Optional.Some(above) : Optional.None<string>();
        var block = lines.Skip(fenceIndex + 1).TakeWhile(line => line.Trim() != "```").ToImmutableArray();
        return new DocExample(docPath, fenceIndex + 2, marker, Dedent(block));
    }

    /// <summary>A block indented in a list item (its fence indented too) loses the indentation its first line has.</summary>
    private static ImmutableArray<string> Dedent(ImmutableArray<string> block)
    {
        var indent = block.Where(line => line.Trim().Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
        return [.. block.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart())];
    }
}

/// <summary>A ```yaml block of a doc: its first line's number in the doc (1-based), the marker above its fence, its lines.</summary>
public sealed record DocExample(string DocPath, int FirstLine, Optional<string> Marker, ImmutableArray<string> Lines)
{
    /// <summary>The block as a source of the doc, its lines at the doc's own line numbers, so a parser's error names the doc's line.</summary>
    public SourceText ToSourceText() => ToSourceText(DocPath, [], 0);

    /// <summary>
    /// The block under <paramref name="header"/> (the smallest file that holds a fragment), every block line indented by
    /// <paramref name="indent"/> spaces; blank lines before the header keep the block's lines at the doc's own line numbers.
    /// </summary>
    public SourceText ToSourceText(string path, ImmutableArray<string> header, int indent)
    {
        var padding = Enumerable.Repeat("", Math.Max(0, FirstLine - 1 - header.Length));
        var body = Lines.Select(line => line.Length > 0 ? new string(' ', indent) + line : line);
        return new SourceText(path, string.Join('\n', [.. padding, .. header, .. body]));
    }

    public override string ToString() => $"{DocPath}:{FirstLine}";
}
