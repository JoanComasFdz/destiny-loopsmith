using System.Collections.Immutable;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>A tab's row as the parsers see it: its 1-based number and its cells, trimmed, with "-" placeholders blanked.</summary>
public sealed record TabRow(int Number, ImmutableArray<string> Cells);

/// <summary>Reads a tab's CSV into <see cref="TabRow"/>s, and the cell helpers every tab's parser shares.</summary>
public static class TabRows
{
    public static ImmutableArray<TabRow> ReadTabRows(string csv) =>
        [.. CsvReading.ReadRows(csv).Select((cells, index) => new TabRow(index + 1, [.. cells.Select(ToCellText)]))];

    /// <summary>The cell at <paramref name="column"/>, or "" past the row's end.</summary>
    public static string ReadCell(this TabRow row, int column) =>
        column < row.Cells.Length ? row.Cells[column] : "";

    /// <summary>The columns that hold something.</summary>
    public static ImmutableArray<int> FindFilledColumns(this TabRow row) =>
        [.. Enumerable.Range(0, row.Cells.Length).Where(column => row.Cells[column].Length > 0)];

    /// <summary>A name cell's first line: "Arcbolt Grenade\nHeavy trajectory" → "Arcbolt Grenade".</summary>
    public static string ReadFirstLine(string cell) =>
        cell.Split('\n')[0].Trim();

    private static string ToCellText(string cell)
    {
        var trimmed = cell.Replace("\r", "").Trim();
        return trimmed == "-" ? "" : trimmed;
    }
}
