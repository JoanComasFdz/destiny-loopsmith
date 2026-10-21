using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A tab's grid → its Markdown and CSV, as the Python version wrote them.</summary>
public sealed class TabWritingTests
{
    private static readonly ImmutableArray<ImmutableArray<string>> Rows =
        [["Name", "Note"], ["Arc | Void", "line one\nline two"], ["Say \"hi\", twice", ""]];

    [Fact]
    public void The_markdown_is_a_heading_then_a_table_with_pipes_escaped_and_line_breaks_as_br() =>
        Assert.Equal(
            "## Arc\n\n| Name | Note |\n|---|---|\n| Arc \\| Void | line one<br>line two |\n| Say \"hi\", twice |  |\n",
            TabWriting.WriteMarkdown("Arc", Rows));

    [Fact]
    public void An_empty_tab_says_so() =>
        Assert.Equal("## Arc\n\n_(empty)_\n", TabWriting.WriteMarkdown("Arc", []));

    [Fact]
    public void The_csv_quotes_only_what_needs_it_and_ends_each_row_with_CRLF() =>
        Assert.Equal(
            "Name,Note\r\nArc | Void,\"line one\nline two\"\r\n\"Say \"\"hi\"\", twice\",\r\n",
            TabWriting.WriteCsv(Rows));

    [Fact]
    public void A_row_of_one_empty_field_is_written_as_two_quotes() =>
        Assert.Equal("\"\"\r\n", TabWriting.WriteCsv([[""]]));

    [Theory]
    [InlineData("", 0)]
    [InlineData("a,b\r\n", 1)]
    [InlineData("a\r\n\"x\ny\",z\r\nlast", 3)]
    [InlineData("a\n\nb\n", 3)]
    public void A_csvs_rows_are_counted_as_Pythons_reader_counts_them(string csv, int rows) =>
        Assert.Equal(rows, TabWriting.CountCsvRows(csv));

    [Fact]
    public void A_written_csv_counts_its_own_rows() =>
        Assert.Equal(3, TabWriting.CountCsvRows(TabWriting.WriteCsv(Rows)));
}
