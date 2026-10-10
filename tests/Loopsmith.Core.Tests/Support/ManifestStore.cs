using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.Tests.Support;

/// <summary>
/// What the web host does for the manifest join, in memory: holds each component's entries as raw JSON and answers a
/// <see cref="ManifestQuery"/> — entries by hash, whole tables, the plug items of a plug category.
/// </summary>
public sealed class ManifestStore(ImmutableDictionary<ManifestTable, ImmutableDictionary<uint, string>> tables)
{
    public static ManifestStore ReadDirectory(string directory) =>
        new(Enum.GetValues<ManifestTable>().ToImmutableDictionary(
            table => table,
            table => ReadTable(File.ReadAllText(Path.Combine(directory, $"Destiny{table}Definition.json")))));

    public static ImmutableDictionary<uint, string> ReadTable(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToImmutableDictionary(entry => uint.Parse(entry.Name, System.Globalization.CultureInfo.InvariantCulture), entry => JsonSerializer.Serialize(entry.Value));   // compact, as Bungie writes it
    }

    public int Rounds { get; private set; }

    public ManifestAnswer Answer(ManifestQuery query)
    {
        Rounds++;
        var entries = query.Entries
            .Where(key => tables[key.Table].ContainsKey(key.Hash))
            .Select(key => KeyValuePair.Create(key, tables[key.Table][key.Hash]))
            .Concat(query.WholeTables.SelectMany(table => tables[table].Select(entry => KeyValuePair.Create(new ManifestEntryKey(table, entry.Key), entry.Value))))
            .GroupBy(entry => entry.Key)
            .ToImmutableDictionary(group => group.Key, group => group.First().Value);
        var plugs = query.PlugCategories.ToImmutableDictionary(
            category => category,
            category => tables[ManifestTable.InventoryItem]
                .Where(entry => entry.Value.Contains($"\"plugCategoryIdentifier\":{JsonSerializer.Serialize(category)}", StringComparison.Ordinal))
                .Select(entry => entry.Key)
                .Order()
                .ToImmutableArray());
        return new ManifestAnswer(entries, plugs);
    }

    /// <summary>Runs a join to the end, answering each of its questions.</summary>
    public ManifestExcerpt Join(ManifestJoinStep start, Func<ManifestJoinProgress, ManifestAnswer, ManifestJoinStep> next)
    {
        var step = start;
        for (var round = 0; step is ManifestJoinStep.Ask ask && round < 20; round++)
        {
            step = next(ask.Progress, Answer(ask.Query));
        }

        return step is ManifestJoinStep.Done done ? done.Excerpt : throw new InvalidOperationException("The join didn't finish in 20 rounds.");
    }
}
