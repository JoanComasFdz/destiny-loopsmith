using System.Collections.Immutable;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// A snapshot → its offline <c>viewer.html</c>: each tab's table from Google's page, its cell styles renamed per tab so
/// one tab's <c>.s0</c> doesn't clash with another's, its images pointed at <c>images/</c> (those Google reports as gone
/// left out, as the live sheet shows them: empty) and its outside links opened in a new tab.
/// </summary>
public static partial class ViewerWriting
{
    /// <param name="images">Each image's link → its file in <c>images/</c> (<c>map.json</c>).</param>
    /// <param name="goneImages">The links Google reports as gone (<c>failed.txt</c>'s 400, 403, 404 and 410).</param>
    public static string WriteViewer(SheetMeta meta, ImmutableArray<SnapshotTab> tabs, IReadOnlyDictionary<string, string> images, IReadOnlySet<string> goneImages)
    {
        var styles = new List<string>();
        var templates = new List<string>();
        foreach (var tab in tabs)
        {
            var page = tab.RawHtml is Optional<string>.Some html ? HtmlReading.ParseHtml(html.Value) : null;
            var table = page is null ? null : HtmlReading.FindSheetTable(page);
            if (page is null || table is null)
            {
                templates.Add($"<template id=\"tpl-{tab.Tab.Gid}\"><p class=\"missing\">This tab could not be loaded.</p></template>");
                continue;
            }

            styles.AddRange(page.QuerySelectorAll("style").SelectMany(style => ReadCellStyles(style.TextContent, tab.Place)));
            RewriteTable(table, tab.Place, images, goneImages);
            templates.Add($"<template id=\"tpl-{tab.Tab.Gid}\">{table.OuterHtml}</template>");
        }

        var tabList = tabs.Select(tab => (IEnumerable<(string, string)>)[("gid", tab.Tab.Gid), ("name", tab.Tab.Name), ("file", $"{SheetNaming.ToStem(tab.Place, tab.Tab.Name)}.md")]);
        return ViewerTemplate.Page
            .Replace("/*__TITLE__*/", PythonText.EscapeHtml($"{meta.Title} ({meta.ExtractedAt})"))
            .Replace("/*__SHEET_CSS__*/", string.Join('\n', styles.Distinct()))
            .Replace("<!--__TEMPLATES__-->", string.Join('\n', templates))
            .Replace("/*__TABS__*/[]", JsonWriting.WriteArrayOfObjects(tabList, asciiOnly: false))
            .Replace("/*__META__*/{}", JsonWriting.WriteObject(MetaReading.ListMetaMembers(meta), asciiOnly: false, indented: false));
    }

    /// <summary>A style sheet's cell rules (<c>.ritz … { … }</c>), renamed for the tab in <paramref name="place"/> (<c>.s3</c> → <c>.t6s3</c>), Google's fonts by their own names.</summary>
    public static ImmutableArray<string> ReadCellStyles(string styleSheet, int place) =>
        [.. CellRule().Matches(styleSheet).Select(rule =>
        {
            var renamed = CellStyleClass().Replace(rule.Value, $".t{place}s${{1}}");
            var roboto = renamed.Replace("docs-Roboto", "Roboto");
            return GoogleFont().Replace(roboto, "\"${1}\"");
        })];

    /// <summary>A link through Google's redirect (<c>https://www.google.com/url?q=…</c>) → where it goes.</summary>
    public static string UnwrapGoogleUrl(string href)
    {
        if (!href.StartsWith("https://www.google.com/url?", StringComparison.Ordinal))
        {
            return href;
        }

        var query = href["https://www.google.com/url?".Length..].Split('#')[0];
        var target = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[1].Length > 0)
            .Select(pair => (Name: DecodeQueryPart(pair[0]), Value: DecodeQueryPart(pair[1])))
            .FirstOrDefault(pair => pair.Name == "q");
        return target.Value ?? href;
    }

    private static void RewriteTable(IElement table, int place, IReadOnlyDictionary<string, string> images, IReadOnlySet<string> goneImages)
    {
        foreach (var element in table.QuerySelectorAll("[class]"))
        {
            element.ClassName = string.Join(' ', element.ClassList.Select(name => CellStyleName().IsMatch(name) ? $"t{place}{name}" : name));
        }

        foreach (var element in table.QuerySelectorAll("script, style"))
        {
            element.Remove();
        }

        foreach (var image in table.QuerySelectorAll("img[src]"))
        {
            var source = image.GetAttribute("src")!;
            if (goneImages.Contains(source))
            {
                image.Remove();
                continue;
            }

            if (images.TryGetValue(source, out var file))
            {
                image.SetAttribute("src", $"images/{file}");
            }
            else
            {
                image.SetAttribute("referrerpolicy", "no-referrer");
            }

            image.SetAttribute("loading", "lazy");
        }

        foreach (var link in table.QuerySelectorAll("a[href]"))
        {
            var href = UnwrapGoogleUrl(link.GetAttribute("href")!);
            link.SetAttribute("href", href);
            if (href.StartsWith('#'))
            {
                link.RemoveAttribute("target");
            }
            else
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer");
            }
        }
    }

    /// <summary>Python's <c>unquote_plus</c>: <c>+</c> is a space, then <c>%XX</c> decoded as UTF-8.</summary>
    private static string DecodeQueryPart(string part) => Uri.UnescapeDataString(part.Replace('+', ' '));

    [GeneratedRegex(@"\.ritz[^{}]*\{[^}]*\}")]
    private static partial Regex CellRule();

    [GeneratedRegex(@"\.s(\d+)\b")]
    private static partial Regex CellStyleClass();

    [GeneratedRegex(@"docs-([A-Za-z][\w ]*?)(?=[,;}])")]
    private static partial Regex GoogleFont();

    [GeneratedRegex(@"^s\d+$")]
    private static partial Regex CellStyleName();
}
