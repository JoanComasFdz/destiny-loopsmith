using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// The Armor Mods tab is a grid: its first row names the columns (Helmet, Effect, Arms, Effect, …, then raid and activity
/// mods), and every row after holds one mod per name column, its name and energy cost ("Siphon\n[1–2–3 Energy]") beside its
/// effect. A row number names several mods, so a citation finds its mod by name in the cited row. A name cell without an
/// effect is a raid's or an activity's heading, not a mod.
/// </summary>
public static partial class ArmorModsParsing
{
    private const string EffectHeader = "Effect";

    public static ImmutableArray<CompendiumEntry> ParseArmorModsTab(string tab, ImmutableArray<TabRow> rows)
    {
        var header = rows.IsEmpty ? new TabRow(1, []) : rows[0];
        var nameColumns = Enumerable.Range(0, header.Cells.Length)
            .Where(column => header.ReadCell(column + 1) == EffectHeader && header.Cells[column] is not ("" or EffectHeader))
            .ToImmutableArray();
        return [.. rows.Skip(1).SelectMany(row => nameColumns
            .Where(column => row.ReadCell(column).Length > 0 && row.ReadCell(column + 1).Length > 0)
            .Select(column => ToEntry(tab, row, header.Cells[column], column)))];
    }

    private static CompendiumEntry ToEntry(string tab, TabRow row, string slot, int column)
    {
        var name = ReadModName(row.ReadCell(column));
        var numbers = NumberReading.ReadNumbers(row.ReadCell(column + 1), BracketMeaning.PvpValue);
        return new CompendiumEntry(tab, [row.Number], slot, name, numbers);
    }

    /// <summary>"Melee\n Kickstart\n[3 Energy]" → "Melee Kickstart": the lines before the energy cost.</summary>
    private static string ReadModName(string cell) =>
        string.Join(" ", cell.Split('\n').Select(line => line.Trim()).TakeWhile(line => !CreateEnergyCostPattern().IsMatch(line)).Where(line => line.Length > 0));

    [GeneratedRegex(@"^\[.*Energ")]
    private static partial Regex CreateEnergyCostPattern();
}
