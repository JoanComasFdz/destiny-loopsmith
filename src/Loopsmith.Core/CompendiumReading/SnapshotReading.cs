using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// A snapshot's tab files (<c>snapshots/compendium/&lt;date&gt;/NN_&lt;Tab&gt;.csv</c>, as SourceFetching read them) →
/// the entries of every tab this slice has a parser for. Each tab has its own layout, so each has its own parser; the
/// <see cref="ParseTab"/> switch is the registry. An archived <c>OLD …</c> tab is never a source and never read.
/// </summary>
public static partial class SnapshotReading
{
    public static CompendiumSnapshot ReadSnapshot(ImmutableArray<SourceText> files)
    {
        var tabs = files
            .Select(ToSnapshotFile)
            .OfType<Optional<SnapshotFile>.Some>()
            .Select(found => found.Value)
            .Where(file => !file.Tab.StartsWith("OLD ", StringComparison.Ordinal))
            .Select(file => (file.Tab, Entries: ParseTab(file)))
            .ToImmutableArray();
        return new CompendiumSnapshot(
            [.. tabs.SelectMany(tab => tab.Entries.Match(some => some.Value, _ => []))],
            [.. tabs.Where(tab => tab.Entries.IsSome()).Select(tab => tab.Tab).Order(StringComparer.Ordinal)],
            [.. tabs.Where(tab => !tab.Entries.IsSome()).Select(tab => tab.Tab).Order(StringComparer.Ordinal)]);
    }

    /// <summary><c>06_Arc.csv</c> → Arc, <c>13_Class_Abilities.csv</c> → Class Abilities; anything else is no tab.</summary>
    public static Optional<SnapshotFile> ToSnapshotFile(SourceText file)
    {
        var name = file.Path[(file.Path.LastIndexOfAny(['/', '\\']) + 1)..];
        var match = TabFileName().Match(name);
        return match.Success
            ? Optional.Some(new SnapshotFile(match.Groups["tab"].Value.Replace('_', ' '), file))
            : Optional.None<SnapshotFile>();
    }

    /// <summary>The registry of tab parsers: a tab without one is read as no entries (its citations aren't checked).</summary>
    public static Optional<ImmutableArray<CompendiumEntry>> ParseTab(SnapshotFile file)
    {
        var rows = TabRows.ReadTabRows(file.Csv.Text);
        return file.Tab switch
        {
            "Arc" or "Solar" or "Void" or "Stasis" or "Strand" or "Prismatic" or "Class Abilities" =>
                Optional.Some(SubclassTabParsing.ParseSubclassTab(file.Tab, rows)),
            "Armor Mods" => Optional.Some(ArmorModsParsing.ParseArmorModsTab(file.Tab, rows)),
            "Artifact Perks" => Optional.Some(ArtifactPerksParsing.ParseArtifactPerksTab(file.Tab, rows)),
            "Weapon Perks" => Optional.Some(WeaponPerksParsing.ParseWeaponPerksTab(file.Tab, rows)),
            _ => Optional.None<ImmutableArray<CompendiumEntry>>(),
        };
    }

    [GeneratedRegex(@"^\d+_(?<tab>.+)\.csv$")]
    private static partial Regex TabFileName();
}
