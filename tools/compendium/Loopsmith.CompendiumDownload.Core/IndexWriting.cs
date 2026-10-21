using System.Collections.Immutable;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>A snapshot → its <c>INDEX.md</c>: one row per tab with its file, rows, size in tokens and in-sheet sections.</summary>
public static partial class IndexWriting
{
    public static string WriteIndex(SheetMeta meta, ImmutableArray<SnapshotTab> tabs)
    {
        var lines = new List<string> { $"# {meta.Title} — index ({meta.ExtractedAt})", "" };
        if (meta.Url.Length > 0)
        {
            lines.Add($"Source: {meta.Url}  ");
        }

        lines.AddRange(
        [
            $"Extracted: {meta.ExtractedAt}", "",
            "One row per tab of the spreadsheet, in sheet order. Open only the file(s) that cover the topic; each `.md` has a "
                + "`.csv` twin with the same data. Tabs whose name starts with OLD are archived, outdated content. Token counts "
                + "are rough (characters / 4). See README.md for the folder layout.",
            "",
            "| # | Tab | File | Rows | ~Tokens | Sections |", "|---|---|---|---|---|---|",
        ]);
        lines.AddRange(tabs.Select(WriteIndexRow));
        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// The tab's in-sheet sections: the texts of its links to a named range (<c>#rangeid=</c>), at most 40 characters,
    /// without "Go back…", each once.
    /// </summary>
    public static ImmutableArray<string> ReadSections(Optional<IElement> table) =>
        table is not Optional<IElement>.Some found
            ? []
            : [.. found.Value.QuerySelectorAll("a[href^='#rangeid=']")
                .Select(link => string.Join(' ', HtmlReading.ReadStrippedTexts(link)))
                .Where(text => text.Length > 0 && PythonText.CountCharacters(text) <= 40 && !CreateGoBackPattern().IsMatch(text))
                .Distinct()];

    private static string WriteIndexRow(SnapshotTab tab)
    {
        var rows = tab.Csv is Optional<string>.Some csv ? TabWriting.CountCsvRows(csv.Value) : 0;
        var tokens = tab.MarkdownBytes is Optional<long>.Some bytes ? PythonText.FormatThousandsToTenths(bytes.Value) : "0";
        var table = tab.RawHtml.Bind(html => HtmlReading.FindSheetTable(HtmlReading.ParseHtml(html)));
        var sections = string.Join(", ", ReadSections(table)).Replace("|", "/");
        var stem = SheetNaming.ToStem(tab.Place, tab.Tab.Name);
        return $"| {tab.Place} | {tab.Tab.Name} | [{stem}.md]({stem}.md) | {rows} | {tokens}k | {sections} |";
    }

    [GeneratedRegex("^go back", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateGoBackPattern();
}
