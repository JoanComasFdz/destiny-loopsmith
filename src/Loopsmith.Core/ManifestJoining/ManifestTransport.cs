using System.Collections.Immutable;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: how the join talks to the host holding Bungie's manifest — the components' URLs (the manifest index's paths,
/// or a pinned version's), a query as the host's request and text search, the host's JSON answer as a
/// <see cref="ManifestAnswer"/> (or why it doesn't read), and the join's end as the excerpt.
/// </summary>
public static class ManifestTransport
{
    private const string Host = "https://www.bungie.net";
    private const string ComponentFolder = "/common/destiny2_content/json/";
    private const string PlugCategoryField = "\"plugCategoryIdentifier\":";

    /// <summary>The manifest's index: the paths of the current version's components.</summary>
    public const string IndexUrl = Host + "/Platform/Destiny2/Manifest/";

    /// <summary>A component's file name stem: <c>DestinyInventoryItemDefinition</c>.</summary>
    public static string ToComponentName(ManifestTable table) => $"Destiny{table}Definition";

    /// <summary>
    /// Each component's URL, by component name: the manifest index's paths (<c>Response.jsonWorldComponentContentPaths.en</c>)
    /// when it has every one (each on bungie.net's component folder), else the pinned content id's.
    /// </summary>
    public static ImmutableDictionary<string, string> ChooseUrls(Optional<string> indexJson, string pinnedContentId)
    {
        var pinned = Enum.GetValues<ManifestTable>().ToImmutableDictionary(ToComponentName, table => $"{Host}{ComponentFolder}en/{ToComponentName(table)}-{pinnedContentId}.json");
        var fromIndex = indexJson.Bind(ReadIndexUrls);
        return fromIndex.Match(urls => urls.Value.Count == pinned.Count ? urls.Value : pinned, _ => pinned);
    }

    /// <summary>What the host is asked for: each component's hashes (an empty list: the whole component).</summary>
    public static ImmutableDictionary<string, ImmutableArray<ItemHash>> ToEntryRequest(ManifestQuery query) =>
        query.Entries
            .GroupBy(key => key.Table)
            .Select(group => (Table: group.Key, Hashes: group.Select(key => key.Hash).ToImmutableArray()))
            .Concat(query.WholeTables.Select(table => (Table: table, Hashes: ImmutableArray<ItemHash>.Empty)))
            .GroupBy(entry => entry.Table)
            .ToImmutableDictionary(
                group => ToComponentName(group.Key),
                group => group.Any(entry => entry.Hashes.IsEmpty) ? ImmutableArray<ItemHash>.Empty : group.SelectMany(entry => entry.Hashes).ToImmutableArray());

    /// <summary>
    /// The search for a query's plug categories: the item component's entries whose JSON holds
    /// <c>"plugCategoryIdentifier":"&lt;category&gt;"</c> (compact, as Bungie writes it), one needle per category.
    /// </summary>
    public static ManifestTextSearch ToPlugSearch(ManifestQuery query) =>
        new(ToComponentName(ManifestTable.InventoryItem), [.. query.PlugCategories.Select(ToPlugNeedle)]);

    /// <summary>
    /// The host's answer to <paramref name="query"/>: <paramref name="entriesJson"/> is
    /// <c>{ "DestinyXDefinition": { "hash": { …entry… } } }</c> (each entry's JSON as the manifest has it),
    /// <paramref name="foundJson"/> <c>{ "needle": [hashes] }</c> for its <see cref="ToPlugSearch"/>. A part that isn't
    /// a hash and an entry is left out; an answer that doesn't parse is why it doesn't read.
    /// </summary>
    public static Result<ManifestAnswer, string> ReadAnswer(ManifestQuery query, string entriesJson, string foundJson) =>
        ParseDocument(entriesJson)
            .MapError(problem => $"its entries don't read: {problem}")
            .Bind(entries => ParseDocument(foundJson)
                .MapError(problem => $"its search doesn't read: {problem}")
                .Map(found => new ManifestAnswer(ReadEntries(entries), ReadPlugs(query, found))));

    /// <summary>What the host reads whole for a listing's question: its entries and whole tables, per component (as <see cref="ToEntryRequest"/>).</summary>
    public static ImmutableDictionary<string, ImmutableArray<ItemHash>> ToListEntryRequest(ManifestListQuery query) =>
        ToEntryRequest(new ManifestQuery(query.Entries, query.WholeTables, []));

    /// <summary>The item entries the host reads cut down to the query's outline paths; none when it asks for none (an empty list would be the whole table).</summary>
    public static ImmutableDictionary<string, ImmutableArray<ItemHash>> ToOutlineRequest(ManifestListQuery query) =>
        query.Outlines.IsEmpty
            ? ImmutableDictionary<string, ImmutableArray<ItemHash>>.Empty
            : ImmutableDictionary<string, ImmutableArray<ItemHash>>.Empty.Add(ToComponentName(ManifestTable.InventoryItem), query.Outlines);

    /// <summary>The search for a listing's needles in the item component.</summary>
    public static ManifestTextSearch ToListSearch(ManifestListQuery query) =>
        new(ToComponentName(ManifestTable.InventoryItem), query.Needles);

    /// <summary>
    /// The host's answer to a listing's <paramref name="query"/>: <paramref name="entriesJson"/> and
    /// <paramref name="outlinesJson"/> as a join's entries (<see cref="ReadAnswer"/>; the outlines cut down), and
    /// <paramref name="foundJson"/> <c>{ "needle": [hashes] }</c>; an answer that doesn't parse is why it doesn't read.
    /// </summary>
    public static Result<ManifestListAnswer, string> ReadListAnswer(ManifestListQuery query, string entriesJson, string outlinesJson, string foundJson) =>
        ParseDocument(entriesJson)
            .MapError(problem => $"its entries don't read: {problem}")
            .Bind(entries => ParseDocument(outlinesJson)
                .MapError(problem => $"its outlines don't read: {problem}")
                .Bind(outlines => ParseDocument(foundJson)
                    .MapError(problem => $"its search doesn't read: {problem}")
                    .Map(found => new ManifestListAnswer(
                        new ManifestAnswer(ReadEntries(entries).SetItems(ReadEntries(outlines)), ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>>.Empty),
                        ReadFound(query.Needles, found)))));

    /// <summary>The game's weapons and armor a finished listing found; why there are none when reading the manifest failed or didn't finish.</summary>
    public static Result<GameItems, string> ToListedGameItems(Result<ManifestListStep, string> step) =>
        step.Bind(reached => reached.Match<Result<GameItems, string>>(
            _ => new Result<GameItems, string>.Error("its reading didn't finish"),
            done => new Result<GameItems, string>.Ok(done.Items)));

    /// <summary>The excerpt a finished join built; why there is none when reading the manifest failed or didn't finish.</summary>
    public static Result<ManifestExcerpt, string> ToJoinedExcerpt(Result<ManifestJoinStep, string> step) =>
        step.Bind(reached => reached.Match<Result<ManifestExcerpt, string>>(
            _ => new Result<ManifestExcerpt, string>.Error("its reading didn't finish"),
            done => new Result<ManifestExcerpt, string>.Ok(done.Excerpt)));

    private static ImmutableDictionary<ManifestEntryKey, JsonElement> ReadEntries(JsonElement root) =>
        Enum.GetValues<ManifestTable>()
            .SelectMany(table => root.ReadPath(ToComponentName(table)).Match(
                entries => entries.Value.ValueKind == JsonValueKind.Object ? entries.Value.EnumerateObject().SelectMany(entry => ReadEntry(table, entry)) : [],
                _ => []))
            .ToImmutableDictionary();

    /// <summary>
    /// An entry of the answer, parsed once: an object as it is (the host's answer holds the entries' own JSON), or a
    /// string of JSON parsed here; one that doesn't parse is as if the manifest lacked it.
    /// </summary>
    private static IEnumerable<KeyValuePair<ManifestEntryKey, JsonElement>> ReadEntry(ManifestTable table, JsonProperty entry) =>
        ReadHashName(entry.Name).Match(
            hash => entry.Value.ValueKind == JsonValueKind.String
                ? ToSequence(Optional.FromNullable(entry.Value.GetString()).Bind(ReadEntryText)).Select(parsed => KeyValuePair.Create(new ManifestEntryKey(table, hash.Value), parsed))
                : entry.Value.ValueKind == JsonValueKind.Object ? [KeyValuePair.Create(new ManifestEntryKey(table, hash.Value), entry.Value)]
                : [],
            _ => []);

    private static Optional<JsonElement> ReadEntryText(string json) =>
        ParseDocument(json).Match(parsed => Optional.Some(parsed.Value), _ => Optional.None<JsonElement>());

    private static ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>> ReadPlugs(ManifestQuery query, JsonElement found) =>
        query.PlugCategories
            .SelectMany(category => found.ReadPath(ToPlugNeedle(category)).Match(
                hashes => new[] { KeyValuePair.Create(category, hashes.Value.ValueKind == JsonValueKind.Array ? hashes.Value.EnumerateArray().SelectMany(hash => ToSequence(hash.ToItemHash())).ToImmutableArray() : []) },
                _ => []))
            .ToImmutableDictionary();

    private static ImmutableDictionary<string, ImmutableArray<ItemHash>> ReadFound(ImmutableArray<string> needles, JsonElement found) =>
        needles
            .Distinct(StringComparer.Ordinal)
            .SelectMany(needle => found.ReadPath(needle).Match(
                hashes => new[] { KeyValuePair.Create(needle, hashes.Value.ValueKind == JsonValueKind.Array ? hashes.Value.EnumerateArray().SelectMany(hash => ToSequence(hash.ToItemHash())).ToImmutableArray() : []) },
                _ => []))
            .ToImmutableDictionary(StringComparer.Ordinal);

    private static string ToPlugNeedle(PlugCategoryId category) =>
        PlugCategoryField + "\"" + JsonEncodedText.Encode(category.Value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).Value + "\"";

    private static Optional<ImmutableDictionary<string, string>> ReadIndexUrls(string json) =>
        ParseDocument(json).Match(
            root => root.Value.ReadPath("Response", "jsonWorldComponentContentPaths", "en").Map(paths =>
                Enum.GetValues<ManifestTable>()
                    .SelectMany(table => ToSequence(paths.ReadString(ToComponentName(table)).Bind(ToComponentUrl)).Select(url => KeyValuePair.Create(ToComponentName(table), url)))
                    .ToImmutableDictionary()),
            _ => Optional.None<ImmutableDictionary<string, string>>());

    /// <summary>A path the index gives, on bungie.net, when it is in the component folder (anything else is not a component).</summary>
    private static Optional<string> ToComponentUrl(string path) =>
        path.StartsWith(ComponentFolder, StringComparison.Ordinal) && Uri.IsWellFormedUriString(path, UriKind.Relative)
            ? Optional.Some(Host + path)
            : Optional.None<string>();

    private static Optional<ItemHash> ReadHashName(string name) =>
        uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var hash) ? ToItemHash(hash) : Optional.None<ItemHash>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}
