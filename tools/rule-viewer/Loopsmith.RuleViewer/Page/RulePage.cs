using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>The page's own style and script, read from the tool's embedded files.</summary>
public sealed record PageAssets(string Style, string Script);

/// <summary>
/// The rules page: its title and what it shows, a chip per scope (the first shown), and each scope's groups of flows,
/// scrolling sideways in their own area on a narrow screen.
/// </summary>
public static class RulePage
{
    private const string Fonts =
        "https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@600&family=IBM+Plex+Sans:wght@400;500;600&family=IBM+Plex+Mono:wght@400;500&display=swap";

    /// <summary>
    /// The one notched arrowhead every wire ends in. A flow's own SVG can't hold it: a scope that is hidden would hide the
    /// marker the shown scopes' wires point to.
    /// </summary>
    private const string ArrowheadDefinition =
        "<svg class=\"defs\" aria-hidden=\"true\" width=\"0\" height=\"0\"><defs><marker id=\"rhead\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" "
        + "markerWidth=\"9\" markerHeight=\"9\" markerUnits=\"userSpaceOnUse\" orient=\"auto\"><path d=\"M0,0 L10,5 L0,10 L3,5 z\"/></marker></defs></svg>\n";

    public static string RenderPage(SubclassRules rules, PageAssets assets) =>
        "<!doctype html>\n<html lang=\"en\">\n<meta charset=\"utf-8\">\n"
        + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">\n"
        + $"<title>{RuleBoxView.Encode(rules.Title)} rules</title>\n"
        + $"<link rel=\"stylesheet\" href=\"{Fonts}\">\n"
        + $"<style>\n{assets.Style}</style>\n"
        + ArrowheadDefinition
        + "<div class=\"wrap\">\n"
        + "  <header style=\"display:flex;flex-direction:column;gap:8px\">\n"
        + $"    <h1>{RuleBoxView.Encode(rules.Title)} rules</h1>\n"
        + "    <p class=\"lede\">Every rule in Loopsmith's rules as one flow, read left to right: what sets it off, what must be in place, what it gives. They are grouped by what sets them off.</p>\n"
        + "  </header>\n"
        + "  <div class=\"rulesview\">\n"
        + $"    <div class=\"bar\">{string.Concat(rules.Scopes.Select(RenderChip))}</div>\n"
        + string.Concat(rules.Scopes.Select(RenderScope))
        + "  </div>\n"
        + "</div>\n"
        + $"<script>\n{assets.Script}</script>\n"
        + "</html>\n";

    private static string RenderChip(RuleScope scope) =>
        $"<button class=\"chip\" aria-pressed=\"false\" data-scope-chip=\"{ToScopeId(scope.Kind)}\">{RuleBoxView.Encode(scope.Name)}</button>";

    private static string RenderScope(RuleScope scope) =>
        $"    <div class=\"rscroll rscope\" data-scope=\"{ToScopeId(scope.Kind)}\" hidden>"
        + string.Concat(scope.Groups.Select(RuleGroupView.RenderGroup))
        + "</div>\n";

    private static string ToScopeId(ScopeKind kind) =>
        string.Concat(kind.ToString()[..1].ToLowerInvariant(), kind.ToString()[1..]);
}
