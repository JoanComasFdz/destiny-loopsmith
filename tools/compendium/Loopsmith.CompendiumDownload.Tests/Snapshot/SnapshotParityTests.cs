using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests.Snapshot;

/// <summary>
/// A downloaded snapshot (<c>LOOPSMITH_COMPENDIUM_DIR</c>, any snapshot, the Python version's too) against what this tool
/// makes of the pages it kept: each tab's <c>.md</c> and <c>.csv</c> from its <c>raw/</c> page, and INDEX.md, byte for byte.
/// Skipped without a snapshot (CI never has one: licensing).
/// </summary>
public sealed class SnapshotParityTests
{
    [Fact]
    public void Each_tabs_files_are_what_its_raw_page_makes()
    {
        var folder = ReadSnapshotFolder();   // impure
        var tabs = ReadTabs(folder);   // impure

        var differing = tabs.Where(tab => tab.RawHtml is Optional<string>.Some).Select(tab => CompareTab(folder, tab)).Where(found => found.Length > 0).ToImmutableArray();   // impure

        Assert.True(differing.IsEmpty, string.Join("\n", differing));
    }

    [Fact]
    public void The_index_is_what_the_snapshot_makes()
    {
        var folder = ReadSnapshotFolder();   // impure
        var tabs = ReadTabs(folder);   // impure
        var metaJson = File.ReadAllText(Path.Combine(folder, "meta.json"));   // impure
        var expected = File.ReadAllText(Path.Combine(folder, "INDEX.md"));   // impure

        var meta = ((Result<SheetMeta, string>.Ok)MetaReading.ReadMetaJson(metaJson)).Value;   // pure
        var index = IndexWriting.WriteIndex(meta, tabs);   // pure

        Assert.Equal(expected, index);
    }

    private static string CompareTab(string folder, SnapshotTab tab)
    {
        var stem = SheetNaming.ToStem(tab.Place, tab.Tab.Name);   // pure
        var markdown = File.ReadAllText(Path.Combine(folder, $"{stem}.md"));   // impure
        var csv = File.ReadAllText(Path.Combine(folder, $"{stem}.csv"));   // impure

        var page = HtmlReading.ParseHtml(((Optional<string>.Some)tab.RawHtml).Value);   // pure
        var rows = GridReading.ReadGrid(HtmlReading.FindSheetTable(page));   // pure

        var wrongMarkdown = TabWriting.WriteMarkdown(tab.Tab.Name, rows) == markdown ? "" : $"{stem}.md differs. ";   // pure
        var wrongCsv = TabWriting.WriteCsv(rows) == csv ? "" : $"{stem}.csv differs.";   // pure
        return wrongMarkdown + wrongCsv;
    }

    private static string ReadSnapshotFolder()
    {
        var folder = Environment.GetEnvironmentVariable("LOOPSMITH_COMPENDIUM_DIR");   // impure
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "Set LOOPSMITH_COMPENDIUM_DIR to a downloaded Compendium snapshot (tools/compendium/README.md) to run it.");   // impure
        return folder!;
    }

    private static ImmutableArray<SnapshotTab> ReadTabs(string folder)
    {
        var tabsJson = File.ReadAllText(Path.Combine(folder, "tabs.json"));   // impure
        var tabs = ((Result<ImmutableArray<SheetTab>, string>.Ok)MetaReading.ReadTabsJson(tabsJson)).Value;   // pure
        return [.. tabs.Select((tab, at) => ReadTab(folder, at + 1, tab))];   // impure
    }

    private static SnapshotTab ReadTab(string folder, int place, SheetTab tab)
    {
        var stem = SheetNaming.ToStem(place, tab.Name);   // pure
        var markdown = new FileInfo(Path.Combine(folder, $"{stem}.md"));   // impure
        var csv = Path.Combine(folder, $"{stem}.csv");   // pure
        var raw = Path.Combine(folder, "raw", $"{stem}.html");   // pure
        return new SnapshotTab(
            place,
            tab,
            markdown.Exists ? Optional.Some(markdown.Length) : Optional.None<long>(),   // impure
            File.Exists(csv) ? Optional.Some(File.ReadAllText(csv)) : Optional.None<string>(),   // impure
            File.Exists(raw) ? Optional.Some(File.ReadAllText(raw)) : Optional.None<string>());   // impure
    }
}
