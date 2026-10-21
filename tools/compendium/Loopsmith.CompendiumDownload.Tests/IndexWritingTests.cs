using Loopsmith.CompendiumDownload.Core;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Tests;

/// <summary>A snapshot → its INDEX.md: one row per tab with its file, rows, tokens and in-sheet sections.</summary>
public sealed class IndexWritingTests
{
    private static readonly SheetMeta Meta = new("Compendium", "abc", "https://docs.google.com/spreadsheets/d/abc/edit", "2026-10-21", "2026-10-21 09:07");

    [Fact]
    public void Each_tab_is_a_row_with_its_file_rows_tokens_and_sections()
    {
        var raw = """
            <table class="waffle"><tr><td><a href="#rangeid=1">Fragments</a></td><td><a href="#rangeid=2"> Aspects | <b>new</b> </a></td>
            <td><a href="#rangeid=3">Go back to the top</a></td><td><a href="#rangeid=4">Fragments</a></td>
            <td><a href="#gid=5">Other tab</a></td></tr></table>
            """;
        SnapshotTab[] tabs =
        [
            new(1, new SheetTab("Arc", "618"), Optional.Some(12_345L), Optional.Some("a,b\r\nc,d\r\n"), Optional.Some(raw)),
            new(2, new SheetTab("OLD Void", "9"), Optional.None<long>(), Optional.None<string>(), Optional.None<string>()),
        ];

        var index = IndexWriting.WriteIndex(Meta, [.. tabs]);

        Assert.Equal(
            """
            # Compendium — index (2026-10-21 09:07)

            Source: https://docs.google.com/spreadsheets/d/abc/edit  
            Extracted: 2026-10-21 09:07

            One row per tab of the spreadsheet, in sheet order. Open only the file(s) that cover the topic; each `.md` has a `.csv` twin with the same data. Tabs whose name starts with OLD are archived, outdated content. Token counts are rough (characters / 4). See README.md for the folder layout.

            | # | Tab | File | Rows | ~Tokens | Sections |
            |---|---|---|---|---|---|
            | 1 | Arc | [01_Arc.md](01_Arc.md) | 2 | 3.1k | Fragments, Aspects / new |
            | 2 | OLD Void | [02_OLD_Void.md](02_OLD_Void.md) | 0 | 0k |  |

            """,
            index);
    }

    [Fact]
    public void A_sheet_without_a_link_has_no_source_line() =>
        Assert.DoesNotContain("Source:", IndexWriting.WriteIndex(Meta with { Url = "" }, []));

    [Fact]
    public void A_section_longer_than_40_characters_is_not_one() =>
        Assert.Empty(IndexWriting.ReadSections(HtmlReading.FindSheetTable(HtmlReading.ParseHtml($"<table><tr><td><a href=\"#rangeid=1\">{new string('x', 41)}</a></td></tr></table>"))));
}
