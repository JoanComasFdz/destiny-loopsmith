using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Functional;
using static Loopsmith.ManifestTools.Core.DefinitionReading;

namespace Loopsmith.ManifestTools.Core;

/// <summary>
/// The plugs that change an armor stat (<c>statPlugs</c>), for adding up a build's stats from a player's own armor the way
/// DIM does (docs/loop-format.md, "Stats").
/// </summary>
public static class StatPlugReading
{
    /// <summary>The six armor stats by their hash, in the excerpt's order.</summary>
    public static readonly ImmutableArray<(long Hash, string Stat)> ArmorStats =
    [
        (2996146975, "weapons"), (392767087, "health"), (1943323491, "class"), (1735777505, "grenade"), (144602215, "super"), (4244567218, "melee"),
    ];

    private static readonly FrozenDictionary<long, string> StatOfHash = ArmorStats.ToFrozenDictionary(pair => pair.Hash, pair => pair.Stat);

    /// <summary>Plug category hashes (DIM's PlugCategoryHashes) → the kind of stat plug.</summary>
    private static readonly FrozenDictionary<long, string> StatPlugKinds = new Dictionary<long, string>
    {
        [748854354] = "base", [1744546145] = "base", [2198080209] = "masterwork", [3481777685] = "tuning",
        [2487827355] = "mod", [3773173029] = "mod", [2912171003] = "mod", [3422420680] = "mod", [1526202480] = "mod",
        [2111701510] = "mod", [912441879] = "mod",
    }.ToFrozenDictionary();

    private const long ArmorStatsCategory = 748854354;

    private const long BalancedTuning = 3122197216;

    /// <summary>Echo of Persistence, Spark of Focus: -10 to the class's own ability regeneration stat.</summary>
    private static readonly FrozenSet<long> ClassStatPlugs = FrozenSet.Create(2272984671L, 1727069360L);

    private static readonly FrozenDictionary<string, string> ClassOfStat =
        new Dictionary<string, string> { ["weapons"] = "hunter", ["health"] = "titan", ["class"] = "warlock" }.ToFrozenDictionary();

    /// <summary>The armor stats a plug changes, each with its value as the manifest writes it, in the manifest's order.</summary>
    public static ImmutableArray<(string Stat, string Value)> ReadPlugStats(JsonElement item) =>
        [.. ReadArray(item, "investmentStats")
            .Select(stat => (Hash: ReadNumber(stat, "statTypeHash"), Value: ReadField(stat, "value").Match(value => value.Value.GetRawText(), _ => "0")))
            .Where(stat => stat.Hash is Optional<long>.Some found && StatOfHash.ContainsKey(found.Value))
            .Select(stat => (StatOfHash[((Optional<long>.Some)stat.Hash).Value], stat.Value))];

    /// <summary>
    /// A plug's kind for adding up stats, or none: armor stats, masterworks and tuning always (an empty one marks its
    /// socket), mods only with a stat, subclass plugs (fragments) with one.
    /// </summary>
    public static Optional<string> ClassifyStatPlug(JsonElement item)
    {
        var plug = ReadObject(item, "plug").UnwrapOr(EmptyObject);
        var categoryHash = ReadNumber(plug, "plugCategoryHash");
        var kind = categoryHash.Bind(hash => StatPlugKinds.TryGetValue(hash, out var found) ? Optional.Some(found) : Optional.None<string>());
        var stats = ReadPlugStats(item);
        return kind switch
        {
            Optional<string>.Some known when known.Value is "masterwork" or "tuning" || !stats.IsEmpty =>
                known.Value != "base" || categoryHash.UnwrapOr(0) == ArmorStatsCategory || !stats.IsEmpty ? kind : Optional.None<string>(),
            _ => ReadString(plug, "plugCategoryIdentifier").UnwrapOr("").EndsWith(".fragments", StringComparison.Ordinal) && !stats.IsEmpty
                ? Optional.Some("subclass")
                : Optional.None<string>(),
        };
    }

    public static string WriteStatPlugEntry(long hash, JsonElement item, string kind)
    {
        var stats = ReadPlugStats(item);
        var listed = ClassStatPlugs.Contains(hash)
            ? stats.Where(stat => ClassOfStat.ContainsKey(stat.Stat)).Select(stat => $"{{ stat: {stat.Stat}, value: {stat.Value}, class: {ClassOfStat[stat.Stat]} }}")
            : stats.Select(stat => $"{{ stat: {stat.Stat}, value: {stat.Value} }}");
        return $"  - {{ hash: {hash}, kind: {ToWrittenKind(hash, kind, stats)}, stats: [{string.Join(", ", listed)}] }}";
    }

    /// <summary>
    /// The excerpt's kind of a stat plug: a tuning plug is the empty socket's (no stats), Balanced Tuning or a tuning mod; a
    /// mod is a stat mod; a subclass plug is a fragment.
    /// </summary>
    public static string ToWrittenKind(long hash, string kind, ImmutableArray<(string Stat, string Value)> stats) =>
        kind switch
        {
            "tuning" when stats.IsEmpty => "tuningSocket",
            "tuning" when hash == BalancedTuning => "balancedTuning",
            "mod" => "statMod",
            "subclass" => "fragment",
            _ => kind,
        };
}
