using System.Collections.Immutable;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Loopsmith.Functional;

namespace Loopsmith.CompendiumDownload.Core;

/// <summary>
/// Google's HTML, read: the page parsed, its sheet table found, and an element's text taken as BeautifulSoup's
/// <c>get_text</c> took it in sheet_dump.py (the Python version), so the tab files come out the same: only plain text,
/// not a script's, a style's or a template's, nor a comment, and a text of spaces alone read as one space (one line
/// break when it holds one), as BeautifulSoup stores it.
/// </summary>
public static class HtmlReading
{
    public static IHtmlDocument ParseHtml(string html) => new HtmlParser().ParseDocument(html);

    /// <summary>The tab's table: the one Google classes <c>waffle</c>, else the first.</summary>
    public static Optional<IElement> FindSheetTable(IDocument page) => Optional.FromNullable(page.QuerySelector("table.waffle") ?? page.QuerySelector("table"));

    /// <summary>The element's text, each <c>&lt;br&gt;</c> a line break when <paramref name="breaksAsNewlines"/> (a cell's text).</summary>
    public static string ReadText(INode node, bool breaksAsNewlines)
    {
        var text = new StringBuilder();
        AppendText(node, breaksAsNewlines, text);
        return text.ToString();
    }

    /// <summary>The element's texts, each trimmed, the empty ones left out (<c>get_text(strip=True)</c>'s parts).</summary>
    public static ImmutableArray<string> ReadStrippedTexts(INode node) =>
        [.. ReadTextNodes(node).Select(PythonText.StripWhitespace).Where(text => text.Length > 0)];

    private static IEnumerable<string> ReadTextNodes(INode node) =>
        node.ChildNodes.SelectMany(child => child switch
        {
            IText text => [ReadTextData(text)],
            IElement element when !IsOpaque(element) => ReadTextNodes(element),
            _ => Enumerable.Empty<string>(),
        });

    private static void AppendText(INode node, bool breaksAsNewlines, StringBuilder text)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText found:
                    text.Append(ReadTextData(found));
                    break;
                case IElement { LocalName: "br" } when breaksAsNewlines:
                    text.Append('\n');
                    break;
                case IElement element when !IsOpaque(element):
                    AppendText(element, breaksAsNewlines, text);
                    break;
            }
        }
    }

    /// <summary>A text of ASCII spaces, tabs and line breaks alone is one line break if it holds one, else one space (outside <c>&lt;pre&gt;</c> and <c>&lt;textarea&gt;</c>).</summary>
    private static string ReadTextData(IText text) =>
        text.Data.Length > 0 && text.Data.All(character => character is ' ' or '\n' or '\t' or '\f' or '\r') && !IsPreformatted(text)
            ? text.Data.Contains('\n') ? "\n" : " "
            : text.Data;

    private static bool IsPreformatted(INode node) =>
        node.Parent is IElement parent && (parent.LocalName is "pre" or "textarea" || IsPreformatted(parent));

    /// <summary>A script, style or template: BeautifulSoup keeps its text apart from the page's.</summary>
    private static bool IsOpaque(IElement element) => element.LocalName is "script" or "style" or "template";
}
