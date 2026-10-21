using Loopsmith.RuleViewer.Core;

namespace Loopsmith.RuleViewer.Page;

/// <summary>
/// The rules folder as a tree, like a folder explorer: folders, then files, then each file's elements and their rules. Every
/// item has an arrow that folds it (a rule has none: it holds no item), a checkbox that shows or hides its flows (ticked at
/// first) and its name, which selects it. Folders and files start open; elements start folded.
/// </summary>
public static class RuleTreeView
{
    public static string RenderTree(RuleTree tree) =>
        "    <nav class=\"rtree\" aria-label=\"Rules\"><ul role=\"tree\" aria-multiselectable=\"false\">"
        + RenderFolder(tree.Root)
        + "</ul></nav>\n";

    private static string RenderFolder(RuleFolder folder) =>
        RenderItem(
            ToFolderItem(folder.Path),
            $"{folder.Name}/",
            "",
            CountRules(folder),
            true,
            string.Concat(folder.Folders.Select(RenderFolder)) + string.Concat(folder.Files.Select(RenderFile)),
            "folder");

    private static string RenderFile(RuleFile file) =>
        RenderItem(
            RuleFileView.ToFileItem(file),
            file.Name,
            "",
            file.Elements.Sum(element => element.Rules.Length),
            true,
            string.Concat(file.Elements.Select(RenderElement)),
            "file");

    private static string RenderElement(RuleElement element) =>
        RenderItem(
            RuleFileView.ToElementItem(element),
            element.Box.Name,
            RuleIconView.RenderIcon(element.Box.Icon),
            element.Rules.Length,
            false,
            string.Concat(element.Rules.Select(rule => RenderRule(element, rule))),
            "element");

    /// <summary>A rule: its id, and what sets off each of its flows ("Melee hit · Sliding melee hit").</summary>
    private static string RenderRule(RuleElement element, RuleEntry rule) =>
        $"<li role=\"treeitem\" class=\"titem rule\" data-item=\"{RuleBoxView.Encode(RuleFileView.ToRuleItem(element, rule))}\" aria-selected=\"false\">"
        + "<div class=\"trow\"><span class=\"twisty none\"></span>"
        + $"<input type=\"checkbox\" checked aria-label=\"Show {RuleBoxView.Encode(rule.Name)}\">"
        + $"<button type=\"button\" class=\"tname\" title=\"{RuleBoxView.Encode(rule.Name)}\"><span>{RuleBoxView.Encode(rule.Id)}</span>"
        + $"<small>{RuleBoxView.Encode(string.Join(" · ", rule.Flows.Select(flow => flow.Trigger.Name).Distinct()))}</small></button></div></li>";

    private static string RenderItem(string item, string name, string icon, int rules, bool open, string children, string kind) =>
        $"<li role=\"treeitem\" class=\"titem {kind}\" data-item=\"{RuleBoxView.Encode(item)}\" aria-expanded=\"{(open ? "true" : "false")}\" aria-selected=\"false\">"
        + $"<div class=\"trow\"><button type=\"button\" class=\"twisty\" aria-label=\"Fold {RuleBoxView.Encode(name)}\"></button>"
        + $"<input type=\"checkbox\" checked aria-label=\"Show {RuleBoxView.Encode(name)}\">"
        + $"<button type=\"button\" class=\"tname\">{icon}<span>{RuleBoxView.Encode(name)}</span><small>{RulePage.FormatNumber(rules)}</small></button></div>"
        + $"<ul role=\"group\">{children}</ul></li>";

    private static int CountRules(RuleFolder folder) =>
        RuleTreeDrawing.ListFiles(folder).Sum(file => file.Elements.Sum(element => element.Rules.Length));

    private static string ToFolderItem(string path) => $"folder:{path}";
}
