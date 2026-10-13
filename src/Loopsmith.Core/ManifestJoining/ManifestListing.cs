using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>
/// What the host reads of a component for the listing: the entries that hold <c>"<see cref="Key"/>":value</c> with one
/// of the values <see cref="Paths"/> names (as Bungie's JSON writes them; none: nothing to read), each cut down to that
/// value's paths (paths of property names, <c>*</c> for each element of an array; a path an entry lacks is left out),
/// grouped by the value: <c>{ "value": { "hash": { …the entry's parts… } } }</c>.
/// </summary>
public sealed record ManifestPartsQuery(string Component, string Key, ImmutableDictionary<string, ImmutableArray<ImmutableArray<string>>> Paths);

/// <summary>What the listing asks the host for next: entries by hash (an empty list: a whole small component), and items cut down to their parts.</summary>
public sealed record ListingQuery(ImmutableDictionary<string, ImmutableArray<ItemHash>> Entries, ManifestPartsQuery Parts);

/// <summary>
/// The listing's small components, read: each weapon and armor bucket's slot (by the bucket's hash), the armor sets, the
/// socket category of a weapon's intrinsic frame, and the breaker types by name ("shield piercing").
/// </summary>
public sealed record ListingTables(
    ImmutableDictionary<ItemHash, WeaponSlot> WeaponBuckets,
    ImmutableDictionary<ItemHash, ArmorSlot> ArmorBuckets,
    ImmutableDictionary<ItemHash, ArmorSet> Sets,
    Optional<ItemHash> IntrinsicCategory,
    ImmutableDictionary<string, ItemHash> Breakers);

/// <summary>An item the listing keeps, and the plugs its intrinsic sockets start with (a weapon's frame is among them).</summary>
public sealed record ListedEntry(ManifestItem Item, ImmutableArray<ItemHash> Intrinsics);

/// <summary>Where a listing is: what it has read so far, and so what it waits for.</summary>
[Union]
public partial record ListingProgress
{
    partial record AwaitingTables();
    partial record AwaitingItems(ListingTables Tables);
    partial record AwaitingFrames(ListingTables Tables, ImmutableArray<ListedEntry> Items);
    partial record AwaitingFramePerks(ListingTables Tables, ImmutableArray<ListedEntry> Items, ImmutableDictionary<ItemHash, JsonElement> Frames);
}

/// <summary>A listing's next step: ask the host for more, or done — every weapon and armor piece a build can wear.</summary>
[Union]
public partial record ListingStep
{
    partial record Ask(ListingProgress Progress, ListingQuery Query);
    partial record Done(ItemListing Listing);
}

/// <summary>
/// Pure: every weapon and armor piece a build can wear, from Bungie's manifest (<see cref="ItemListing"/>, the pickers'
/// "Every item"), in four rounds the host answers: the small components whole (the buckets, the armor sets, the socket
/// categories, the breaker types); the items of the weapon and armor buckets, each cut down to the fields its square and
/// the build check need and the plugs its sockets start with; the weapons' intrinsic frames; and the frames' perks. A
/// weapon's breaker is its own, else its frame's (a frame's perk named with the breaker, "[Shield-Piercing] Barrier"),
/// as the join reads it. An item is listed when it is equippable, named, legendary or exotic, and a weapon in a weapon
/// bucket or armor in an armor bucket; every version of it is (each reissue is an item of its own, with its season's
/// watermark and, for armor, its set).
/// </summary>
public static class ManifestListing
{
    private const string BucketKey = "bucketTypeHash";
    private const string IntrinsicCategoryName = "INTRINSIC TRAITS";
    private const int ArmorItemType = 2;     // the manifest's itemType for armor…
    private const int WeaponItemType = 3;    // …and for weapons

    private static readonly ImmutableArray<ManifestTable> SmallTables =
        [ManifestTable.InventoryBucket, ManifestTable.EquipableItemSet, ManifestTable.SocketCategory, ManifestTable.BreakerType];

    /// <summary>The components the listing reads.</summary>
    public static readonly ImmutableArray<string> Components =
        [.. new[] { ManifestTable.InventoryItem, ManifestTable.SandboxPerk }.Concat(SmallTables).Select(ManifestTransport.ToComponentName)];

    /// <summary>What an item's square and the build check need of it.</summary>
    private static readonly ImmutableArray<ImmutableArray<string>> ItemPaths =
    [
        ["displayProperties", "name"],
        ["displayProperties", "icon"],
        ["iconWatermark"],
        ["itemTypeDisplayName"],
        ["itemType"],
        ["equippable"],
        ["classType"],
        ["defaultDamageType"],
        ["breakerTypeHash"],
        ["inventory", "tierType"],
        ["inventory", BucketKey],
        ["equippingBlock", "equipableItemSetHash"],
    ];

    /// <summary>…and, for a weapon, the plugs its sockets start with and which sockets are its intrinsic ones (its frame).</summary>
    private static readonly ImmutableArray<ImmutableArray<string>> WeaponPaths =
    [
        .. ItemPaths,
        ["sockets", "socketEntries", "*", "singleInitialItemHash"],
        ["sockets", "socketCategories", "*", "socketCategoryHash"],
        ["sockets", "socketCategories", "*", "socketIndexes"],
    ];

    private static readonly ManifestPartsQuery NoParts =
        new(ManifestTransport.ToComponentName(ManifestTable.InventoryItem), BucketKey, ImmutableDictionary<string, ImmutableArray<ImmutableArray<string>>>.Empty);

    /// <summary>A listing's first question: the small components, whole.</summary>
    public static ListingStep StartListing() =>
        new ListingStep.Ask(
            new ListingProgress.AwaitingTables(),
            new ListingQuery(ManifestTransport.ToEntryRequest(new ManifestQuery([], SmallTables, [])), NoParts));

    /// <summary>The listing with the host's answers to its last question (its entries, its parts): its next question, the listing, or why an answer doesn't read.</summary>
    public static Result<ListingStep, string> ContinueListing(ListingStep.Ask asked, string entriesJson, string partsJson) =>
        ManifestTransport.ReadEntryAnswer(entriesJson).Bind(entries => asked.Progress.Match(
            _ => ReadTables(entries).Map(AskItems),
            awaiting => ParseDocument(partsJson)
                .MapError(problem => $"its items don't read: {problem}")
                .Map(parts => AskFrames(awaiting.Tables, ReadItems(awaiting.Tables, parts))),
            awaiting => new Result<ListingStep, string>.Ok(AskFramePerks(awaiting.Tables, awaiting.Items, ReadFrames(entries))),
            awaiting => new Result<ListingStep, string>.Ok(new ListingStep.Done(BuildListing(awaiting.Tables, awaiting.Items, awaiting.Frames, entries)))));

    /// <summary>The listing a finished step built; why there is none when reading the manifest failed or didn't finish.</summary>
    public static Result<ItemListing, string> ToItemListing(Result<ListingStep, string> step) =>
        step.Bind(reached => reached.Match<Result<ItemListing, string>>(
            _ => new Result<ItemListing, string>.Error("its reading didn't finish"),
            done => new Result<ItemListing, string>.Ok(done.Listing)));

    // ── the rounds ──────────────────────────────────────────────────────────────────

    private static Result<ListingTables, string> ReadTables(ImmutableDictionary<ManifestEntryKey, JsonElement> entries)
    {
        var buckets = ListTable(entries, ManifestTable.InventoryBucket)
            .Select(entry => (entry.Hash, Name: entry.Entry.ReadString("displayProperties", "name").UnwrapOr("")))
            .ToImmutableArray();
        var weapons = buckets.Where(bucket => ManifestJoin.WeaponBuckets.ContainsKey(bucket.Name)).ToImmutableDictionary(bucket => bucket.Hash, bucket => ManifestJoin.WeaponBuckets[bucket.Name]);
        var armor = buckets.Where(bucket => ManifestJoin.ArmorBuckets.ContainsKey(bucket.Name)).ToImmutableDictionary(bucket => bucket.Hash, bucket => ManifestJoin.ArmorBuckets[bucket.Name]);
        var sets = ListTable(entries, ManifestTable.EquipableItemSet).ToImmutableDictionary(entry => entry.Hash, entry => ManifestJoin.ToArmorSet(entry.Hash, entry.Entry));
        var intrinsic = ListTable(entries, ManifestTable.SocketCategory)
            .Where(entry => entry.Entry.ReadString("displayProperties", "name") == Optional.Some(IntrinsicCategoryName))
            .Select(entry => Optional.Some(entry.Hash))
            .FindFirstSome();
        var breakers = ListTable(entries, ManifestTable.BreakerType)
            .SelectMany(entry => ToSequence(entry.Entry.ReadText("displayProperties", "name").Map(name => (Name: ManifestJoin.ToBreakerName(name), entry.Hash))))
            .DistinctBy(breaker => breaker.Name)
            .ToImmutableDictionary(breaker => breaker.Name, breaker => breaker.Hash);
        return weapons.IsEmpty || armor.IsEmpty
            ? new Result<ListingTables, string>.Error("its buckets name no weapon or armor slot")
            : new Result<ListingTables, string>.Ok(new ListingTables(weapons, armor, sets, intrinsic, breakers));
    }

    /// <summary>The items of the weapon and armor buckets, each cut down to its parts.</summary>
    private static ListingStep AskItems(ListingTables tables) =>
        new ListingStep.Ask(
            new ListingProgress.AwaitingItems(tables),
            new ListingQuery(
                ImmutableDictionary<string, ImmutableArray<ItemHash>>.Empty,
                NoParts with
                {
                    Paths = tables.WeaponBuckets.Keys.Select(hash => (Hash: hash, Paths: WeaponPaths))
                        .Concat(tables.ArmorBuckets.Keys.Select(hash => (Hash: hash, Paths: ItemPaths)))
                        .ToImmutableDictionary(bucket => bucket.Hash.Value.ToString(CultureInfo.InvariantCulture), bucket => bucket.Paths),
                }));

    /// <summary>The weapons' intrinsic plugs (their frames, an exotic's own perk), unless no weapon has one.</summary>
    private static ListingStep AskFrames(ListingTables tables, ImmutableArray<ListedEntry> items)
    {
        var frames = items.SelectMany(entry => entry.Intrinsics).Distinct().Order().ToImmutableArray();
        return frames.IsEmpty
            ? new ListingStep.Done(BuildListing(tables, items, ImmutableDictionary<ItemHash, JsonElement>.Empty, ImmutableDictionary<ManifestEntryKey, JsonElement>.Empty))
            : new ListingStep.Ask(
                new ListingProgress.AwaitingFrames(tables, items),
                new ListingQuery(ManifestTransport.ToEntryRequest(new ManifestQuery([.. frames.Select(hash => new ManifestEntryKey(ManifestTable.InventoryItem, hash))], [], [])), NoParts));
    }

    /// <summary>The perks of the frames read (a frame's breaker is in one's name).</summary>
    private static ListingStep AskFramePerks(ListingTables tables, ImmutableArray<ListedEntry> items, ImmutableDictionary<ItemHash, JsonElement> frames)
    {
        var perks = frames.Values.SelectMany(ListPerks).Distinct().Order().ToImmutableArray();
        return perks.IsEmpty
            ? new ListingStep.Done(BuildListing(tables, items, frames, ImmutableDictionary<ManifestEntryKey, JsonElement>.Empty))
            : new ListingStep.Ask(
                new ListingProgress.AwaitingFramePerks(tables, items, frames),
                new ListingQuery(ManifestTransport.ToEntryRequest(new ManifestQuery([.. perks.Select(hash => new ManifestEntryKey(ManifestTable.SandboxPerk, hash))], [], [])), NoParts));
    }

    /// <summary>The listing: the items by name (a name's versions side by side, by hash), each weapon with its intrinsic and breaker; and the sets.</summary>
    private static ItemListing BuildListing(
        ListingTables tables, ImmutableArray<ListedEntry> items, ImmutableDictionary<ItemHash, JsonElement> frames, ImmutableDictionary<ManifestEntryKey, JsonElement> perks) =>
        new(
            [
                .. items
                    .Select(entry => entry.Item.Kind is ManifestKind.Weapon weapon
                        ? entry.Item with { Kind = ToListedWeapon(tables, weapon, FindFrame(entry.Intrinsics, frames), frames, perks) }
                        : entry.Item)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Hash.Value),
            ],
            tables.Sets);

    // ── reading the answers ─────────────────────────────────────────────────────────

    /// <summary>The items a build can wear in the answer's parts (each once), with their intrinsic plugs.</summary>
    private static ImmutableArray<ListedEntry> ReadItems(ListingTables tables, JsonElement parts) =>
    [
        .. parts.EnumerateObject()
            .Where(group => group.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(group => group.Value.EnumerateObject())
            .SelectMany(entry => ToSequence(ReadHashName(entry.Name).Bind(hash => ToListedEntry(tables, hash, entry.Value))))
            .DistinctBy(entry => entry.Item.Hash),
    ];

    /// <summary>An item a build can wear, from its parts; none for anything else (a shader, a blue, an ornament's dummy).</summary>
    private static Optional<ListedEntry> ToListedEntry(ListingTables tables, ItemHash hash, JsonElement parts)
    {
        var name = parts.ReadText("displayProperties", "name");
        var tier = parts.ReadInteger("inventory", "tierType").Bind(found => ManifestJoin.Tiers.TryGetValue(found, out var known) ? Optional.Some(known) : Optional.None<ItemTier>());
        var wearable = parts.ReadFlag("equippable", false) && name.IsSome() && (tier == Optional.Some(ItemTier.Legendary) || tier == Optional.Some(ItemTier.Exotic));
        var kind = wearable ? ToListedKind(tables, parts) : Optional.None<ManifestKind>();
        return kind.Map(found => new ListedEntry(
            new ManifestItem(
                hash,
                name.UnwrapOr(""),
                found,
                parts.ReadText("itemTypeDisplayName"),
                tier,
                Optional.None<string>(),
                parts.ReadString("displayProperties", "icon").Bind(ToPresent),
                parts.ReadString("iconWatermark").Bind(ToPresent)),
            found is ManifestKind.Weapon ? ListIntrinsicPlugs(tables, parts) : []));
    }

    /// <summary>A weapon in a weapon bucket (its slot, damage type and own breaker), armor in an armor bucket (its slot, set and class); none for anything else.</summary>
    private static Optional<ManifestKind> ToListedKind(ListingTables tables, JsonElement parts)
    {
        var bucket = parts.ReadHash("inventory", BucketKey);
        var itemType = parts.ReadInteger("itemType");
        var weaponSlot = bucket.Bind(found => tables.WeaponBuckets.TryGetValue(found, out var slot) ? Optional.Some(slot) : Optional.None<WeaponSlot>());
        var armorSlot = bucket.Bind(found => tables.ArmorBuckets.TryGetValue(found, out var slot) ? Optional.Some(slot) : Optional.None<ArmorSlot>());
        return itemType == Optional.Some(WeaponItemType)
            ? weaponSlot.Map(slot => (ManifestKind)new ManifestKind.Weapon(
                slot,
                parts.ReadInteger("defaultDamageType").Bind(type => ManifestJoin.DamageTypes.TryGetValue(type, out var found) ? Optional.Some(found) : Optional.None<DamageType>()),
                [],
                Optional.None<ItemHash>(),
                Optional.None<ItemHash>(),
                parts.ReadHash("breakerTypeHash")))
            : itemType == Optional.Some(ArmorItemType)
                ? armorSlot.Map(slot => (ManifestKind)new ManifestKind.Armor(
                    slot,
                    Optional.None<ItemHash>(),
                    parts.ReadHash("equippingBlock", "equipableItemSetHash"),
                    parts.ReadInteger("classType").Bind(ManifestJoin.ToGuardianClass)))
                : Optional.None<ManifestKind>();
    }

    /// <summary>The plugs a weapon's intrinsic sockets start with (its frame, an exotic's own perk).</summary>
    private static ImmutableArray<ItemHash> ListIntrinsicPlugs(ListingTables tables, JsonElement parts)
    {
        var entries = parts.ReadArray("sockets", "socketEntries");
        return tables.IntrinsicCategory.Match(
            category => [.. parts.ReadArray("sockets", "socketCategories")
                .Where(group => group.ReadHash("socketCategoryHash") == Optional.Some(category.Value))
                .SelectMany(group => group.ReadArray("socketIndexes"))
                .SelectMany(index => index.TryGetInt32(out var at) && at >= 0 && at < entries.Length ? ToSequence(entries[at].ReadHash("singleInitialItemHash")) : [])],
            _ => ImmutableArray<ItemHash>.Empty);
    }

    /// <summary>The frames read: the intrinsic plugs that are named intrinsics (as the join takes a weapon's intrinsic).</summary>
    private static ImmutableDictionary<ItemHash, JsonElement> ReadFrames(ImmutableDictionary<ManifestEntryKey, JsonElement> entries) =>
        ListTable(entries, ManifestTable.InventoryItem)
            .Where(entry => entry.Entry.ReadString("plug", "plugCategoryIdentifier") == Optional.Some("intrinsics") && entry.Entry.ReadText("displayProperties", "name").IsSome())
            .ToImmutableDictionary(entry => entry.Hash, entry => entry.Entry);

    private static IEnumerable<ItemHash> ListPerks(JsonElement frame) =>
        frame.ReadArray("perks").SelectMany(perk => ToSequence(perk.ReadHash("perkHash")));

    /// <summary>A weapon of the listing with its intrinsic (its first named intrinsic plug: a legendary's frame, an exotic's own perk) and, when it has no breaker of its own, its frame's.</summary>
    private static ManifestKind.Weapon ToListedWeapon(
        ListingTables tables, ManifestKind.Weapon weapon, Optional<ItemHash> frame, ImmutableDictionary<ItemHash, JsonElement> frames, ImmutableDictionary<ManifestEntryKey, JsonElement> perks) =>
        weapon with
        {
            Intrinsic = frame,
            Breaker = weapon.Breaker.IsSome() ? weapon.Breaker : frame.Bind(found => FindFrameBreaker(tables, found, frames, perks)),
        };

    /// <summary>A weapon's first intrinsic plug that is a named intrinsic (as the join takes a weapon's intrinsic).</summary>
    private static Optional<ItemHash> FindFrame(ImmutableArray<ItemHash> intrinsics, ImmutableDictionary<ItemHash, JsonElement> frames) =>
        intrinsics.Where(frames.ContainsKey).Select(Optional.Some).FindFirstSome();

    /// <summary>A frame's breaker: the breaker its perks name ("[Shield-Piercing] Barrier"); none when they name none.</summary>
    private static Optional<ItemHash> FindFrameBreaker(
        ListingTables tables, ItemHash frame, ImmutableDictionary<ItemHash, JsonElement> frames, ImmutableDictionary<ManifestEntryKey, JsonElement> perks) =>
        ListPerks(frames[frame])
            .SelectMany(perk => perks.TryGetValue(new ManifestEntryKey(ManifestTable.SandboxPerk, perk), out var found) ? ToSequence(found.ReadString("displayProperties", "name")) : [])
            .SelectMany(ManifestJoin.ListBracketedNames)
            .SelectMany(name => tables.Breakers.TryGetValue(name, out var breaker) ? new[] { breaker } : [])
            .Select(Optional.Some)
            .FindFirstSome();

    private static IEnumerable<(ItemHash Hash, JsonElement Entry)> ListTable(ImmutableDictionary<ManifestEntryKey, JsonElement> entries, ManifestTable table) =>
        entries.Where(entry => entry.Key.Table == table).OrderBy(entry => entry.Key.Hash.Value).Select(entry => (entry.Key.Hash, entry.Value));

    private static Optional<ItemHash> ReadHashName(string name) =>
        uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var hash) ? ToItemHash(hash) : Optional.None<ItemHash>();

    private static Optional<string> ToPresent(string text) =>
        text.Length > 0 ? Optional.Some(text) : Optional.None<string>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}
