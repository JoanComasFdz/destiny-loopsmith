using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>A snapshot → its <c>README.md</c>: how the folder is organised, which tabs are current and which archived.</summary>
public static partial class ReadmeWriting
{
    public static string WriteReadme(SheetMeta meta, ImmutableArray<SnapshotTab> tabs, string folderName)
    {
        var archived = tabs.Where(tab => IsArchived(tab.Tab.Name)).Select(tab => tab.Place).ToImmutableArray();
        var current = tabs.Select(tab => tab.Place).Where(place => !archived.Contains(place)).ToImmutableArray();
        var bytes = tabs.Sum(tab => tab.MarkdownBytes is Optional<long>.Some size ? size.Value : 0);
        var tokens = PythonText.FormatThousandsToWhole(bytes);
        return $$"""
            # {{meta.Title}} — offline copy ({{meta.ExtractedAt}})

            Snapshot of the public Google Sheet {{meta.Url}}, taken {{meta.ExtractedAt}}.
            It has {{tabs.Length}} tabs. Every tab is kept three ways: as text for LLMs, as a visual copy, and as Google's original HTML.

            ## Where is what

            | What you want | Where it is |
            |---|---|
            | Find which tab covers a topic | `INDEX.md`: one row per tab with its file, size and in-sheet sections |
            | The data, for an LLM or for reading | `NN_<Tab>.md`: one Markdown table per tab (~{{tokens}}k tokens in total) |
            | The same data for spreadsheets/scripts | `NN_<Tab>.csv`: same content as the `.md` with the same number |
            | Browse it like the original sheet | `viewer.html` (open in a browser; keep `images/` next to it) |
            | The pictures and icons | `images/` |
            | Google's original pages (used to rebuild) | `raw/`, `htmlview_raw.html`, `tabs.json`, `meta.json` |
            | Google's own CSV export (secondary) | `gviz/` |
            | What happened during the download | `download.log` |

            ## The tab files

            - `NN` is the tab's position in the sheet, so files sort in the same order as the tabs.
            - Current tabs: {{WritePlaces(current)}}.
            - Archived tabs (name starts with OLD, outdated values): {{WritePlaces(archived)}}.
            - The tables mirror the sheet's visual layout rather than a clean database: section headings,
              navigation cells and notes appear as rows; a merged cell's text sits in its top-left cell
              and the cells it covered are empty; line breaks inside a cell are written as `<br>`.
            - Empty rows and columns are removed.

            ## Using it with an LLM

            1. Give it `INDEX.md` first (it is small).
            2. Then attach only the tab file(s) it points to, rather than everything.
            3. Prefer the `.md` files; the `.csv` files hold the same data for tools that want CSV.

            ## The viewer

            `viewer.html` shows every tab with the sheet's colours, merged cells and frozen header rows.
            Tabs are at the bottom (≡ lists them all, Alt+↑/↓ switches), Ctrl+K searches all tabs,
            and links inside the sheet jump to their tab or section. It needs the `images/` folder
            next to it for pictures; without it, image cells show empty.

            ## Other folders and files

            - `images/`: one file per picture, with hashed names. `map.json` maps each original Google
              link to its file. `failed.txt` lists links Google reports as missing; those cells stay
              empty, as they do in the live sheet.
            - `raw/`: Google's HTML for each tab, the source used to build the `.md`, `.csv` and viewer.
            - `htmlview_raw.html`: Google's tab-list page. It looks blank when opened from disk; that is expected.
            - `gviz/`: Google's CSV export per tab. It can blank out cells in columns that mix numbers
              and text, so the `NN_` files are the reliable version.
            - `tabs.json` / `meta.json`: tab names and IDs, and the sheet's title, link and snapshot time.

            ## Rebuilding

            Run from the root of a clone of the Loopsmith repository (its `tools/compendium/README.md`):

            - Rebuild README, INDEX and viewer without downloading anything:
              `dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- --rebuild <path to {{folderName}}> --no-images`
            - Fresh snapshot (new timestamped folder): `dotnet run --project tools/compendium/Loopsmith.CompendiumDownload -- "{{(meta.Url.Length > 0 ? meta.Url : "<sheet URL>")}}"`

            """;
    }

    /// <summary>A tab whose name starts with the word OLD holds outdated values.</summary>
    public static bool IsArchived(string name) => OldTab().IsMatch(name);

    /// <summary>Places as runs: 1, 2, 3, 5 → "01–03, 05"; none → "none".</summary>
    public static string WritePlaces(ImmutableArray<int> places) =>
        places.IsEmpty
            ? "none"
            : string.Join(", ", SplitRuns(places).Select(run => run.First == run.Last ? $"{run.First:00}" : $"{run.First:00}–{run.Last:00}"));

    private static IEnumerable<(int First, int Last)> SplitRuns(ImmutableArray<int> places)
    {
        var first = places[0];
        for (var at = 0; at < places.Length; at++)
        {
            if (at + 1 == places.Length || places[at + 1] != places[at] + 1)
            {
                yield return (first, places[at]);
                first = at + 1 < places.Length ? places[at + 1] : first;
            }
        }
    }

    [GeneratedRegex(@"^old\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OldTab();
}
