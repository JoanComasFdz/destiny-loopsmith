using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// Pure: every legendary and exotic weapon and armor piece of the game, from Bungie's full manifest (what the pickers list
/// without a Bungie login). An item is read the way the join reads it, but there are thousands, so the listing asks for
/// little at a time: the small tables; the item entries of each weapon and armor slot's bucket; those entries' summaries
/// (<see cref="SummaryPaths"/>: enough to tell which are listed, and which version of each name is the newest); the listed
/// ones' outlines (<see cref="OutlinePaths"/>: what a picker shows); and the weapons' intrinsic perks with their sandbox
/// perks (a legendary's frame and the champion it breaks). No description, trait columns, catalyst, armor intrinsic or set
/// perks: the join reads those for the item picked. A dummy, a redacted or unequippable item, one below legendary and a weapon without a damage type
/// aren't listed. Each name is listed once per slot (and, for armor, per class): its newest version, the manifest's entry
/// of that name with the highest <c>index</c> (Fatebringer's reissue, Shinobu's Vow's Armor 3.0 copy).
/// </summary>
public static partial class ManifestJoin
{
    private const string BucketField = "\"bucketTypeHash\":";
    private const int ArmorItemType = 2;
    private const int WeaponItemType = 3;
    private const int LegendaryTierType = 5;
    private const int ExoticTierType = 6;
    private const int AnyClass = 3;

    /// <summary>The fields of a slot's item entry the listing reads first: whether it is listed, and which version of its name it is.</summary>
    public static readonly ImmutableArray<string> SummaryPaths =
    [
        "displayProperties.name", "itemType", "classType", "defaultDamageType", "index", "equippable", "redacted",
        "inventory.bucketTypeHash", "inventory.tierType",
    ];

    /// <summary>The fields of a listed item's entry: its summary and what a picker shows (a weapon's intrinsic socket among its sockets).</summary>
    public static readonly ImmutableArray<string> OutlinePaths =
    [
        .. SummaryPaths, "displayProperties.icon", "iconWatermark", "itemTypeDisplayName", "breakerTypeHash",
        "equippingBlock.equipableItemSetHash", "sockets.socketCategories", "sockets.socketEntries.*.singleInitialItemHash",
    ];

    private static readonly ManifestListProgress NoListing =
        new(NoAnswer, ImmutableDictionary<string, ImmutableArray<ItemHash>>.Empty, Optional.None<ImmutableArray<ItemHash>>(), [], [], [], [], []);

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

    /// <summary>
    /// One round: the small tables; once the buckets are read, the slots' needles; the summaries of what they found; once
    /// those are read, the items listed (kept from then on) and their outlines; once those are read, the listed weapons'
    /// intrinsic plugs and their frames' sandbox perks. When nothing is left to ask: the game's weapons and armor.
    /// </summary>
    private static ManifestListStep TakeListStep(ManifestListProgress progress)
    {
        var reading = ToReading(progress.Read);
        var needles = ListSlotNeedles(reading);
        var found = needles.SelectMany(needle => progress.Found.TryGetValue(needle, out var hashes) ? hashes : []).Distinct().ToImmutableArray();
        var listed = progress.Listed.Match(kept => kept.Value, _ => progress.AskedSummaries.IsEmpty ? [] : ListNewestItems(reading, found));
        var outlined = listed.Where(progress.AskedOutlines.Contains).SelectMany(hash => ToSequence(FindEntry(reading, ManifestTable.InventoryItem, hash))).ToImmutableArray();
        var intrinsics = outlined
            .Where(item => ReadBucketName(reading, item).Match(bucket => WeaponBuckets.ContainsKey(bucket.Value), _ => false))
            .SelectMany(weapon => ListSocketPlugs(reading, weapon, WeaponIntrinsicCategoryName))
            .Distinct()
            .ToImmutableArray();
        var plugs = Combine(intrinsics
            .Where(plug => IsNamedIntrinsic(reading, plug))
            .Select(frame => FindEntry(reading, ManifestTable.InventoryItem, frame).Match(entry => ListPerkNeeds(reading, entry.Value), _ => Reach.None))
            .Prepend(ToEntryReach(ManifestTable.InventoryItem, intrinsics)));
        var tables = SmallTables.Where(table => !progress.AskedTables.Contains(table)).ToImmutableArray();
        var newNeedles = needles.Where(needle => !progress.AskedNeedles.Contains(needle)).ToImmutableArray();
        var summaries = found.Where(hash => !progress.AskedSummaries.Contains(hash)).ToImmutableArray();
        var outlines = listed.Where(hash => !progress.AskedOutlines.Contains(hash)).ToImmutableArray();
        var entries = plugs.Entries.Distinct().Where(key => !progress.AskedEntries.Contains(key)).ToImmutableArray();
        var asking = summaries.IsEmpty ? outlines : summaries;
        var kept = progress with { Listed = progress.AskedSummaries.IsEmpty ? progress.Listed : Optional.Some(listed) };
        return tables.IsEmpty && newNeedles.IsEmpty && asking.IsEmpty && entries.IsEmpty
            ? new ManifestListStep.Done(BuildGameItems(reading, listed))
            : new ManifestListStep.Ask(
                kept with
                {
                    AskedTables = progress.AskedTables.Union(tables),
                    AskedNeedles = progress.AskedNeedles.Union(newNeedles),
                    AskedSummaries = progress.AskedSummaries.Union(summaries),
                    AskedOutlines = summaries.IsEmpty ? progress.AskedOutlines.Union(outlines) : progress.AskedOutlines,
                    AskedEntries = progress.AskedEntries.Union(entries),
                },
                new ManifestListQuery(
                    [.. tables.OrderBy(table => table)],
                    [.. newNeedles.Order(StringComparer.Ordinal)],
                    [.. asking.OrderBy(hash => hash.Value)],
                    summaries.IsEmpty ? OutlinePaths : SummaryPaths,
                    [.. entries.OrderBy(key => key.Table).ThenBy(key => key.Hash.Value)]));
    }

    /// <summary>The needles of the weapon and armor slots' buckets, by their names in the bucket table.</summary>
    private static ImmutableArray<string> ListSlotNeedles(Reading reading) =>
    [
        .. reading.BucketNames
            .Where(bucket => WeaponBuckets.ContainsKey(bucket.Value) || ArmorBuckets.ContainsKey(bucket.Value))
            .Select(bucket => ToBucketNeedle(bucket.Key))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>A summarised entry that is listed: what makes its versions one item, and which version it is (its <c>index</c>).</summary>
    private sealed record Version(ItemHash Hash, string Item, int Index);

    /// <summary>
    /// Of the summarised entries, those listed (a legendary or exotic weapon with a damage type, or armor piece, one can
    /// equip, named and not redacted), each name once per slot (and class): its newest version, the highest <c>index</c>.
    /// </summary>
    private static ImmutableArray<ItemHash> ListNewestItems(Reading reading, IEnumerable<ItemHash> summarised) =>
    [
        .. summarised
            .SelectMany(hash => ToSequence(FindEntry(reading, ManifestTable.InventoryItem, hash).Bind(entry => ToVersion(reading, hash, entry))))
            .GroupBy(version => version.Item, StringComparer.Ordinal)
            .Select(versions => versions.OrderByDescending(version => version.Index).ThenByDescending(version => version.Hash.Value).First().Hash)
            .OrderBy(hash => hash.Value),
    ];

    /// <summary>
    /// A summarised entry as a version of an item, when it is listed: a weapon is one by its slot (bucket) and name, an
    /// armor piece by its slot, class and name. Each field is read once (thousands are).
    /// </summary>
    private static Optional<Version> ToVersion(Reading reading, ItemHash hash, JsonElement item)
    {
        var bucket = ReadBucketName(reading, item).UnwrapOr("");
        var weapon = WeaponBuckets.ContainsKey(bucket);
        var armor = ArmorBuckets.ContainsKey(bucket);
        var itemType = item.ReadInteger("itemType");
        var tier = item.ReadInteger("inventory", "tierType");
        var name = item.ReadString("displayProperties", "name").UnwrapOr("").Trim();
        var listed = ((weapon && itemType == Optional.Some(WeaponItemType) && item.ReadInteger("defaultDamageType").Match(type => DamageTypes.ContainsKey(type.Value), _ => false))
                || (armor && itemType == Optional.Some(ArmorItemType)))
            && (tier == Optional.Some(LegendaryTierType) || tier == Optional.Some(ExoticTierType))
            && item.ReadFlag("equippable", false)
            && !item.ReadFlag("redacted", false)
            && name.Length > 0;
        var key = armor
            ? $"armor|{bucket}|{item.ReadInteger("classType").UnwrapOr(AnyClass).ToString(CultureInfo.InvariantCulture)}|{name}"
            : $"weapon|{bucket}|{name}";
        return listed ? Optional.Some(new Version(hash, key, item.ReadInteger("index").UnwrapOr(0))) : Optional.None<Version>();
    }

    /// <summary>The listed items by name, and the weapons' intrinsic perks (each frame's breaker found once, not per weapon).</summary>
    private static GameItems BuildGameItems(Reading reading, ImmutableArray<ItemHash> listed)
    {
        var entries = listed.SelectMany(hash => ToSequence(FindEntry(reading, ManifestTable.InventoryItem, hash).Map(entry => new ReadItem(hash, entry)))).ToImmutableArray();
        var frames = entries
            .Where(item => ReadBucketName(reading, item.Entry).Match(bucket => WeaponBuckets.ContainsKey(bucket.Value), _ => false))
            .SelectMany(weapon => ToSequence(FindIntrinsic(reading, weapon.Entry, WeaponIntrinsicCategoryName)))
            .Distinct()
            .ToImmutableDictionary(frame => frame, frame => FindBreakerOfFrame(reading, frame));
        var items = entries
            .SelectMany(item => ToSequence(ToListedItem(reading, item, frames)))
            .Where(item => item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some } or ManifestKind.Armor)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Hash.Value)
            .ToImmutableArray();
        var weapons = items.Where(item => item.Kind is ManifestKind.Weapon).ToImmutableArray();
        var intrinsics = weapons
            .SelectMany(item => item.Kind is ManifestKind.Weapon weapon ? ToSequence(weapon.Intrinsic) : [])
            .Distinct()
            .SelectMany(hash => ToSequence(ToManifestItem(reading, hash, false)))
            .ToImmutableDictionary(item => item.Hash);
        return new GameItems(weapons, [.. items.Where(item => item.Kind is ManifestKind.Armor)], intrinsics);
    }

    /// <summary>
    /// A listed item from its outline, as the join would read it but for what a picker doesn't show: no description, a
    /// weapon's trait columns or catalyst, or armor's intrinsic perk (the join reads them for the item picked).
    /// </summary>
    private static Optional<ManifestItem> ToListedItem(Reading reading, ReadItem item, ImmutableDictionary<ItemHash, Optional<ItemHash>> frameBreakers) =>
        ToListedKind(reading, item.Entry, frameBreakers).Map(kind => ToItem(item.Hash, item.Entry, kind, Optional.None<string>()));

    /// <summary>A listed weapon's kind (its breaker its own, else its frame's) or armor's; none for anything else.</summary>
    private static Optional<ManifestKind> ToListedKind(Reading reading, JsonElement item, ImmutableDictionary<ItemHash, Optional<ItemHash>> frameBreakers)
    {
        var bucket = ReadBucketName(reading, item).UnwrapOr("");
        return WeaponBuckets.TryGetValue(bucket, out var weaponSlot)
            ? Optional.Some<ManifestKind>(ToListedWeapon(reading, item, weaponSlot, frameBreakers))
            : ArmorBuckets.TryGetValue(bucket, out var armorSlot)
                ? Optional.Some<ManifestKind>(new ManifestKind.Armor(armorSlot, Optional.None<ItemHash>(), item.ReadHash("equippingBlock", "equipableItemSetHash"), item.ReadInteger("classType").Bind(ToGuardianClass)))
                : Optional.None<ManifestKind>();
    }

    private static ManifestKind.Weapon ToListedWeapon(Reading reading, JsonElement item, WeaponSlot slot, ImmutableDictionary<ItemHash, Optional<ItemHash>> frameBreakers)
    {
        var intrinsic = FindIntrinsic(reading, item, WeaponIntrinsicCategoryName);
        var frameBreaker = intrinsic.Bind(frame => frameBreakers.TryGetValue(frame, out var breaker) ? breaker : Optional.None<ItemHash>());
        return new ManifestKind.Weapon(
            slot,
            item.ReadInteger("defaultDamageType").Bind(type => DamageTypes.TryGetValue(type, out var found) ? Optional.Some(found) : Optional.None<DamageType>()),
            [],
            intrinsic,
            Optional.None<ItemHash>(),
            new[] { item.ReadHash("breakerTypeHash"), frameBreaker }.FindFirstSome());
    }
}
