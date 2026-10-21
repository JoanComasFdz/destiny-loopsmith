using System.Collections.Immutable;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// The page of one rule's flows, the one its PNG is taken of: only the flows, stacked, each under the rule's full name
/// (no title, lede or tree), with the rules page's style and script, in its light or dark theme.
/// </summary>
public static class RuleFlowPage
{
    /// <summary>What the PNG is taken of: the flows and a margin around them.</summary>
    public const string ShotSelector = ".shot";

    public static string RenderPage(string ruleName, ImmutableArray<RuleFlow> flows, PageAssets assets, FlowTheme theme) =>
        $"<!doctype html>\n<html lang=\"en\" data-theme=\"{(theme == FlowTheme.Dark ? "dark" : "light")}\">\n<meta charset=\"utf-8\">\n"
        + $"<title>{RuleBoxView.Encode(ruleName)}</title>\n"
        + $"<link rel=\"stylesheet\" href=\"{RulePage.Fonts}\">\n"
        + $"<style>\n{assets.Style}.shot{{width:max-content;background:var(--bg);padding:4px 16px 12px}}\n.shot .flow:first-child{{border-top:none}}\n</style>\n"
        + RulePage.ArrowheadDefinition
        + "<div class=\"shot\"><div class=\"rrule\">"
        + string.Concat(flows.Select(RuleFlowView.RenderFlow))
        + "</div></div>\n"
        + $"<script>\n{assets.Script}</script>\n"
        + "</html>\n";
}
