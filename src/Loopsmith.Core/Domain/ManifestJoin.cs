using System.Collections.Immutable;
using Dunet;

namespace Loopsmith.Core.Domain;

/// <summary>A component of Bungie's manifest the join reads (its file is <c>Destiny{Table}Definition-…json</c>).</summary>
public enum ManifestTable { InventoryItem, InventoryBucket, SocketType, SocketCategory, PlugSet, SandboxPerk, EquipableItemSet }

/// <summary>An entry of a manifest component: the table and its hash.</summary>
public sealed record ManifestEntryKey(ManifestTable Table, uint Hash);

/// <summary>
/// What the join asks Bungie's manifest for next: entries by hash, whole (small) tables, and the plug items of plug
/// categories (an exotic's catalyst socket names its category, not its catalyst).
/// </summary>
public sealed record ManifestQuery(
    ImmutableArray<ManifestEntryKey> Entries,
    ImmutableArray<ManifestTable> WholeTables,
    ImmutableArray<string> PlugCategories);

/// <summary>The manifest's answer: each entry's raw JSON (what it has of what was asked), and each plug category's plug items.</summary>
public sealed record ManifestAnswer(
    ImmutableDictionary<ManifestEntryKey, string> Entries,
    ImmutableDictionary<string, ImmutableArray<uint>> Plugs);

/// <summary>
/// A join under way: the hashes it describes, what it has read, and what it has asked (asked and not answered: not in
/// the manifest, so never asked again).
/// </summary>
public sealed record ManifestJoinProgress(
    ImmutableArray<ItemHash> Wanted,
    ManifestAnswer Read,
    ImmutableHashSet<ManifestEntryKey> AskedEntries,
    ImmutableHashSet<ManifestTable> AskedTables,
    ImmutableHashSet<string> AskedPlugs);

/// <summary>A join's next step: ask the manifest for more, or done — the excerpt of everything the wanted hashes need.</summary>
[Union]
public partial record ManifestJoinStep
{
    partial record Ask(ManifestJoinProgress Progress, ManifestQuery Query);
    partial record Done(ManifestExcerpt Excerpt);
}
