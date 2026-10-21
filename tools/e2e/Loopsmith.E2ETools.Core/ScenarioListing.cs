using System.Collections.Immutable;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// A recording's runs → its <c>scenarios.json</c>, which <c>post</c> reads (<see cref="JsonReading.ReadScenarios"/>) and
/// push-gifs.sh lists the files of: one entry per run, in the records' order, with the run's names, Gherkin, screen, GIF and
/// MP4 (under its domain folder), startup time, result, why it was picked (<see cref="ClassSelection"/>) and the flow of the
/// rule its Gherkin names (its PNG, or why there is none). Also the rules to draw, and the summary record.sh prints.
/// </summary>
public static partial class ScenarioListing
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Each rule the runs' Gherkins name ("# Rule &lt;name&gt;:"), once per domain folder, in the runs' order.</summary>
    public static ImmutableArray<FlowToDraw> ListFlows(ImmutableArray<RunRecord> records) =>
        [.. records.SelectMany(record => FindRules(record.Gherkin).Select(rule => new FlowToDraw(record.Domain, rule))).Distinct()];

    /// <summary>The line record.sh reads for a flow to draw: its domain folder and rule.</summary>
    public static string WriteFlowLine(FlowToDraw flow) => $"{flow.Domain} {flow.Rule}";

    /// <summary>The rule a run's Gherkin names first: the one its entry's flow shows.</summary>
    public static Optional<string> FindRule(RunRecord record) =>
        FindRules(record.Gherkin).Select(Optional.Some).DefaultIfEmpty(Optional.None<string>()).First();

    /// <summary>Where a rule's flow PNG is drawn, relative to the output folder.</summary>
    public static string FindFlowPath(string domain, string rule) => $"{domain}/{rule}.png";

    /// <summary>
    /// record.sh's selection lines ("&lt;class&gt;\tchanged", or "&lt;class&gt;\tfixtures\t&lt;path&gt;" per changed fixture)
    /// → the classes that changed and each fixture-only class's fixtures, sorted and once each.
    /// </summary>
    public static ClassSelection ReadSelection(string tsv)
    {
        var lines = tsv.Split('\n').Select(line => line.TrimEnd('\r').Split('\t')).ToImmutableArray();
        var changed = lines.Where(parts => parts.Length >= 2 && parts[1] == "changed").Select(parts => parts[0]).ToImmutableHashSet();
        var fixtures = lines
            .Where(parts => parts.Length == 3 && parts[1] == "fixtures")
            .GroupBy(parts => parts[0])
            .ToImmutableDictionary(group => group.Key, group => group.Select(parts => parts[2]).Distinct().Order(StringComparer.Ordinal).ToImmutableArray());
        return new ClassSelection(changed, fixtures);
    }

    /// <summary>The <c>scenarios.json</c> text; <paramref name="flows"/> says what became of each run's rule's flow.</summary>
    public static string WriteScenarios(ImmutableArray<RunRecord> records, ClassSelection selection, ImmutableDictionary<FlowToDraw, DrawnFlow> flows) =>
        new JsonArray([.. records.Select(record => WriteEntry(record, selection, flows))]).ToJsonString(Indented);

    /// <summary>The summary record.sh prints: each run's result, startup time and GIF, and why a fixture-only one was re-recorded.</summary>
    public static ImmutableArray<string> WriteSummary(ImmutableArray<RunRecord> records, ClassSelection selection) =>
        ["", "Scenarios:", .. records.Select(record => WriteSummaryLine(record, selection))];

    private static JsonObject WriteEntry(RunRecord record, ClassSelection selection, ImmutableDictionary<FlowToDraw, DrawnFlow> flows) =>
        new([.. WriteRunFields(record), .. WriteSelectionFields(FindFixtures(record, selection)), .. WriteFlowFields(record, flows)]);

    private static ImmutableArray<KeyValuePair<string, JsonNode?>> WriteRunFields(RunRecord record) =>
        [
            new("domain", record.Domain),
            new("class", record.Class),
            new("method", record.Method),
            new("title", record.Title),
            new("gherkin", record.Gherkin.Match<JsonNode?>(some => JsonValue.Create(some.Value), _ => null)),
            new("screen", record.Screen),
            new("gif", $"{record.Domain}/{record.Gif}.gif"),
            new("mp4", $"{record.Domain}/{record.Gif}.mp4"),
            new("startupSeconds", RoundStartup(record).Match<JsonNode?>(some => JsonValue.Create(some.Value), _ => null)),
            new("passed", record.Passed),
        ];

    /// <summary>"changed", or "fixtures" with the fixture files that alone picked the run's class.</summary>
    private static ImmutableArray<KeyValuePair<string, JsonNode?>> WriteSelectionFields(Optional<ImmutableArray<string>> fixtures) =>
        fixtures.Match<ImmutableArray<KeyValuePair<string, JsonNode?>>>(
            paths => [new("selected", "fixtures"), new("fixtures", new JsonArray([.. paths.Value.Select(path => JsonValue.Create(path))]))],
            _ => [new("selected", "changed")]);

    /// <summary>The rule the run's Gherkin names and its flow PNG, or why there is none; nothing when it names no rule.</summary>
    private static ImmutableArray<KeyValuePair<string, JsonNode?>> WriteFlowFields(RunRecord record, ImmutableDictionary<FlowToDraw, DrawnFlow> flows) =>
        FindRule(record).Match<ImmutableArray<KeyValuePair<string, JsonNode?>>>(
            rule => [new("rule", rule.Value), WriteFlow(record.Domain, rule.Value, FindDrawnFlow(flows, new FlowToDraw(record.Domain, rule.Value)))],
            _ => []);

    private static KeyValuePair<string, JsonNode?> WriteFlow(string domain, string rule, DrawnFlow flow) =>
        flow.Match<KeyValuePair<string, JsonNode?>>(
            _ => new("flow", FindFlowPath(domain, rule)),
            failed => new("flowError", failed.Error),
            _ => new("flowError", "not drawn"));

    private static DrawnFlow FindDrawnFlow(ImmutableDictionary<FlowToDraw, DrawnFlow> flows, FlowToDraw flow) =>
        flows.TryGetValue(flow, out var drawn) ? drawn : new DrawnFlow.NotDrawn();

    /// <summary>What became of a flow, from what the host found: its PNG, else the rule viewer's error (trimmed), else not drawn.</summary>
    public static DrawnFlow ReadDrawnFlow(FlowFiles files) =>
        files.HasPng
            ? new DrawnFlow.Drawn()
            : files.ErrorText.Match<DrawnFlow>(error => new DrawnFlow.Failed(error.Value.Trim()), _ => new DrawnFlow.NotDrawn());

    /// <summary>The files the host checks for each flow: its PNG and the rule viewer's error, relative to the output folder.</summary>
    public static ImmutableArray<(FlowToDraw Flow, string Png, string Error)> PlanFlowFiles(ImmutableArray<FlowToDraw> flows) =>
        [.. flows.Select(flow => (flow, FindFlowPath(flow.Domain, flow.Rule), FindFlowErrorPath(flow.Rule)))];

    /// <summary>What became of each flow, from what the host found of it.</summary>
    public static ImmutableDictionary<FlowToDraw, DrawnFlow> ReadDrawnFlows(ImmutableDictionary<FlowToDraw, FlowFiles> files) =>
        files.ToImmutableDictionary(pair => pair.Key, pair => ReadDrawnFlow(pair.Value));

    /// <summary>Where the rule viewer's error for a rule is kept, relative to the output folder.</summary>
    public static string FindFlowErrorPath(string rule) => $".flow-errors/{rule}.txt";

    /// <summary>The fixtures that alone picked a run's class; none when the class changed itself (changed wins) or was named.</summary>
    private static Optional<ImmutableArray<string>> FindFixtures(RunRecord record, ClassSelection selection) =>
        selection.Fixtures.TryGetValue(record.Class, out var paths) && !selection.Changed.Contains(record.Class)
            ? Optional.Some(paths)
            : Optional.None<ImmutableArray<string>>();

    /// <summary>The startup time to a tenth of a second; none when Home never showed.</summary>
    private static Optional<double> RoundStartup(RunRecord record) => record.HomeShownSeconds.Map(seconds => Math.Round(seconds, 1));

    private static string WriteSummaryLine(RunRecord record, ClassSelection selection)
    {
        var startup = RoundStartup(record).Match(some => $"startup {some.Value.ToString("F1", CultureInfo.InvariantCulture)} s", _ => "Home never shown");
        var why = FindFixtures(record, selection).Match(some => $"  (re-recorded: {string.Join(", ", some.Value)} changed)", _ => "");
        return $"  {(record.Passed ? "passed" : "FAILED")}  {startup,16}  {record.Domain}/{record.Gif}.gif{why}";
    }

    private static IEnumerable<string> FindRules(Optional<string> gherkin) =>
        CreateRuleLinePattern().Matches(gherkin.UnwrapOr("")).Select(found => found.Groups[1].Value);

    [GeneratedRegex(@"^\s*# Rule ([a-z0-9-]+):", RegexOptions.Multiline)]
    private static partial Regex CreateRuleLinePattern();
}
