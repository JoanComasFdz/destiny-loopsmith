using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: how the join talks to the host holding Bungie's manifest — the components' paths (the manifest index's, or a
/// pinned version's), a query as the host's request, the host's JSON answer as a <see cref="ManifestAnswer"/>. The
/// answer is the slice boundary for <see cref="JsonDocument"/>: malformed parts are left out, once, here.
/// </summary>
public static class ManifestTransport
{
    private const string ComponentFolder = "/common/destiny2_content/json/en/";

    /// <summary>A component's file name stem: <c>DestinyInventoryItemDefinition</c>.</summary>
    public static string ToComponentName(ManifestTable table) => $"Destiny{table}Definition";

    /// <summary>
    /// Each component's path (on bungie.net): the manifest index's (<c>Response.jsonWorldComponentContentPaths.en</c>) when
    /// it has every one, else the pinned content id's.
    /// </summary>
    public static ImmutableDictionary<ManifestTable, string> ChoosePaths(Optional<string> indexJson, string pinnedContentId)
    {
        var pinned = Enum.GetValues<ManifestTable>().ToImmutableDictionary(table => table, table => $"{ComponentFolder}{ToComponentName(table)}-{pinnedContentId}.json");
        var fromIndex = indexJson.Bind(ReadIndexPaths);
        return fromIndex.Match(paths => paths.Value.Count == pinned.Count ? paths.Value : pinned, _ => pinned);
    }

    /// <summary>What the host is asked for: each table's hashes (an empty list: the whole table).</summary>
    public static ImmutableDictionary<string, ImmutableArray<uint>> ToEntryRequest(ManifestQuery query) =>
        query.Entries
            .GroupBy(key => key.Table)
            .Select(group => (Table: group.Key, Hashes: group.Select(key => key.Hash).ToImmutableArray()))
            .Concat(query.WholeTables.Select(table => (Table: table, Hashes: ImmutableArray<uint>.Empty)))
            .GroupBy(entry => entry.Table)
            .ToImmutableDictionary(
                group => ToComponentName(group.Key),
                group => group.Any(entry => entry.Hashes.IsEmpty) ? ImmutableArray<uint>.Empty : group.SelectMany(entry => entry.Hashes).ToImmutableArray());

    /// <summary>
    /// The host's answer: <paramref name="entriesJson"/> is <c>{ "DestinyXDefinition": { "hash": { …entry… } } }</c> (each
    /// entry's JSON as the manifest has it), <paramref name="plugsJson"/> <c>{ "category": [hashes] }</c>.
    /// </summary>
    public static ManifestAnswer ReadAnswer(string entriesJson, string plugsJson) =>
        new(ReadEntries(entriesJson), ReadPlugs(plugsJson));

    private static ImmutableDictionary<ManifestEntryKey, string> ReadEntries(string json) =>
        ParseRoot(json).Match(
            root => Enum.GetValues<ManifestTable>()
                .SelectMany(table => root.Value.TryGetProperty(ToComponentName(table), out var entries) && entries.ValueKind == JsonValueKind.Object
                    ? entries.EnumerateObject().SelectMany(entry => ReadEntry(table, entry))
                    : [])
                .ToImmutableDictionary(),
            _ => ImmutableDictionary<ManifestEntryKey, string>.Empty);

    private static IEnumerable<KeyValuePair<ManifestEntryKey, string>> ReadEntry(ManifestTable table, JsonProperty entry) =>
        uint.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var hash) && entry.Value.ValueKind is JsonValueKind.Object or JsonValueKind.String
            ? [KeyValuePair.Create(new ManifestEntryKey(table, hash), entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() ?? "" : entry.Value.GetRawText())]
            : [];

    private static ImmutableDictionary<string, ImmutableArray<uint>> ReadPlugs(string json) =>
        ParseRoot(json).Match(
            root => root.Value.EnumerateObject()
                .Where(category => category.Value.ValueKind == JsonValueKind.Array)
                .ToImmutableDictionary(
                    category => category.Name,
                    category => category.Value.EnumerateArray()
                        .SelectMany(hash => hash.ValueKind == JsonValueKind.Number && hash.TryGetUInt32(out var value) ? new[] { value } : [])
                        .ToImmutableArray()),
            _ => ImmutableDictionary<string, ImmutableArray<uint>>.Empty);

    private static Optional<ImmutableDictionary<ManifestTable, string>> ReadIndexPaths(string json) =>
        ParseRoot(json).Bind(root => root.ReadPath("Response", "jsonWorldComponentContentPaths", "en").Map(paths =>
            Enum.GetValues<ManifestTable>()
                .SelectMany(table => paths.ReadString(ToComponentName(table)).Match(path => new[] { KeyValuePair.Create(table, path.Value) }, _ => []))
                .ToImmutableDictionary()));

    private static Optional<JsonElement> ParseRoot(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Optional.Some(document.RootElement.Clone()) : Optional.None<JsonElement>();
        }
        catch (JsonException)
        {
            return Optional.None<JsonElement>();
        }
    }
}
