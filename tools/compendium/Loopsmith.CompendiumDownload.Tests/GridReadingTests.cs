using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A tab's table → its grid of cell texts, as BeautifulSoup read it in the Python version.</summary>
public sealed class GridReadingTests
{
    [Fact]
    public void Merged_cells_keep_their_text_in_the_top_left_and_leave_the_cells_they_cover_empty()
    {
        var grid = ReadGrid("""
            <tr><td rowspan="2">A</td><td colspan="2">B</td></tr>
            <tr><td>C</td><td>D</td></tr>
            """);

        Assert.Equal([["A", "B", ""], ["", "C", "D"]], ToLists(grid));
    }

    [Fact]
    public void Googles_row_numbers_and_column_letters_are_dropped()
    {
        var grid = ReadGrid("""
            <thead><tr><th></th><th>A</th><th>B</th></tr></thead>
            <tbody><tr><th id="0R0">1</th><td>x</td><td>y</td></tr></tbody>
            """);

        Assert.Equal([["x", "y"]], ToLists(grid));
    }

    [Fact]
    public void Empty_rows_and_columns_are_removed()
    {
        var grid = ReadGrid("""
            <tr><td>a</td><td></td><td>b</td></tr>
            <tr><td> </td><td></td><td></td></tr>
            <tr><td></td><td></td><td>c</td></tr>
            """);

        Assert.Equal([["a", "b"], ["", "c"]], ToLists(grid));
    }

    [Fact]
    public void A_line_break_is_kept_and_the_cell_trimmed()
    {
        var grid = ReadGrid("<tr><td>\u00a0 One<br>Two <b>bold</b>\n</td></tr>");

        Assert.Equal([["One\nTwo bold"]], ToLists(grid));
    }

    [Fact]
    public void Text_of_spaces_alone_is_one_space_as_BeautifulSoup_stores_it()
    {
        var grid = ReadGrid("<tr><td>Strand ⯁<span>  </span>Bow</td><td>a<span>\n  </span>b</td></tr>");

        Assert.Equal([["Strand ⯁ Bow", "a\nb"]], ToLists(grid));
    }

    [Fact]
    public void A_scripts_a_styles_and_a_comments_text_is_not_a_cells()
    {
        var grid = ReadGrid("<tr><td>a<script>var x;</script><style>.s0{}</style><!-- note -->b</td></tr>");

        Assert.Equal([["ab"]], ToLists(grid));
    }

    [Fact]
    public void The_sheet_table_is_the_waffle_one()
    {
        var page = HtmlReading.ParseHtml("<table><tr><td>menu</td></tr></table><table class=\"waffle\"><tr><td>data</td></tr></table>");

        Assert.Equal([["data"]], ToLists(GridReading.ReadGrid(HtmlReading.FindSheetTable(page))));
    }

    [Fact]
    public void No_table_is_an_empty_grid() =>
        Assert.Empty(GridReading.ReadGrid(HtmlReading.FindSheetTable(HtmlReading.ParseHtml("<p>nothing</p>"))));

    private static ImmutableArray<ImmutableArray<string>> ReadGrid(string rows) =>
        GridReading.ReadGrid(HtmlReading.FindSheetTable(HtmlReading.ParseHtml($"<table class=\"waffle\">{rows}</table>")));

    private static List<List<string>> ToLists(ImmutableArray<ImmutableArray<string>> grid) => [.. grid.Select(row => row.ToList())];
}
