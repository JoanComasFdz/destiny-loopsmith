using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// One flow, read left to right: its trigger ➜ what must be in place ("always produces" when nothing is) ➜ its results,
/// and "(chance)" under them when the rule only may fire. The wires between the columns are an empty SVG the page's
/// script draws once the boxes have landed.
/// </summary>
public static class RuleFlowView
{
    public static string RenderFlow(RuleFlow flow) =>
        $"<div class=\"flow\" data-flow=\"{RuleBoxView.Encode(flow.Id)}\"><svg class=\"wires\" aria-hidden=\"true\"></svg>"
        + $"<div class=\"seg tcol\">{RuleBoxView.RenderBox(flow.Trigger)}</div>"
        + RenderConditions(flow)
        + $"<div class=\"seg rcol\">{string.Concat(flow.Results.Select(RuleBoxView.RenderBox))}{(flow.ByChance ? "<span class=\"ror\">(chance)</span>" : "")}</div>"
        + "</div>";

    private static string RenderConditions(RuleFlow flow) =>
        flow.Conditions.IsEmpty
            ? "<div class=\"seg ccol none\"><span class=\"ralways\">always produces</span></div>"
            : $"<div class=\"seg ccol\">{string.Concat(flow.Conditions.Select(RuleBoxView.RenderBox))}</div>";
}
