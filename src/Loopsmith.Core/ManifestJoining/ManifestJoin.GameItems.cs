using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: every legendary and exotic weapon and armor piece of the game, from Bungie's full manifest (what the pickers list
/// without a Bungie login). An item is read the way the join reads it, but thousands at once, so the listing asks for
/// less: the small tables; the item entries of each weapon and armor slot's bucket; those entries cut down to what a
/// picker shows (<see cref="OutlinePaths"/>); and the weapons' intrinsic perks with their sandbox perks (a legendary's
/// frame and the champion it breaks). No trait columns, catalyst or set perks: the join reads those for the item picked.
/// A dummy, a redacted or unequippable item, one below legendary and a weapon without a damage type aren't listed. Each
/// name is listed once per slot (and, for armor, per class): its newest version, the manifest's entry of that name with
/// the highest <c>index</c> (Fatebringer's reissue, Shinobu's Vow's Armor 3.0 copy).
/// </summary>
public static partial class ManifestJoin
{
    private const string BucketField = "\"bucketTypeHash\":";
    private const int ArmorItemType = 2;
    private const int WeaponItemType = 3;
    private const int LegendaryTierType = 5;
    private const int ExoticTierType = 6;

    /// <summary>
    /// The fields of an item entry the listing reads (a path of property names; <c>*</c>: each element of an array, in
    /// place): what a picker shows and what tells a listed item apart.
    /// </summary>
    public static readonly ImmutableArray<string> OutlinePaths =
    [
        "displayProperties.name", "displayProperties.icon", "iconWatermark", "itemTypeDisplayName", "itemType", "classType",
        "defaultDamageType", "breakerTypeHash", "index", "equippable", "redacted", "inventory.bucketTypeHash", "inventory.tierType",
        "equippingBlock.equipableItemSetHash", "sockets.socketCategories", "sockets.socketEntries.*.singleInitialItemHash",
    ];

    private static readonly ManifestListProgress NoListing = new(NoAnswer, ImmutableDictionary<string, ImmutableArray<ItemHash>>.Empty, [], [], []);

    /// <summary>A listing of the game's weapons and armor: its first question to the manifest.</summary>
    public static ManifestListStep StartGameItemListing() =>
        TakeListStep(NoListing);

    /// <summary>The listing with the manifest's answer to its last question: its next question, or the game's weapons and armor.</summary>
    public static ManifestListStep ContinueGameItemListing(ManifestListProgress progress, ManifestListAnswer answer) =>
        TakeListStep(progress with
        {
            Read = new ManifestAnswer(progress.Read.Entries.SetItems(answer.Read.Entries), progress.Read.Plugs.SetItems(answer.Read.Plugs)),
            Found = progress.Found.SetItems(answer.Found),
        });

    /// <summary>The needle that finds a bucket's item entries: <c>"bucketTypeHash":1498876634</c> (compact, as Bungie writes it).</summary>
    public static string ToBucketNeedle(ItemHash bucket) =>
        BucketField + bucket.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a listing walk reaches: whole tables, needles, item entries to read cut down, and entries to read whole.</summary>
    private sealed record ListReach(
        ImmutableArray<ManifestTable> Tables,
        ImmutableArray<string> Needles,
        ImmutableArray<ItemHash> Outlines,
        ImmutableArray<ManifestEntryKey> Entries);

    private static ManifestListStep TakeListStep(ManifestListProgress progress)
    {
        var reading = ToReading(progress.Read);
        var reach = WalkGameItems(reading, progress.Found);
        var tables = reach.Tables.ToImmutableHashSet().Except(progress.AskedTables);
        var needles = reach.Needles.ToImmutableHashSet(StringComparer.Ordinal).Except(progress.AskedNeedles);
        var outlines = reach.Outlines.Select(hash => new ManifestEntryKey(ManifestTable.InventoryItem, hash)).ToImmutableHashSet().Except(progress.AskedEntries);
        var entries = reach.Entries.ToImmutableHashSet().Except(progress.AskedEntries).Except(outlines);
        return tables.IsEmpty && needles.IsEmpty && outlines.IsEmpty && entries.IsEmpty
            ? new ManifestListStep.Done(BuildGameItems(reading, progress.Found))
            : new ManifestListStep.Ask(
                progress with
                {
                    AskedTables = progress.AskedTables.Union(tables),
                    AskedNeedles = progress.AskedNeedles.Union(needles),
                    AskedEntries = progress.AskedEntries.Union(outlines).Union(entries),
                },
                new ManifestListQuery(
                    [.. tables.OrderBy(table => table)],
                    [.. needles.Order(StringComparer.Ordinal)],
                    [.. outlines.Select(key => key.Hash).OrderBy(hash => hash.Value)],
                    OutlinePaths,
                    [.. entries.OrderBy(key => key.Table).ThenBy(key => key.Hash.Value)]));
    }

    /// <summary>
    /// The small tables; once the buckets are read, the needle of each weapon and armor slot's bucket; the item entries
    /// they found; and, for each listed weapon, its intrinsic sockets' plugs and its frame's sandbox perks.
    /// </summary>
    private static ListReach WalkGameItems(Reading reading, ImmutableDictionary<string, ImmutableArray<ItemHash>> found)
    {
        var needles = ListSlotNeedles(reading);
        var outlines = needles.SelectMany(needle => found.TryGetValue(needle, out var hashes) ? hashes : []).Distinct().ToImmutableArray();
        var weapons = ListOutlinedItems(reading, outlines).Where(item => ClassifyItem(reading, item.Entry) == KindWord.Weapon).ToImmutableArray();
        var intrinsics = Combine(weapons.Select(weapon => WalkIntrinsicSockets(reading, weapon.Entry, WeaponIntrinsicCategoryName)));
        var framePerks = Combine(weapons.Select(weapon => WalkFramePerks(reading, weapon.Entry)));
        return new ListReach(SmallTables, needles, outlines, [.. intrinsics.Entries, .. framePerks.Entries]);
    }

    /// <summary>The needles of the weapon and armor slots' buckets, by their names in the bucket table.</summary>
    private static ImmutableArray<string> ListSlotNeedles(Reading reading) =>
    [
        .. reading.Entries
            .Where(entry => entry.Key.Table == ManifestTable.InventoryBucket)
            .Where(entry => entry.Value.ReadString("displayProperties", "name").Match(name => WeaponBuckets.ContainsKey(name.Value) || ArmorBuckets.ContainsKey(name.Value), _ => false))
            .Select(entry => ToBucketNeedle(entry.Key.Hash))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>The outlined entries that are listed: a legendary or exotic weapon or armor piece one can equip, named and not redacted.</summary>
    private static IEnumerable<ReadItem> ListOutlinedItems(Reading reading, IEnumerable<ItemHash> outlines) =>
        outlines
            .SelectMany(hash => ToSequence(FindEntry(reading, ManifestTable.InventoryItem, hash).Map(entry => new ReadItem(hash, entry))))
            .Where(item => IsListed(reading, item.Entry));

    private static bool IsListed(Reading reading, JsonElement item)
    {
        var itemType = item.ReadInteger("itemType");
        var tier = item.ReadInteger("inventory", "tierType");
        var kind = ClassifyItem(reading, item);
        return ((kind == KindWord.Weapon && itemType == Optional.Some(WeaponItemType)) || (kind == KindWord.Armor && itemType == Optional.Some(ArmorItemType)))
            && (tier == Optional.Some(LegendaryTierType) || tier == Optional.Some(ExoticTierType))
            && item.ReadFlag("equippable", false)
            && !item.ReadFlag("redacted", false)
            && item.ReadText("displayProperties", "name").IsSome();
    }

    /// <summary>The listed items, each name once per slot (and class) in its newest version, by name; and the weapons' intrinsic perks.</summary>
    private static GameItems BuildGameItems(Reading reading, ImmutableDictionary<string, ImmutableArray<ItemHash>> found)
    {
        var outlines = ListSlotNeedles(reading).SelectMany(needle => found.TryGetValue(needle, out var hashes) ? hashes : []).Distinct();
        var newest = ListOutlinedItems(reading, outlines)
            .SelectMany(item => ToSequence(ToManifestItem(reading, item.Hash, false).Map(listed => (Item: listed, Index: item.Entry.ReadInteger("index").UnwrapOr(0)))))
            .Where(listed => listed.Item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some } or ManifestKind.Armor)
            .GroupBy(listed => ToVersionKey(listed.Item), StringComparer.Ordinal)
            .Select(versions => versions.OrderByDescending(listed => listed.Index).ThenByDescending(listed => listed.Item.Hash.Value).First().Item)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Hash.Value)
            .ToImmutableArray();
        var weapons = newest.Where(item => item.Kind is ManifestKind.Weapon).ToImmutableArray();
        var intrinsics = weapons
            .SelectMany(item => item.Kind is ManifestKind.Weapon weapon ? ToSequence(weapon.Intrinsic) : [])
            .Distinct()
            .SelectMany(hash => ToSequence(ToManifestItem(reading, hash, false)))
            .ToImmutableDictionary(item => item.Hash);
        return new GameItems(weapons, [.. newest.Where(item => item.Kind is ManifestKind.Armor)], intrinsics);
    }

    /// <summary>What makes two entries versions of one item: a weapon's slot and name; an armor piece's slot, class and name.</summary>
    private static string ToVersionKey(ManifestItem item) =>
        item.Kind switch
        {
            ManifestKind.Weapon weapon => $"weapon|{weapon.Slot}|{item.Name}",
            ManifestKind.Armor armor => $"armor|{armor.Slot}|{armor.Class.Match(wornBy => wornBy.Value.ToString(), _ => "any")}|{item.Name}",
            _ => $"other|{item.Hash.Value}",
        };
}
