using System.Collections.Immutable;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// The subclass tabs (Arc, Solar, Void, Stasis, Strand, Prismatic) and Class Abilities share one layout: a class band
/// ("Hunter", "Titans"), a section heading in the name column ("Glossary", "Fragments", "Grenade Abilities", "Aspects"),
/// then one row per entry, its name in the second column and its text, cooldown and chunk scalar in the columns after.
/// A row whose only cell is the name column and no heading is a note ("Numbers in [Smaller Parenthesis]…"), not an entry.
/// </summary>
public static class SubclassTabParsing
{
    private const int NameColumn = 1;

    private static readonly ImmutableHashSet<string> ClassBands =
        new[] { "Hunter", "Hunters", "Titan", "Titans", "Warlock", "Warlocks" }.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly ImmutableHashSet<string> SectionHeadings =
        new[] { "Glossary", "Fragments", "Grenade Abilities", "Melee Abilities", "Super Abilities", "Class Abilities", "Aspect", "Aspects" }
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The one section whose <c>[…]</c> is the value with Spark of Magnitude, not the PvP value (its note says so).</summary>
    private static readonly (string Tab, string Section) MagnitudeSection = ("Arc", "Grenade Abilities");

    public static ImmutableArray<CompendiumEntry> ParseSubclassTab(string tab, ImmutableArray<TabRow> rows) =>
        [.. rows
            .Aggregate(
                (Section: "", Entries: ImmutableArray<CompendiumEntry>.Empty),
                (read, row) => ReadRow(tab, row, read.Section) switch
                {
                    SubclassRow.Heading heading => (heading.Section, read.Entries),
                    SubclassRow.Entry entry => (read.Section, read.Entries.Add(entry.Read)),
                    _ => read,
                })
            .Entries];

    private static SubclassRow ReadRow(string tab, TabRow row, string section)
    {
        var filled = row.FindFilledColumns();
        var name = TabRows.ReadFirstLine(row.ReadCell(NameColumn));
        var isBand = filled.Length > 0 && filled.All(column => ClassBands.Contains(row.Cells[column]));
        return (isBand, name, SectionHeadings.Contains(name), filled.Length) switch
        {
            (true, _, _, _) => new SubclassRow.Skipped(),
            (_, "", _, _) => new SubclassRow.Skipped(),
            (_, _, true, _) => new SubclassRow.Heading(name),
            (_, _, _, 1) => new SubclassRow.Skipped(),
            _ => new SubclassRow.Entry(ToEntry(tab, row, section, name, filled)),
        };
    }

    private static CompendiumEntry ToEntry(string tab, TabRow row, string section, string name, ImmutableArray<int> filled)
    {
        var text = string.Join("\n", filled.Where(column => column > NameColumn).Select(column => row.Cells[column]));
        var brackets = (tab, section) == MagnitudeSection ? BracketMeaning.AlternativeValue : BracketMeaning.PvpValue;
        return new CompendiumEntry(tab, [row.Number], section, name, NumberReading.ReadNumbers(text, brackets));
    }
}

/// <summary>What a subclass tab's row is: a section heading, an entry, or nothing to read (a band, a note, a blank).</summary>
[Dunet.Union]
public partial record SubclassRow
{
    partial record Heading(string Section);
    partial record Entry(CompendiumEntry Read);
    partial record Skipped();
}
