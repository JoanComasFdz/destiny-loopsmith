using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loopsmith.Functional;

namespace Loopsmith.E2ETools.Core;

/// <summary>
/// The JSON the tool reads: record.sh's <c>scenarios.json</c>, and GitHub's answers through <c>gh api --paginate --slurp</c>
/// (one array per page), each to its records; and the JSON bodies it sends. A text that isn't the JSON expected is an error
/// saying what was being read.
/// </summary>
public static class JsonReading
{
    public static Result<ImmutableArray<ScenarioEntry>, string> ReadScenarios(string json) =>
        ParseArray(json, "scenarios.json").Bind(entries => ReadEach(entries, ReadScenario, "scenarios.json"));

    public static Result<ImmutableArray<PrComment>, string> ReadComments(string json) =>
        ReadPages(json, "the PR's comments", item => new PrComment(
            ReadNumber(item, "id"), ReadString(item, "node_id"), ReadString(item["user"] ?? new JsonObject(), "login"), ReadText(item, "body").UnwrapOr("")));

    public static Result<ImmutableArray<Release>, string> ReadReleases(string json) =>
        ReadPages(json, "the releases", item => new Release(ReadNumber(item, "id"), ReadString(item, "tag_name")));

    public static Result<ImmutableArray<ReleaseAsset>, string> ReadAssets(string json) =>
        ReadPages(json, "a release's assets", item => new ReleaseAsset(ReadNumber(item, "id"), ReadString(item, "name")));

    public static Result<ImmutableArray<int>, string> ReadPullNumbers(string json) =>
        ReadPages(json, "the open PRs", item => (int)ReadNumber(item, "number"));

    /// <summary>
    /// The node ids of these posts already hidden, from GraphQL's answer to <see cref="WriteHiddenQuery"/> (its nodes in the
    /// order asked); an error when the answer has no such nodes.
    /// </summary>
    public static Result<ImmutableHashSet<string>, string> ReadHidden(ImmutableArray<string> ids, string json)
    {
        try
        {
            if (JsonNode.Parse(json)?["data"]?["nodes"] is not JsonArray nodes)
            {
                return new Result<ImmutableHashSet<string>, string>.Error(json.Trim());
            }

            return new Result<ImmutableHashSet<string>, string>.Ok([.. ids.Zip(nodes)
                .Where(pair => pair.Second is JsonObject node && node["isMinimized"] is JsonValue minimized && minimized.TryGetValue(out bool hidden) && hidden)
                .Select(pair => pair.First)]);
        }
        catch (JsonException)
        {
            return new Result<ImmutableHashSet<string>, string>.Error(json.Trim());
        }
    }

    /// <summary>GraphQL's isMinimized of each of these comments (REST doesn't say), at most 100.</summary>
    public static string WriteHiddenQuery(ImmutableArray<string> ids) =>
        new JsonObject
        {
            ["query"] = "query($ids: [ID!]!) { nodes(ids: $ids) { ... on IssueComment { isMinimized } } }",
            ["variables"] = new JsonObject { ["ids"] = new JsonArray([.. ids.Select(id => JsonValue.Create(id))]) },
        }.ToJsonString();

    /// <summary>GraphQL's minimizeComment of a comment, as Outdated.</summary>
    public static string WriteHideMutation(string nodeId) =>
        new JsonObject
        {
            ["query"] = "mutation($id: ID!) { minimizeComment(input: {subjectId: $id, classifier: OUTDATED}) { minimizedComment { isMinimized } } }",
            ["variables"] = new JsonObject { ["id"] = nodeId },
        }.ToJsonString();

    /// <summary>A comment's body, as REST takes it to post or edit one.</summary>
    public static string WriteBody(string body) => new JsonObject { ["body"] = body }.ToJsonString();

    private static ScenarioEntry ReadScenario(JsonNode entry) =>
        new(
            ReadString(entry, "class"),
            ReadString(entry, "method"),
            ReadString(entry, "title"),
            ReadText(entry, "gherkin"),
            ReadText(entry, "screen"),
            ReadString(entry, "gif"),
            ReadText(entry, "mp4"),
            ReadText(entry, "flow"),
            ReadText(entry, "rule"),
            ReadText(entry, "flowError"),
            entry["passed"] is JsonValue passed && passed.TryGetValue(out bool hasPassed) ? hasPassed : throw new FormatException("no 'passed'"),
            ReadText(entry, "selected").UnwrapOr("") == "fixtures"
                ? new RunSelection.Fixtures([.. (entry["fixtures"] as JsonArray ?? []).OfType<JsonValue>().Select(path => path.GetValue<string>())])
                : new RunSelection.Changed());

    /// <summary>A text field the record needs; a <see cref="FormatException"/>, which ReadEach turns into an error, when it is missing.</summary>
    private static string ReadString(JsonNode item, string name) =>
        item[name] is JsonValue value && value.TryGetValue(out string? text) ? text : throw new FormatException($"no text '{name}'");

    private static long ReadNumber(JsonNode item, string name) =>
        item[name] is JsonValue value && value.TryGetValue(out long number) ? number : throw new FormatException($"no number '{name}'");

    /// <summary>A text field; none when it is missing, null or empty.</summary>
    private static Optional<string> ReadText(JsonNode entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue(out string? text) && text.Length > 0 ? Optional.Some(text) : Optional.None<string>();

    private static Result<ImmutableArray<T>, string> ReadPages<T>(string json, string what, Func<JsonNode, T> read) =>
        ParseArray(json, what).Bind(pages => ReadEach(
            [.. pages.SelectMany(page => page as JsonArray ?? [])],
            read,
            what));

    private static Result<JsonArray, string> ParseArray(string json, string what)
    {
        try
        {
            return JsonNode.Parse(json) is JsonArray array
                ? new Result<JsonArray, string>.Ok(array)
                : new Result<JsonArray, string>.Error($"{what} isn't a JSON array.");
        }
        catch (JsonException exception)
        {
            return new Result<JsonArray, string>.Error($"{what} isn't JSON: {exception.Message}");
        }
    }

    private static Result<ImmutableArray<T>, string> ReadEach<T>(IEnumerable<JsonNode?> items, Func<JsonNode, T> read, string what)
    {
        try
        {
            return new Result<ImmutableArray<T>, string>.Ok([.. items.OfType<JsonNode>().Select(read)]);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return new Result<ImmutableArray<T>, string>.Error($"{what} lacks a field it needs: {exception.Message}");
        }
    }
}
