using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>A tab of the sheet: its name and Google's id for it.</summary>
public sealed record SheetTab(string Name, string Gid);

/// <summary>The tab-list page (<c>/htmlview</c>) → the sheet's title and its tabs, in sheet order.</summary>
public static partial class TabListing
{
    /// <summary>
    /// The tabs, from the page's script (<c>items.push({name: "Tab", …, gid: "123"</c>), or, on an older page, its menu
    /// (<c>&lt;li id="sheet-button-123"&gt;</c>), or else every <c>switchToSheet('123')</c> it calls, named by gid.
    /// </summary>
    public static ImmutableArray<SheetTab> ListTabs(string html, IDocument page)
    {
        var pushed = PushedTab().Matches(html).Select(found => new SheetTab(UnescapeJavaScript(found.Groups[1].Value), found.Groups[2].Value)).ToImmutableArray();
        if (pushed.Length > 0)
        {
            return pushed;
        }

        var menu = page.QuerySelectorAll("#sheet-menu li")
            .Select(item => (Item: item, Found: MenuButton().Match(item.GetAttribute("id") ?? "")))
            .Where(item => item.Found.Success)
            .Select(item => new SheetTab(string.Concat(HtmlReading.ReadStrippedTexts(item.Item)), item.Found.Groups[1].Value))
            .ToImmutableArray();
        return menu.Length > 0
            ? menu
            : [.. SwitchToSheet().Matches(html).Select(found => found.Groups[1].Value).Distinct().Select(gid => new SheetTab($"gid_{gid}", gid))];
    }

    /// <summary>The page's title without Google's " - Google Sheets"; the sheet's id when it has none.</summary>
    public static string ReadTitle(IDocument page, string sheetId)
    {
        var title = page.QuerySelector("title");
        return title is null ? sheetId : TrimGoogleSuffix(string.Concat(HtmlReading.ReadStrippedTexts(title)));
    }

    public static string TrimGoogleSuffix(string title) => GoogleSuffix().Replace(title, "");

    /// <summary>A JavaScript string's escapes (<c>\x41</c>, <c>\u0041</c>, <c>\"</c>) → their characters.</summary>
    public static string UnescapeJavaScript(string text) =>
        JavaScriptEscape().Replace(text, found =>
            found.Groups[1].Success || found.Groups[2].Success
                ? ((char)int.Parse(found.Groups[1].Success ? found.Groups[1].Value : found.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()
                : found.Groups[3].Value);

    [GeneratedRegex(@"items\.push\(\{name: ""((?:[^""\\]|\\.)*)"",.*?gid: ""(-?\d+)""")]
    private static partial Regex PushedTab();

    [GeneratedRegex(@"sheet-button-(\d+)")]
    private static partial Regex MenuButton();

    [GeneratedRegex(@"switchToSheet\('(\d+)'\)")]
    private static partial Regex SwitchToSheet();

    [GeneratedRegex(@"\s*-\s*Google (Drive|Sheets)\s*$")]
    private static partial Regex GoogleSuffix();

    [GeneratedRegex(@"\\x([0-9a-fA-F]{2})|\\u([0-9a-fA-F]{4})|\\(.)")]
    private static partial Regex JavaScriptEscape();
}
