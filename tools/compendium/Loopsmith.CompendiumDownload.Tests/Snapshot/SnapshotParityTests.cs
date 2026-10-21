using System.Collections.Immutable;
using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests.Snapshot;

/// <summary>
/// A downloaded snapshot (<c>LOOPSMITH_COMPENDIUM_DIR</c>, any snapshot) against what this tool
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
        var markdowns = tabs.Select(tab => ReadTabFile(folder, tab, ".md")).ToImmutableArray();   // impure

        var differing = tabs.Zip(markdowns)
            .Where(tab => tab.First.RawHtml is Optional<string>.Some)
            .Select(tab => CompareTab(tab.First, tab.Second))
            .Where(found => found.Length > 0)
            .ToImmutableArray();   // pure

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

    /// <summary>"" when the tab's files are what its raw page makes, else which differ.</summary>
    private static string CompareTab(SnapshotTab tab, string markdown)
    {
        var stem = SheetNaming.ToStem(tab.Place, tab.Tab.Name);
        var page = HtmlReading.ParseHtml(((Optional<string>.Some)tab.RawHtml).Value);
        var rows = GridReading.ReadGrid(HtmlReading.FindSheetTable(page));
        var csv = tab.Csv is Optional<string>.Some found ? found.Value : "";
        var wrongMarkdown = TabWriting.WriteMarkdown(tab.Tab.Name, rows) == markdown ? "" : $"{stem}.md differs. ";
        var wrongCsv = TabWriting.WriteCsv(rows) == csv ? "" : $"{stem}.csv differs.";
        return wrongMarkdown + wrongCsv;
    }

    private static string ReadSnapshotFolder()
    {
        var folder = Environment.GetEnvironmentVariable("LOOPSMITH_COMPENDIUM_DIR");   // impure
        Assert.SkipWhen(string.IsNullOrEmpty(folder), "Set LOOPSMITH_COMPENDIUM_DIR to a downloaded Compendium snapshot (tools/compendium/README.md) to run it.");   // impure
        return folder!;
    }

    private static string ReadTabFile(string folder, SnapshotTab tab, string extension)
    {
        var path = Path.Combine(folder, SheetNaming.ToStem(tab.Place, tab.Tab.Name) + extension);   // pure
        return File.Exists(path) ? File.ReadAllText(path) : "";   // impure
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
