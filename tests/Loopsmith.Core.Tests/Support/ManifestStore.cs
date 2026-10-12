using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.Support;

/// <summary>
/// What the web host (Hosting/BungieManifestJoining.cs, wwwroot/js/manifest.js) does for the manifest join, in memory:
/// holds each component's entries as raw JSON, answers Core's request and text search as JSON, and runs the join's
/// rounds through <see cref="LoopDesigning"/> as the host does.
/// </summary>
public sealed class ManifestStore(ImmutableDictionary<ManifestTable, ImmutableDictionary<ItemHash, string>> tables)
{
    private const int MaxRounds = 20;

    public static ManifestStore ReadDirectory(string directory) =>
        new(Enum.GetValues<ManifestTable>().ToImmutableDictionary(
            table => table,
            table => ReadTable(File.ReadAllText(Path.Combine(directory, $"{ManifestTransport.ToComponentName(table)}.json")))));

    /// <summary>A component's entries by hash, each compact, as Bungie writes it.</summary>
    public static ImmutableDictionary<ItemHash, string> ReadTable(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .Where(entry => ItemHash.TryFrom(uint.Parse(entry.Name, System.Globalization.CultureInfo.InvariantCulture)).IsSuccess)
            .ToImmutableDictionary(entry => ItemHash.From(uint.Parse(entry.Name, System.Globalization.CultureInfo.InvariantCulture)), entry => JsonSerializer.Serialize(entry.Value));
    }

    public int Rounds { get; private set; }

    /// <summary>Runs a join of what <paramref name="hashes"/> need to the end, answering each of its questions as the host does.</summary>
    public ManifestExcerpt Join(IEnumerable<ItemHash> hashes)
    {
        Result<ManifestJoinStep, string> step = new Result<ManifestJoinStep, string>.Ok(LoopDesigning.StartManifestJoin(hashes));
        for (var round = 0; step is Result<ManifestJoinStep, string>.Ok { Value: ManifestJoinStep.Ask ask } && round < MaxRounds; round++)
        {
            var (entries, found) = Answer(ask.Query);
            step = LoopDesigning.ContinueManifestJoin(ask, entries, found);
        }

        return LoopDesigning.ToJoinedExcerpt(step).Match(ok => ok.Value, error => throw new InvalidOperationException($"The join didn't finish in {MaxRounds} rounds: {error.Failure}"));
    }

    /// <summary>The host's two answers to a question: the entries asked for, and the entries holding each needle.</summary>
    public (string Entries, string Found) Answer(ManifestQuery query)
    {
        Rounds++;
        var request = LoopDesigning.ToManifestRequest(query);
        var search = LoopDesigning.ToManifestPlugSearch(query);
        var entries = request.ToDictionary(component => component.Key, component => ReadEntries(ToTable(component.Key), component.Value));
        var found = search.Needles.ToDictionary(needle => needle, needle => FindContaining(ToTable(search.Component), needle));
        return (JsonSerializer.Serialize(entries), JsonSerializer.Serialize(found));
    }

    /// <summary>Runs a listing of the game's weapons and armor to the end, answering each of its questions as the host does.</summary>
    public GameItems ListGameItems()
    {
        Result<ManifestListStep, string> step = new Result<ManifestListStep, string>.Ok(LoopDesigning.StartGameItemListing());
        for (var round = 0; step is Result<ManifestListStep, string>.Ok { Value: ManifestListStep.Ask ask } && round < MaxRounds; round++)
        {
            var (entries, outlines, found) = AnswerList(ask.Query);
            step = LoopDesigning.ContinueGameItemListing(ask, entries, outlines, found);
        }

        return LoopDesigning.ToListedGameItems(step).Match(ok => ok.Value, error => throw new InvalidOperationException($"The listing didn't finish in {MaxRounds} rounds: {error.Failure}"));
    }

    /// <summary>The host's three answers to a listing's question: the entries read whole, those cut down to its outline paths, and the entries holding each needle.</summary>
    public (string Entries, string Outlines, string Found) AnswerList(ManifestListQuery query)
    {
        Rounds++;
        var request = LoopDesigning.ToGameListRequest(query);
        var outlineRequest = LoopDesigning.ToGameListOutlineRequest(query);
        var search = LoopDesigning.ToGameListSearch(query);
        var entries = request.ToDictionary(component => component.Key, component => ReadEntries(ToTable(component.Key), component.Value));
        var outlines = outlineRequest.ToDictionary(component => component.Key, component => ReadEntries(ToTable(component.Key), component.Value)
            .ToDictionary(entry => entry.Key, entry => OutlineEntry(entry.Value, query.OutlinePaths)));
        var found = search.Needles.ToDictionary(needle => needle, needle => FindContaining(ToTable(search.Component), needle));
        return (JsonSerializer.Serialize(entries), JsonSerializer.Serialize(outlines), JsonSerializer.Serialize(found));
    }

    /// <summary>
    /// An entry cut down to <paramref name="paths"/> as manifest.js cuts it: each path's value kept where it is (a path of
    /// property names; <c>*</c>: every element of an array, in place, null where it has nothing of the path).
    /// </summary>
    private static string OutlineEntry(string json, ImmutableArray<string> paths)
    {
        var root = JsonNode.Parse(json);
        var outline = KeepPaths(root, [.. paths.Select(path => path.Split('.'))]) ?? new JsonObject();
        return outline.ToJsonString();
    }

    private static JsonNode? KeepPaths(JsonNode? node, ImmutableArray<string[]> paths) =>
        paths.Any(path => path.Length == 0) ? node?.DeepClone()
        : node is JsonObject obj ? ToObjectOrNull(paths
            .GroupBy(path => path[0], StringComparer.Ordinal)
            .Where(group => group.Key != "*" && obj.ContainsKey(group.Key))
            .Select(group => (group.Key, Kept: KeepPaths(obj[group.Key], [.. group.Select(path => path[1..])])))
            .Where(child => child.Kept is not null))
        : node is JsonArray array && paths.Any(path => path[0] == "*") ? new JsonArray([.. array.Select(element => KeepPaths(element, [.. paths.Where(path => path[0] == "*").Select(path => path[1..])]))])
        : null;

    private static JsonObject? ToObjectOrNull(IEnumerable<(string Key, JsonNode? Kept)> children)
    {
        var kept = children.ToArray();
        return kept.Length == 0 ? null : new JsonObject(kept.Select(child => KeyValuePair.Create(child.Key, child.Kept)));
    }

    private Dictionary<string, string> ReadEntries(ManifestTable table, ImmutableArray<ItemHash> hashes) =>
        (hashes.IsEmpty ? tables[table].Keys : hashes.Where(tables[table].ContainsKey))
            .ToDictionary(hash => hash.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), hash => tables[table][hash]);

    private uint[] FindContaining(ManifestTable table, string needle) =>
        [.. tables[table].Where(entry => entry.Value.Contains(needle, StringComparison.Ordinal)).Select(entry => entry.Key.Value).Order()];

    private static ManifestTable ToTable(string component) =>
        Enum.GetValues<ManifestTable>().Single(table => ManifestTransport.ToComponentName(table) == component);
}
