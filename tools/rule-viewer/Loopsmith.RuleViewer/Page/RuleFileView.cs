using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// A rule file's flows: under its path, each element under its icon, name and kind, then each of its rules' flows. Each part
/// carries the tree item it belongs to (<c>data-item</c>), which the tree's checkboxes show or hide and its names scroll to.
/// </summary>
public static class RuleFileView
{
    public static string RenderFile(RuleFile file) =>
        $"<section class=\"rfile\" data-item=\"{RuleBoxView.Encode(ToFileItem(file))}\"><h2>{RuleBoxView.Encode(file.Path)}</h2>"
        + string.Concat(file.Elements.Select(element => RenderElement(element)))
        + "</section>";

    private static string RenderElement(RuleElement element) =>
        $"<section class=\"relem\" data-item=\"{RuleBoxView.Encode(ToElementItem(element))}\">"
        + $"<h3>{RuleIconView.RenderHeadingIcon(element.Box.Icon)}{RuleBoxView.Encode(element.Box.Name)} <small>{RuleBoxView.Encode(element.Box.Title)}</small></h3>"
        + string.Concat(element.Rules.Select(rule => RenderRule(element, rule)))
        + "</section>";

    private static string RenderRule(RuleElement element, RuleEntry rule) =>
        $"<div class=\"rrule\" data-item=\"{RuleBoxView.Encode(ToRuleItem(element, rule))}\">{string.Concat(rule.Flows.Select(RuleFlowView.RenderFlow))}</div>";

    internal static string ToFileItem(RuleFile file) => $"file:{file.Path}";

    internal static string ToElementItem(RuleElement element) => $"element:{element.Id}";

    /// <summary>A rule's item, by its element: an enhanced trait's rules have its perk's names.</summary>
    internal static string ToRuleItem(RuleElement element, RuleEntry rule) => $"rule:{element.Id}:{rule.Name}";
}
