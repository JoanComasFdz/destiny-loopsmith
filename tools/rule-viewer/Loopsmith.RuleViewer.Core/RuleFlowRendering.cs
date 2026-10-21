using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// The rule tree as text (the golden test's: how many rules and flows, then each file, its elements and every flow, one
/// line each) and as JSON (what the rules page is drawn from, for whoever reads it next: the e2e scenarios to write). Pure.
/// </summary>
public static class RuleFlowRendering
{
    public static string RenderText(RuleTree tree)
    {
        var files = RuleTreeDrawing.ListFiles(tree.Root).ToImmutableArray();
        var rules = files.SelectMany(file => file.Elements).SelectMany(element => element.Rules).ToImmutableArray();
        return string.Join('\n', new[] { $"# {tree.Title}: {FormatNumber(rules.Length)} rules, {FormatNumber(rules.Sum(rule => rule.Flows.Length))} flows" }
            .Concat(files.SelectMany(RenderFileText))) + "\n";
    }

    private static IEnumerable<string> RenderFileText(RuleFile file) =>
        new[] { "", $"## {file.Path}" }
            .Concat(file.Elements.SelectMany(element => new[] { "", $"### {element.Box.Name} ({element.Box.Title})" }
                .Concat(element.Rules.SelectMany(rule => rule.Flows).Select(RenderFlowLine))));

    /// <summary>"gamblers-dodge#0 hunter-class-abilities-gamblers-dodge-1: [Trigger] Class ability (Dodge) ➜ [Subclass ability · Class ability] Gambler's Dodge (equipped) ➜ [Effect] Melee energy (?)".</summary>
    private static string RenderFlowLine(RuleFlow flow) =>
        $"{flow.Id} {flow.Rule}: {RenderBox(flow.Trigger)} ➜ "
        + (flow.Conditions.IsEmpty ? "always" : string.Join(" + ", flow.Conditions.Select(RenderBox)))
        + $" ➜ {string.Join(", ", flow.Results.Select(RenderBox))}"
        + (flow.ByChance ? " (chance)" : "");

    private static string RenderBox(RuleBox box) =>
        $"[{box.Title}] {box.Name}" + (box.Caption.Length == 0 ? "" : $" ({box.Caption})");

    /// <summary>
    /// <c>{"title", "root": folder}</c>; a folder is <c>{path, name, folders, files}</c>; a file <c>{path, name, elements}</c>;
    /// an element <c>{id, box, rules}</c>; a rule <c>{name, id, flows}</c>; a flow <c>{id, element, rule, file, reason,
    /// chance, trigger, conditions, results}</c>; a box <c>{node, title, name, caption, tone, role, icon: {kind, url | text}}</c>.
    /// </summary>
    public static string RenderJson(RuleTree tree) =>
        new JsonObject
        {
            ["title"] = tree.Title,
            ["root"] = ToFolderObject(tree.Root),
        }.ToJsonString();

    private static JsonNode? ToFolderObject(RuleFolder folder) =>
        new JsonObject
        {
            ["path"] = folder.Path,
            ["name"] = folder.Name,
            ["folders"] = new JsonArray([.. folder.Folders.Select(ToFolderObject)]),
            ["files"] = new JsonArray([.. folder.Files.Select(ToFileObject)]),
        };

    private static JsonNode? ToFileObject(RuleFile file) =>
        new JsonObject
        {
            ["path"] = file.Path,
            ["name"] = file.Name,
            ["elements"] = new JsonArray([.. file.Elements.Select(ToElementObject)]),
        };

    private static JsonNode? ToElementObject(RuleElement element) =>
        new JsonObject
        {
            ["id"] = element.Id,
            ["box"] = ToBoxObject(element.Box),
            ["rules"] = new JsonArray([.. element.Rules.Select(ToRuleObject)]),
        };

    private static JsonNode? ToRuleObject(RuleEntry rule) =>
        new JsonObject
        {
            ["name"] = rule.Name,
            ["id"] = rule.Id,
            ["flows"] = new JsonArray([.. rule.Flows.Select(ToFlowObject)]),
        };

    private static JsonNode? ToFlowObject(RuleFlow flow) =>
        new JsonObject
        {
            ["id"] = flow.Id,
            ["element"] = flow.Element,
            ["rule"] = flow.Rule,
            ["file"] = ToText(flow.File),
            ["reason"] = ToText(flow.Reason),
            ["chance"] = flow.ByChance,
            ["trigger"] = ToBoxObject(flow.Trigger),
            ["conditions"] = new JsonArray([.. flow.Conditions.Select(ToBoxObject)]),
            ["results"] = new JsonArray([.. flow.Results.Select(ToBoxObject)]),
        };

    private static JsonNode? ToBoxObject(RuleBox box) =>
        new JsonObject
        {
            ["node"] = ToText(box.Node),
            ["title"] = box.Title,
            ["name"] = box.Name,
            ["caption"] = box.Caption,
            ["tone"] = ToCamelCase(box.Tone.ToString()),
            ["role"] = ToCamelCase(box.Role.ToString()),
            ["icon"] = ToIconObject(box.Icon),
        };

    private static JsonObject ToIconObject(RuleIcon icon) =>
        icon.Match(
            picture => new JsonObject { ["kind"] = "picture", ["url"] = picture.Url },
            masked => new JsonObject { ["kind"] = "maskedBuff", ["url"] = masked.Url },
            emoji => new JsonObject { ["kind"] = "emoji", ["text"] = emoji.Text },
            _ => new JsonObject { ["kind"] = "blank" });

    private static string? ToText(Optional<string> text) => text.Match<string?>(some => some.Value, _ => null);

    private static string FormatNumber(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string ToCamelCase(string name) => string.Concat(name[..1].ToLowerInvariant(), name[1..]);
}
