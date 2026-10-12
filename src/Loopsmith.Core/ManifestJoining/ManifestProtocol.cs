using System.Collections.Immutable;
using System.Text.Json;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.ManifestJoining;

// The join's protocol with the host that holds Bungie's manifest (ManifestJoin asks, the host answers).

/// <summary>A component of Bungie's manifest the join reads (its file is <c>Destiny{Table}Definition-…json</c>).</summary>
public enum ManifestTable { InventoryItem, InventoryBucket, SocketType, SocketCategory, PlugSet, SandboxPerk, EquipableItemSet, BreakerType }

/// <summary>An entry of a manifest component: the table and its hash.</summary>
public sealed record ManifestEntryKey(ManifestTable Table, ItemHash Hash);

/// <summary>
/// What the join asks Bungie's manifest for next: entries by hash, whole (small) tables, and the plug items of plug
/// categories (an exotic's catalyst socket names its category, not its catalyst).
/// </summary>
public sealed record ManifestQuery(
    ImmutableArray<ManifestEntryKey> Entries,
    ImmutableArray<ManifestTable> WholeTables,
    ImmutableArray<PlugCategoryId> PlugCategories);

/// <summary>
/// A search of a component's raw JSON: the entries that hold each needle (the plug items of a plug category are the
/// item entries that name it).
/// </summary>
public sealed record ManifestTextSearch(string Component, ImmutableArray<string> Needles);

/// <summary>
/// The manifest's answer: each entry (what it has of what was asked), parsed once where the answer is read, and each
/// plug category's plug items.
/// </summary>
public sealed record ManifestAnswer(
    ImmutableDictionary<ManifestEntryKey, JsonElement> Entries,
    ImmutableDictionary<PlugCategoryId, ImmutableArray<ItemHash>> Plugs);

/// <summary>
/// A join under way: the hashes it describes, what it has read, and what it has asked (asked and not answered: not in
/// the manifest, so never asked again).
/// </summary>
public sealed record ManifestJoinProgress(
    ImmutableArray<ItemHash> Wanted,
    ManifestAnswer Read,
    ImmutableHashSet<ManifestEntryKey> AskedEntries,
    ImmutableHashSet<ManifestTable> AskedTables,
    ImmutableHashSet<PlugCategoryId> AskedPlugs);

/// <summary>A join's next step: ask the manifest for more, or done — the excerpt of everything the wanted hashes need.</summary>
[Union]
public partial record ManifestJoinStep
{
    partial record Ask(ManifestJoinProgress Progress, ManifestQuery Query);
    partial record Done(ManifestExcerpt Excerpt);
}

/// <summary>
/// What the listing of the game's weapons and armor asks Bungie's manifest for next: whole (small) tables; the item
/// entries that hold each needle (a slot's bucket, <c>"bucketTypeHash":1498876634</c>); item entries cut down to
/// <see cref="OutlinePaths"/> (thousands of them: only the fields that round needs — a path of property names, <c>*</c>
/// for each element of an array); and entries read whole.
/// </summary>
public sealed record ManifestListQuery(
    ImmutableArray<ManifestTable> WholeTables,
    ImmutableArray<string> Needles,
    ImmutableArray<ItemHash> Outlines,
    ImmutableArray<string> OutlinePaths,
    ImmutableArray<ManifestEntryKey> Entries);

/// <summary>The manifest's answer to a listing's question: the entries (whole or cut down) and the item entries holding each needle.</summary>
public sealed record ManifestListAnswer(ManifestAnswer Read, ImmutableDictionary<string, ImmutableArray<ItemHash>> Found);

/// <summary>
/// A listing under way: what it has read, the entries each needle found, the items it lists (once their summaries are
/// read: each name's newest version), and what it has asked — summaries, outlines and whole entries apart (never asked again).
/// </summary>
public sealed record ManifestListProgress(
    ManifestAnswer Read,
    ImmutableDictionary<string, ImmutableArray<ItemHash>> Found,
    Optional<ImmutableArray<ItemHash>> Listed,
    ImmutableHashSet<ManifestTable> AskedTables,
    ImmutableHashSet<string> AskedNeedles,
    ImmutableHashSet<ItemHash> AskedSummaries,
    ImmutableHashSet<ItemHash> AskedOutlines,
    ImmutableHashSet<ManifestEntryKey> AskedEntries);

/// <summary>A listing's next step: ask the manifest for more, or done — the game's weapons and armor.</summary>
[Union]
public partial record ManifestListStep
{
    partial record Ask(ManifestListProgress Progress, ManifestListQuery Query);
    partial record Done(GameItems Items);
}
