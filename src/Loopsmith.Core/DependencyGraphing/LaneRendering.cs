using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.DependencyGraphing;

/// <summary>
/// The lanes as text (the golden test's: each scope's groups and counts, then every lane of the widest scope, one line
/// each) and as JSON (what the lanes page is drawn from, for whoever reads it next: the e2e scenarios to write). Pure.
/// </summary>
public static class LaneRendering
{
    public static string RenderText(RuleLanes lanes) =>
        string.Join('\n', new[] { $"# {lanes.Title} lanes" }
            .Concat(lanes.Scopes.SelectMany(RenderScopeSummary))
            .Concat(lanes.Scopes.IsEmpty ? [] : RenderScopeLanes(lanes.Scopes[^1]))) + "\n";

    private static IEnumerable<string> RenderScopeSummary(LaneScope scope) =>
        new[] { "", $"## {scope.Name} ({ToCamelCase(scope.Kind.ToString())}): {FormatNumber(scope.Groups.Sum(group => group.Lanes.Length))} lanes" }
            .Concat(scope.Groups.Select(group => $"  {group.Name}: {FormatNumber(group.Lanes.Length)}"));

    private static IEnumerable<string> RenderScopeLanes(LaneScope scope) =>
        new[] { "", $"## Every lane of {scope.Name}" }
            .Concat(scope.Groups.SelectMany(group => new[] { "", $"### {group.Name} ({FormatNumber(group.Lanes.Length)})" }
                .Concat(group.Lanes.Select(RenderLaneLine))));

    /// <summary>"gamblers-dodge#0: [Trigger] Dodge ➜ [Subclass ability · Class ability] Gambler's Dodge (equipped) ➜ [Effect] Melee energy (?)".</summary>
    private static string RenderLaneLine(RuleLane lane) =>
        $"{lane.Id}: {RenderBox(lane.Trigger)} ➜ "
        + (lane.Conditions.IsEmpty ? "always" : string.Join(" + ", lane.Conditions.Select(RenderBox)))
        + $" ➜ {string.Join(", ", lane.Results.Select(RenderBox))}"
        + (lane.ByChance ? " (chance)" : "");

    private static string RenderBox(LaneBox box) =>
        $"[{box.Title}] {box.Name}" + (box.Caption.Length == 0 ? "" : $" ({box.Caption})");

    /// <summary>
    /// <c>{"title", "scopes": [{id, name, groups: [{key, name, icon, lanes: [{id, element, file, reason, chance, trigger,
    /// conditions, results}]}]}]}</c>; a box is <c>{node, title, name, caption, tone, role, icon: {kind, url | text}}</c>.
    /// </summary>
    public static string RenderJson(RuleLanes lanes) =>
        new JsonObject
        {
            ["title"] = lanes.Title,
            ["scopes"] = new JsonArray([.. lanes.Scopes.Select(ToScopeObject)]),
        }.ToJsonString();

    private static JsonNode? ToScopeObject(LaneScope scope) =>
        new JsonObject
        {
            ["id"] = ToCamelCase(scope.Kind.ToString()),
            ["name"] = scope.Name,
            ["groups"] = new JsonArray([.. scope.Groups.Select(ToGroupObject)]),
        };

    private static JsonNode? ToGroupObject(LaneGroup group) =>
        new JsonObject
        {
            ["key"] = group.Key,
            ["name"] = group.Name,
            ["icon"] = ToIconObject(group.Icon),
            ["lanes"] = new JsonArray([.. group.Lanes.Select(ToLaneObject)]),
        };

    private static JsonNode? ToLaneObject(RuleLane lane) =>
        new JsonObject
        {
            ["id"] = lane.Id,
            ["element"] = lane.Element,
            ["file"] = ToText(lane.File),
            ["reason"] = ToText(lane.Reason),
            ["chance"] = lane.ByChance,
            ["trigger"] = ToBoxObject(lane.Trigger),
            ["conditions"] = new JsonArray([.. lane.Conditions.Select(ToBoxObject)]),
            ["results"] = new JsonArray([.. lane.Results.Select(ToBoxObject)]),
        };

    private static JsonNode? ToBoxObject(LaneBox box) =>
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

    private static JsonObject ToIconObject(LaneIcon icon) =>
        icon.Match(
            picture => new JsonObject { ["kind"] = "picture", ["url"] = picture.Url },
            masked => new JsonObject { ["kind"] = "maskedBuff", ["url"] = masked.Url },
            emoji => new JsonObject { ["kind"] = "emoji", ["text"] = emoji.Text },
            _ => new JsonObject { ["kind"] = "blank" });

    private static string? ToText(Optional<string> text) => text.Match<string?>(some => some.Value, _ => null);

    private static string FormatNumber(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string ToCamelCase(string name) => string.Concat(name[..1].ToLowerInvariant(), name[1..]);
}
