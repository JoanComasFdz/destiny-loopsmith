using System.Collections.Immutable;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>
/// One entry of a Compendium tab: an ability, aspect, fragment, mod, perk or glossary keyword, by the name the tab gives
/// it. <see cref="Rows"/> are the tab's rows it spans (an artifact perk's name row and description row), as a rule cites
/// them (<c>compendium/&lt;date&gt;/&lt;tab&gt;#&lt;row&gt;</c>); <see cref="Section"/> the tab's section heading above it
/// ("Fragments", "Weapon Mods", an artifact's name). <see cref="Numbers"/> are the numbers its text states, never the text.
/// </summary>
public sealed record CompendiumEntry(string Tab, ImmutableArray<int> Rows, string Section, string Name, StatedNumbers Numbers);

/// <summary>
/// The numbers a Compendium text states: <see cref="Pve"/> those that hold in PvE (a percent also as its fraction: 15% is
/// 15 and 0.15), <see cref="All"/> those plus the PvP values in brackets ("25% [10%]").
/// </summary>
public sealed record StatedNumbers(ImmutableArray<decimal> Pve, ImmutableArray<decimal> All);

/// <summary>
/// The tabs of a snapshot this slice reads (<see cref="Entries"/>) and the names of the tabs it has no parser for
/// (<see cref="UnreadTabs"/>): a citation into one of those is not checked.
/// </summary>
public sealed record CompendiumSnapshot(ImmutableArray<CompendiumEntry> Entries, ImmutableArray<string> ReadTabs, ImmutableArray<string> UnreadTabs);

/// <summary>A snapshot file's tab name: <c>06_Arc.csv</c> → <c>Arc</c>, <c>13_Class_Abilities.csv</c> → <c>Class Abilities</c>.</summary>
public sealed record SnapshotFile(string Tab, SourceText Csv);
