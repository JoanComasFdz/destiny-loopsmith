using System.Text.RegularExpressions;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>The sheet's id and pages, and the names of the files and folder a snapshot writes.</summary>
public static partial class SheetNaming
{
    /// <summary>A sheet's URL → its id (<c>…/d/&lt;id&gt;/edit</c>); anything else is taken as the id itself.</summary>
    public static string ReadSheetId(string sheet)
    {
        var found = SheetIdPattern().Match(sheet);
        return found.Success ? found.Groups[1].Value : sheet;
    }

    public static string BuildSheetBase(string sheetId) => $"https://docs.google.com/spreadsheets/d/{sheetId}";

    /// <summary>The page listing the tabs.</summary>
    public static string BuildTabListUrl(string sheetId) => $"{BuildSheetBase(sheetId)}/htmlview";

    /// <summary>One tab, rendered as an HTML table.</summary>
    public static string BuildTabUrl(string sheetId, string gid) => $"{BuildSheetBase(sheetId)}/htmlview/sheet?headers=false&gid={gid}";

    /// <summary>One tab as Google's CSV export.</summary>
    public static string BuildGvizUrl(string sheetId, string gid) => $"{BuildSheetBase(sheetId)}/gviz/tq?tqx=out%3Acsv&gid={gid}";

    public static string BuildEditUrl(string sheetId) => $"{BuildSheetBase(sheetId)}/edit";

    /// <summary>A name made safe for a file: letters, digits, <c>_</c>, <c>-</c> and spaces kept, spaces as <c>_</c>, at most 60 characters.</summary>
    public static string ToSlug(string name)
    {
        var kept = PythonText.StripWhitespace(UnsafeCharacters().Replace(name, "")).Replace(' ', '_');
        var slug = PythonText.TakeCharacters(kept, 60);
        return slug.Length > 0 ? slug : "tab";
    }

    /// <summary>A tab's files' name without extension: its place in the sheet, then its name (<c>06_Arc</c>).</summary>
    public static string ToStem(int place, string name) => $"{place:00}_{ToSlug(name)}";

    /// <summary>The folder a download writes when it isn't given one: <c>&lt;title&gt;_&lt;YYYY-MM-DD_HHMM&gt;</c>.</summary>
    public static string NameDefaultFolder(string title, DateTime now) => $"{ToSlug(title).ToLowerInvariant()}_{now:yyyy-MM-dd_HHmm}";

    [GeneratedRegex("/d/([a-zA-Z0-9_-]+)")]
    private static partial Regex SheetIdPattern();

    /// <summary>Python's <c>[^\w\- ]+</c>: <c>\w</c> there is a letter, a digit of any kind or <c>_</c>.</summary>
    [GeneratedRegex(@"[^\p{L}\p{N}_\- ]+")]
    private static partial Regex UnsafeCharacters();
}
