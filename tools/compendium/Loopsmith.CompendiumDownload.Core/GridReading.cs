using System.Collections.Immutable;
using AngleSharp.Dom;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// A tab's table → its grid of cell texts: merged cells spread (their text in the top-left cell, the cells they cover
/// empty), Google's row-number and column-letter cells dropped, and empty rows and columns removed.
/// </summary>
public static class GridReading
{
    public static ImmutableArray<ImmutableArray<string>> ReadGrid(Optional<IElement> table) =>
        table is Optional<IElement>.Some found ? TrimGrid(SpreadCells(found.Value)) : [];

    private static Dictionary<(int Row, int Column), string> SpreadCells(IElement table)
    {
        var grid = new Dictionary<(int Row, int Column), string>();
        var row = 0;
        foreach (var tableRow in table.QuerySelectorAll("tr").Where(tableRow => tableRow.Closest("thead") is null))
        {
            var column = 0;
            foreach (var cell in tableRow.Children.Where(cell => cell.LocalName is "td"))
            {
                while (grid.ContainsKey((row, column)))
                {
                    column++;
                }

                var text = PythonText.StripWhitespace(HtmlReading.ReadText(cell, breaksAsNewlines: true));
                var rowSpan = ReadSpan(cell, "rowspan");
                var columnSpan = ReadSpan(cell, "colspan");
                for (var down = 0; down < rowSpan; down++)
                {
                    for (var across = 0; across < columnSpan; across++)
                    {
                        grid[(row + down, column + across)] = down == 0 && across == 0 ? text : "";
                    }
                }

                column += columnSpan;
            }

            row++;
        }

        return grid;
    }

    private static ImmutableArray<ImmutableArray<string>> TrimGrid(Dictionary<(int Row, int Column), string> grid)
    {
        if (grid.Count == 0)
        {
            return [];
        }

        var rowCount = grid.Keys.Max(key => key.Row) + 1;
        var columnCount = grid.Keys.Max(key => key.Column) + 1;
        var rows = Enumerable.Range(0, rowCount)
            .Select(row => Enumerable.Range(0, columnCount).Select(column => grid.GetValueOrDefault((row, column), "")).ToImmutableArray())
            .Where(cells => cells.Any(cell => cell.Length > 0))
            .ToImmutableArray();
        var kept = Enumerable.Range(0, columnCount).Where(column => rows.Any(cells => cells[column].Length > 0)).ToImmutableArray();
        return [.. rows.Select(cells => kept.Select(column => cells[column]).ToImmutableArray())];
    }

    /// <summary>A cell's <c>rowspan</c> or <c>colspan</c> as written (a 0 covers nothing); 1 when it has none or it isn't a number.</summary>
    private static int ReadSpan(IElement cell, string attribute) =>
        int.TryParse(PythonText.StripWhitespace(cell.GetAttribute(attribute) ?? ""), out var span) ? span : 1;
}
