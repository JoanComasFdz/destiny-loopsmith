using System.Globalization;
using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>The flows one trigger sets off, under a heading with its icon, its name and how many flows it has.</summary>
public static class RuleGroupView
{
    public static string RenderGroup(RuleGroup group) =>
        $"<section class=\"rgroup\" data-group=\"{RuleBoxView.Encode(group.Key)}\">"
        + $"<h3>{RuleIconView.RenderHeadingIcon(group.Icon)}{RuleBoxView.Encode(group.Name)} <small>{group.Flows.Length.ToString(CultureInfo.InvariantCulture)}</small></h3>"
        + string.Concat(group.Flows.Select(RuleFlowView.RenderFlow))
        + "</section>";
}
