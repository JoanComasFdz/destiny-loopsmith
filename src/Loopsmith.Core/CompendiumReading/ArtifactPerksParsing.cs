using System.Collections.Immutable;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// The Artifact Perks tab lists each artifact under a heading ("Tablet of Ruin (Heresy)", alone in its row, with a row of
/// its weapon types under it), then its perks three to a row: a row of names (third, sixth and eighth columns) over a row
/// of their descriptions, one column to the left of each name. An entry spans both rows; rules cite the description row.
/// </summary>
public static class ArtifactPerksParsing
{
    private const int HeadingColumn = 3;
    private static readonly ImmutableArray<int> NameColumns = [2, 5, 7];

    public static ImmutableArray<CompendiumEntry> ParseArtifactPerksTab(string tab, ImmutableArray<TabRow> rows) =>
        [.. Enumerable.Range(0, rows.Length)
            .Aggregate(
                (Artifact: "", Entries: ImmutableArray<CompendiumEntry>.Empty),
                (read, index) => (IsArtifactHeading(rows, index), index + 1 < rows.Length && IsNameRow(rows[index], rows[index + 1])) switch
                {
                    (true, _) => (rows[index].Cells[HeadingColumn], read.Entries),
                    (_, true) => (read.Artifact, read.Entries.AddRange(ToEntries(tab, read.Artifact, rows[index], rows[index + 1]))),
                    _ => read,
                })
            .Entries];

    /// <summary>The first of the heading rows: the artifact's name, not its weapon types under it.</summary>
    private static bool IsArtifactHeading(ImmutableArray<TabRow> rows, int index) =>
        IsHeading(rows[index]) && !(index > 0 && IsHeading(rows[index - 1]));

    private static bool IsHeading(TabRow row) =>
        row.FindFilledColumns() is [HeadingColumn];

    private static bool IsNameRow(TabRow row, TabRow next) =>
        NameColumns.Any(column => row.ReadCell(column).Length > 0 && next.ReadCell(column - 1).Length > 0);

    private static IEnumerable<CompendiumEntry> ToEntries(string tab, string artifact, TabRow names, TabRow descriptions) =>
        NameColumns
            .Where(column => names.ReadCell(column).Length > 0 && descriptions.ReadCell(column - 1).Length > 0)
            .Select(column => new CompendiumEntry(
                tab,
                [names.Number, descriptions.Number],
                artifact,
                TabRows.ReadFirstLine(names.ReadCell(column)),
                NumberReading.ReadNumbers(descriptions.ReadCell(column - 1), BracketMeaning.PvpValue)));
}
