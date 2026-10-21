using System.Globalization;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>The page's own style and script, read from the tool's embedded files.</summary>
public sealed record PageAssets(string Style, string Script);

/// <summary>
/// The rules page: its title and what it shows, then the rules folder as a tree beside every rule's flows, file by file.
/// The tree is a folder explorer: an item's arrow folds it, its checkbox shows or hides its flows, its name selects it and
/// brings its flows into view. On a narrow screen the tree sits above the flows, which scroll sideways in their own area.
/// </summary>
public static class RulePage
{
    internal const string Fonts =
        "https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@600&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap";

    /// <summary>
    /// The one notched arrowhead every wire ends in. A flow's own SVG can't hold it: a flow that is hidden would hide the
    /// marker the shown flows' wires point to.
    /// </summary>
    internal const string ArrowheadDefinition =
        "<svg class=\"defs\" aria-hidden=\"true\" width=\"0\" height=\"0\"><defs><marker id=\"rhead\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" "
        + "markerWidth=\"9\" markerHeight=\"9\" markerUnits=\"userSpaceOnUse\" orient=\"auto\"><path d=\"M0,0 L10,5 L0,10 L3,5 z\"/></marker></defs></svg>\n";

    public static string RenderPage(RuleTree tree, PageAssets assets)
    {
        var rules = RuleTreeDrawing.ListRules(tree.Root).ToList();
        return "<!doctype html>\n<html lang=\"en\">\n<meta charset=\"utf-8\">\n"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">\n"
            + $"<title>{RuleBoxView.Encode(tree.Title)}</title>\n"
            + $"<link rel=\"stylesheet\" href=\"{Fonts}\">\n"
            + $"<style>\n{assets.Style}</style>\n"
            + ArrowheadDefinition
            + "<div class=\"wrap\">\n"
            + "  <header style=\"display:flex;flex-direction:column;gap:8px\">\n"
            + $"    <h1>{RuleBoxView.Encode(tree.Title)}</h1>\n"
            + $"    <p class=\"lede\">Every rule in the rules folder as one flow per claim, read left to right: what sets it off, what must be in place, what it gives. "
            + $"{FormatNumber(rules.Count)} rules, {FormatNumber(rules.Sum(rule => rule.Flows.Length))} flows, laid out file by file as the folder is. "
            + "In the tree, the arrow folds an item, the checkbox shows or hides its flows, and its name takes you to them.</p>\n"
            + "  </header>\n"
            + "  <div class=\"rview\">\n"
            + RuleTreeView.RenderTree(tree)
            + $"    <main class=\"rflows\">{string.Concat(RuleTreeDrawing.ListFiles(tree.Root).Select(RuleFileView.RenderFile))}</main>\n"
            + "  </div>\n"
            + "</div>\n"
            + $"<script>\n{assets.Script}</script>\n"
            + "</html>\n";
    }

    internal static string FormatNumber(int number) => number.ToString(CultureInfo.InvariantCulture);
}
