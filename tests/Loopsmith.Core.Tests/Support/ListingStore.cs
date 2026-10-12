using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.Support;

/// <summary>
/// What the web host (Hosting/BungieManifestJoining.cs, wwwroot/js/manifest.js) does for the listing of every weapon and
/// armor piece, in memory: answers Core's table request and parts query as JSON (the entries holding
/// <c>"key":value</c> for one of the values, each cut down to the paths, grouped by the value) and runs the listing
/// through <see cref="LoopDesigning"/> as the host does.
/// </summary>
public static class ListingStore
{
    /// <summary>The listing of <paramref name="liteItems"/> (the lite item table's entries), with these buckets and sets (whole components, as Bungie's JSON).</summary>
    public static ItemListing List(string bucketsJson, string setsJson, ImmutableDictionary<ItemHash, string> liteItems)
    {
        var request = LoopDesigning.ToListingTableRequest();
        var tablesJson = $$"""{ "{{request.Keys.Single(name => name.Contains("Bucket", StringComparison.Ordinal))}}": {{bucketsJson}}, "{{request.Keys.Single(name => name.Contains("Set", StringComparison.Ordinal))}}": {{setsJson}} }""";
        var tables = LoopDesigning.ReadListingTables(tablesJson).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var query = LoopDesigning.ToListingPartsQuery(tables);
        var parts = AnswerParts(query, liteItems);
        return LoopDesigning.ReadItemListing(tables, parts).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
    }

    /// <summary>The host's answer to a parts query: <c>{ "value": { "hash": { …parts… } } }</c>.</summary>
    public static string AnswerParts(ManifestPartsQuery query, ImmutableDictionary<ItemHash, string> entries)
    {
        var values = query.Values.ToImmutableHashSet();
        var pattern = new Regex($"\"{Regex.Escape(query.Key)}\":([^,}}\\]]*)", RegexOptions.CultureInvariant);
        var groups = new JsonObject();
        foreach (var (hash, json) in entries.OrderBy(entry => entry.Key.Value))
        {
            foreach (var value in pattern.Matches(json).Select(match => match.Groups[1].Value).Where(values.Contains).Distinct())
            {
                var group = groups[value] as JsonObject ?? new JsonObject();
                groups[value] = group;
                group[hash.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Cut(JsonNode.Parse(json)!, query.Paths);
            }
        }

        return groups.ToJsonString();
    }

    /// <summary>An entry cut down to the paths it has, nested as they were.</summary>
    private static JsonObject Cut(JsonNode entry, ImmutableArray<ImmutableArray<string>> paths)
    {
        var cut = new JsonObject();
        foreach (var path in paths)
        {
            var found = path.Aggregate((JsonNode?)entry, (node, name) => node is JsonObject parent && parent.TryGetPropertyValue(name, out var next) ? next : null);
            if (found is null)
            {
                continue;
            }

            var parent = path[..^1].Aggregate(cut, (node, name) => node[name] as JsonObject ?? (JsonObject)(node[name] = new JsonObject()));
            parent[path[^1]] = found.DeepClone();
        }

        return cut;
    }

    /// <summary>A component's entries by hash, each compact (as <see cref="ManifestStore.ReadTable"/>).</summary>
    public static ImmutableDictionary<ItemHash, string> ReadTable(string json) =>
        ManifestStore.ReadTable(json);

    /// <summary>The components of a downloaded manifest the listing reads, when <paramref name="directory"/> has its lite item table.</summary>
    public static bool HasLiteItems(string directory) =>
        File.Exists(Path.Combine(directory, $"{ManifestListing.LiteItemComponent}.json"));

    /// <summary>The listing of a downloaded manifest (<c>LOOPSMITH_MANIFEST_DIR</c>, with its lite item table).</summary>
    public static ItemListing ListDirectory(string directory) =>
        List(
            File.ReadAllText(Path.Combine(directory, "DestinyInventoryBucketDefinition.json")),
            File.ReadAllText(Path.Combine(directory, "DestinyEquipableItemSetDefinition.json")),
            ReadTable(File.ReadAllText(Path.Combine(directory, $"{ManifestListing.LiteItemComponent}.json"))));
}
