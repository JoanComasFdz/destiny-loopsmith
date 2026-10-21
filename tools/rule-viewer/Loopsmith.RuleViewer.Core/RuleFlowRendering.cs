using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Loopsmith.Functional;

namespace Loopsmith.RuleViewer.Core;

/// <summary>
/// The flows as text (the golden test's: each scope's groups and counts, then every flow of the widest scope, one line
/// each) and as JSON (what the rules page is drawn from, for whoever reads it next: the e2e scenarios to write). Pure.
/// </summary>
public static class RuleFlowRendering
{
    public static string RenderText(SubclassRules rules) =>
        string.Join('\n', new[] { $"# {rules.Title} rules" }
            .Concat(rules.Scopes.SelectMany(RenderScopeSummary))
            .Concat(rules.Scopes.IsEmpty ? [] : RenderScopeFlows(rules.Scopes[^1]))) + "\n";

    private static IEnumerable<string> RenderScopeSummary(RuleScope scope) =>
        new[] { "", $"## {scope.Name} ({ToCamelCase(scope.Kind.ToString())}): {FormatNumber(scope.Groups.Sum(group => group.Flows.Length))} rules" }
            .Concat(scope.Groups.Select(group => $"  {group.Name}: {FormatNumber(group.Flows.Length)}"));

    private static IEnumerable<string> RenderScopeFlows(RuleScope scope) =>
        new[] { "", $"## Every rule of {scope.Name}" }
            .Concat(scope.Groups.SelectMany(group => new[] { "", $"### {group.Name} ({FormatNumber(group.Flows.Length)})" }
                .Concat(group.Flows.Select(RenderFlowLine))));

    /// <summary>"gamblers-dodge#0 hunter-class-abilities-gamblers-dodge-1: [Trigger] Dodge ➜ [Subclass ability · Class ability] Gambler's Dodge (equipped) ➜ [Effect] Melee energy (?)".</summary>
    private static string RenderFlowLine(RuleFlow flow) =>
        $"{flow.Id} {flow.Rule}: {RenderBox(flow.Trigger)} ➜ "
        + (flow.Conditions.IsEmpty ? "always" : string.Join(" + ", flow.Conditions.Select(RenderBox)))
        + $" ➜ {string.Join(", ", flow.Results.Select(RenderBox))}"
        + (flow.ByChance ? " (chance)" : "");

    private static string RenderBox(RuleBox box) =>
        $"[{box.Title}] {box.Name}" + (box.Caption.Length == 0 ? "" : $" ({box.Caption})");

    /// <summary>
    /// <c>{"title", "scopes": [{id, name, groups: [{key, name, icon, flows: [{id, element, rule, file, reason, chance, trigger,
    /// conditions, results}]}]}]}</c>; a box is <c>{node, title, name, caption, tone, role, icon: {kind, url | text}}</c>.
    /// </summary>
    public static string RenderJson(SubclassRules rules) =>
        new JsonObject
        {
            ["title"] = rules.Title,
            ["scopes"] = new JsonArray([.. rules.Scopes.Select(ToScopeObject)]),
        }.ToJsonString();

    private static JsonNode? ToScopeObject(RuleScope scope) =>
        new JsonObject
        {
            ["id"] = ToCamelCase(scope.Kind.ToString()),
            ["name"] = scope.Name,
            ["groups"] = new JsonArray([.. scope.Groups.Select(ToGroupObject)]),
        };

    private static JsonNode? ToGroupObject(RuleGroup group) =>
        new JsonObject
        {
            ["key"] = group.Key,
            ["name"] = group.Name,
            ["icon"] = ToIconObject(group.Icon),
            ["flows"] = new JsonArray([.. group.Flows.Select(ToFlowObject)]),
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
