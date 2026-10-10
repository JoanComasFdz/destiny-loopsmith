using System.Collections.Immutable;
using System.Text.Json;
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

    private Dictionary<string, string> ReadEntries(ManifestTable table, ImmutableArray<ItemHash> hashes) =>
        (hashes.IsEmpty ? tables[table].Keys : hashes.Where(tables[table].ContainsKey))
            .ToDictionary(hash => hash.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), hash => tables[table][hash]);

    private uint[] FindContaining(ManifestTable table, string needle) =>
        [.. tables[table].Where(entry => entry.Value.Contains(needle, StringComparison.Ordinal)).Select(entry => entry.Key.Value).Order()];

    private static ManifestTable ToTable(string component) =>
        Enum.GetValues<ManifestTable>().Single(table => ManifestTransport.ToComponentName(table) == component);
}
