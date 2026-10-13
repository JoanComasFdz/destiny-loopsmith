using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;

namespace Loopsmith.Core.Tests.Support;

/// <summary>
/// What the web host (Hosting/BungieManifestJoining.cs, wwwroot/js/manifest.js) does for the manifest join and the
/// listing of every weapon and armor piece, in memory: holds each component's entries as raw JSON, answers Core's
/// requests, text searches and parts queries as JSON, and runs their rounds through <see cref="LoopDesigning"/> as the
/// host does.
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

    /// <summary>The round each of a listing's questions said it was (<c>LoopDesigning.ToListingRound</c>).</summary>
    public ImmutableArray<int> ListingRounds { get; private set; } = [];

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
        var found = search.Needles.ToDictionary(needle => needle, needle => FindContaining(ToTable(search.Component), needle));
        return (AnswerEntries(request), JsonSerializer.Serialize(found));
    }

    /// <summary>Runs a listing of every weapon and armor piece to the end, answering each of its questions as the host does.</summary>
    public ItemListing List()
    {
        Result<ListingStep, string> step = new Result<ListingStep, string>.Ok(LoopDesigning.StartListing());
        for (var round = 0; step is Result<ListingStep, string>.Ok { Value: ListingStep.Ask ask } && round < MaxRounds; round++)
        {
            Rounds++;
            ListingRounds = ListingRounds.Add(LoopDesigning.ToListingRound(ask));
            step = LoopDesigning.ContinueListing(ask, AnswerEntries(ask.Query.Entries), AnswerParts(ask.Query.Parts));
        }

        return LoopDesigning.ToItemListing(step).Match(ok => ok.Value, error => throw new InvalidOperationException($"The listing didn't finish in {MaxRounds} rounds: {error.Failure}"));
    }

    /// <summary>The entries asked for, per component (an empty list: the whole component).</summary>
    public string AnswerEntries(ImmutableDictionary<string, ImmutableArray<ItemHash>> request) =>
        JsonSerializer.Serialize(request.ToDictionary(component => component.Key, component => ReadEntries(ToTable(component.Key), component.Value)));

    /// <summary>The host's answer to a parts query: <c>{ "value": { "hash": { …parts… } } }</c>, as manifest.js's <c>readEntryParts</c>.</summary>
    public string AnswerParts(ManifestPartsQuery query)
    {
        var groups = new JsonObject();
        if (query.Paths.IsEmpty)
        {
            return groups.ToJsonString();
        }

        var pattern = new Regex($"\"{Regex.Escape(query.Key)}\":([^,}}\\]]*)", RegexOptions.CultureInvariant);
        foreach (var (hash, json) in tables[ToTable(query.Component)].OrderBy(entry => entry.Key.Value))
        {
            foreach (var value in pattern.Matches(json).Select(match => match.Groups[1].Value).Where(query.Paths.ContainsKey).Distinct())
            {
                var group = groups[value] as JsonObject ?? new JsonObject();
                groups[value] = group;
                group[hash.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Cut(JsonNode.Parse(json), query.Paths[value]) ?? new JsonObject();
            }
        }

        return groups.ToJsonString();
    }

    /// <summary>
    /// A value cut down to the paths it has, as manifest.js cuts it: an object keeps the properties the paths name (one
    /// that keeps none is left out), an array maps <c>*</c> over its elements (one that keeps nothing is null), and an
    /// empty path keeps the value whole.
    /// </summary>
    private static JsonNode? Cut(JsonNode? value, IEnumerable<ImmutableArray<string>> paths)
    {
        var all = paths.ToImmutableArray();
        if (all.Any(path => path.IsEmpty))
        {
            return value?.DeepClone();
        }

        if (value is JsonArray array)
        {
            var rest = all.Where(path => path[0] == "*").Select(path => path[1..]).ToImmutableArray();
            return rest.IsEmpty ? null : new JsonArray([.. array.Select(element => Cut(element, rest))]);
        }

        if (value is not JsonObject entry)
        {
            return null;
        }

        var parts = new JsonObject();
        foreach (var named in all.Where(path => path[0] != "*").GroupBy(path => path[0]))
        {
            if (entry.TryGetPropertyValue(named.Key, out var child) && Cut(child, named.Select(path => path[1..])) is { } kept)
            {
                parts[named.Key] = kept;
            }
        }

        return parts.Count > 0 ? parts : null;
    }

    private Dictionary<string, string> ReadEntries(ManifestTable table, ImmutableArray<ItemHash> hashes) =>
        (hashes.IsEmpty ? tables[table].Keys : hashes.Where(tables[table].ContainsKey))
            .ToDictionary(hash => hash.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), hash => tables[table][hash]);

    private uint[] FindContaining(ManifestTable table, string needle) =>
        [.. tables[table].Where(entry => entry.Value.Contains(needle, StringComparison.Ordinal)).Select(entry => entry.Key.Value).Order()];

    internal static ManifestTable ToTable(string component) =>
        Enum.GetValues<ManifestTable>().Single(table => ManifestTransport.ToComponentName(table) == component);
}
