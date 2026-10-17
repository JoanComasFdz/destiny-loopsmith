using System.Collections.Immutable;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// The Weapon Perks tab is one list: a section heading alone in the first column ("Weapon Traits", "Weapon Mods", "Origin
/// Traits"), then one row per perk, its name in the first column and its description in the second. An enhanced
/// trait's or mod's values share its row after "↑" ("+30 ↑+40"), so both are read. A long text alone in the first column is
/// a note, not a heading.
/// </summary>
public static class WeaponPerksParsing
{
    private const int LongestHeading = 40;

    public static ImmutableArray<CompendiumEntry> ParseWeaponPerksTab(string tab, ImmutableArray<TabRow> rows) =>
        [.. rows
            .Aggregate(
                (Section: "", Entries: ImmutableArray<CompendiumEntry>.Empty),
                (read, row) => row.FindFilledColumns() switch
                {
                    [0] when row.Cells[0].Length <= LongestHeading => (row.Cells[0], read.Entries),
                    [0, 1] => (read.Section, read.Entries.Add(new CompendiumEntry(
                        tab,
                        [row.Number],
                        read.Section,
                        TabRows.ReadFirstLine(row.Cells[0]),
                        NumberReading.ReadNumbers(row.Cells[1], BracketMeaning.PvpValue)))),
                    _ => read,
                })
            .Entries];
}
