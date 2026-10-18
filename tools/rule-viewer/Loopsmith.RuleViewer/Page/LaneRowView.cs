using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// One lane, read left to right: its trigger ➜ what must be in place ("always produces" when nothing is) ➜ its results,
/// and "(chance)" under them when the rule only may fire. The wires between the columns are an empty SVG the page's
/// script draws once the boxes have landed.
/// </summary>
public static class LaneRowView
{
    public static string RenderLane(RuleLane lane) =>
        $"<div class=\"lane3\" data-lane=\"{LaneBoxView.Encode(lane.Id)}\"><svg class=\"wires\" aria-hidden=\"true\"></svg>"
        + $"<div class=\"seg tcol\">{LaneBoxView.RenderBox(lane.Trigger)}</div>"
        + RenderConditions(lane)
        + $"<div class=\"seg rcol\">{string.Concat(lane.Results.Select(LaneBoxView.RenderBox))}{(lane.ByChance ? "<span class=\"lor\">(chance)</span>" : "")}</div>"
        + "</div>";

    private static string RenderConditions(RuleLane lane) =>
        lane.Conditions.IsEmpty
            ? "<div class=\"seg ccol none\"><span class=\"lalways\">always produces</span></div>"
            : $"<div class=\"seg ccol\">{string.Concat(lane.Conditions.Select(LaneBoxView.RenderBox))}</div>";
}
