using System.Globalization;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>The lanes one trigger sets off, under a heading with its icon, its name and how many lanes it has.</summary>
public static class LaneGroupView
{
    public static string RenderGroup(LaneGroup group) =>
        $"<section class=\"lgroup\" data-group=\"{LaneBoxView.Encode(group.Key)}\">"
        + $"<h3>{LaneIconView.RenderHeadingIcon(group.Icon)}{LaneBoxView.Encode(group.Name)} <small>{group.Lanes.Length.ToString(CultureInfo.InvariantCulture)}</small></h3>"
        + string.Concat(group.Lanes.Select(LaneRowView.RenderLane))
        + "</section>";
}
