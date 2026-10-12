using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>The listing's small components, read: each weapon and armor bucket's slot (by the bucket's hash), and the armor sets.</summary>
public sealed record ListingTables(
    ImmutableDictionary<ItemHash, WeaponSlot> WeaponBuckets,
    ImmutableDictionary<ItemHash, ArmorSlot> ArmorBuckets,
    ImmutableDictionary<ItemHash, ArmorSet> Sets);

/// <summary>
/// What the host reads of a component for the listing: the entries that hold <c>"<see cref="Key"/>":value</c> with one
/// of <see cref="Values"/> (as Bungie's JSON writes them), each cut down to <see cref="Paths"/> (paths of property names;
/// a path an entry lacks is left out), grouped by the value: <c>{ "value": { "hash": { …the entry's parts… } } }</c>.
/// </summary>
public sealed record ManifestPartsQuery(string Component, string Key, ImmutableArray<string> Values, ImmutableArray<ImmutableArray<string>> Paths);

/// <summary>
/// Pure: every weapon and armor piece a build can wear, from Bungie's manifest (<see cref="ItemListing"/>, the pickers'
/// "Every item"). It reads the item table's lite version (a third of the full table's size: the items without their
/// sockets, so without perks) with the buckets and the armor sets: the host loads the three components, reads the two
/// small ones whole (<see cref="ToTableRequest"/>), then the lite items in a weapon or armor bucket, each cut down to the
/// fields an item's square and the build check need (<see cref="ToPartsQuery"/>). An item is listed when it is
/// equippable, named, legendary or exotic, and a weapon in a weapon bucket or armor in an armor bucket; every version of
/// it is (each reissue is an item of its own, with its season's watermark and, for armor, its set).
/// </summary>
public static class ManifestListing
{
    /// <summary>The item table's lite version: each item without its sockets, stats and preview.</summary>
    public const string LiteItemComponent = "DestinyInventoryItemLiteDefinition";

    private const string BucketKey = "bucketTypeHash";
    private const int ArmorItemType = 2;     // the manifest's itemType for armor…
    private const int WeaponItemType = 3;    // …and for weapons

    /// <summary>The components the listing reads.</summary>
    public static readonly ImmutableArray<string> Components =
        [LiteItemComponent, ManifestTransport.ToComponentName(ManifestTable.InventoryBucket), ManifestTransport.ToComponentName(ManifestTable.EquipableItemSet)];

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
        ["inventory", "tierType"],
        ["inventory", BucketKey],
        ["equippingBlock", "equipableItemSetHash"],
    ];

    /// <summary>The two small components, read whole: the buckets (which are the weapon and armor slots) and the armor sets.</summary>
    public static ImmutableDictionary<string, ImmutableArray<ItemHash>> ToTableRequest() =>
        ManifestTransport.ToEntryRequest(new ManifestQuery([], [ManifestTable.InventoryBucket, ManifestTable.EquipableItemSet], []));

    /// <summary>The host's answer to <see cref="ToTableRequest"/>: the weapon and armor buckets and the sets; or why it doesn't read.</summary>
    public static Result<ListingTables, string> ReadTables(string entriesJson) =>
        ParseDocument(entriesJson)
            .MapError(problem => $"its buckets don't read: {problem}")
            .Bind(ToListingTables);

    private static Result<ListingTables, string> ToListingTables(JsonElement root)
    {
        var buckets = ListEntries(root, ManifestTransport.ToComponentName(ManifestTable.InventoryBucket))
            .Select(entry => (entry.Hash, Name: entry.Entry.ReadString("displayProperties", "name").UnwrapOr("")))
            .ToImmutableArray();
        var weapons = buckets.Where(bucket => ManifestJoin.WeaponBuckets.ContainsKey(bucket.Name)).ToImmutableDictionary(bucket => bucket.Hash, bucket => ManifestJoin.WeaponBuckets[bucket.Name]);
        var armor = buckets.Where(bucket => ManifestJoin.ArmorBuckets.ContainsKey(bucket.Name)).ToImmutableDictionary(bucket => bucket.Hash, bucket => ManifestJoin.ArmorBuckets[bucket.Name]);
        var sets = ListEntries(root, ManifestTransport.ToComponentName(ManifestTable.EquipableItemSet))
            .ToImmutableDictionary(entry => entry.Hash, entry => ManifestJoin.ToArmorSet(entry.Hash, entry.Entry));
        return weapons.IsEmpty || armor.IsEmpty
            ? new Result<ListingTables, string>.Error("its buckets name no weapon or armor slot")
            : new Result<ListingTables, string>.Ok(new ListingTables(weapons, armor, sets));
    }

    /// <summary>The lite items in a weapon or armor bucket, each cut down to what its square and the build check need.</summary>
    public static ManifestPartsQuery ToPartsQuery(ListingTables tables) =>
        new(
            LiteItemComponent,
            BucketKey,
            [.. tables.WeaponBuckets.Keys.Concat(tables.ArmorBuckets.Keys).Select(hash => hash.Value).Order().Select(hash => hash.ToString(CultureInfo.InvariantCulture))],
            ItemPaths);

    /// <summary>
    /// The host's answer to <see cref="ToPartsQuery"/> as the listing: every weapon and armor piece a build can wear,
    /// by name (versions of a name side by side, by hash), with the sets; or why the answer doesn't read.
    /// </summary>
    public static Result<ItemListing, string> ReadListing(ListingTables tables, string partsJson) =>
        ParseDocument(partsJson)
            .MapError(problem => $"its items don't read: {problem}")
            .Map(root => new ItemListing(
                [
                    .. root.EnumerateObject()
                        .Where(group => group.Value.ValueKind == JsonValueKind.Object)
                        .SelectMany(group => group.Value.EnumerateObject())
                        .SelectMany(entry => ToSequence(ReadHashName(entry.Name).Bind(hash => ToListedItem(tables, hash, entry.Value))))
                        .DistinctBy(item => item.Hash)
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.Hash.Value),
                ],
                tables.Sets));

    /// <summary>An item a build can wear, from its parts; none for anything else (a shader, a blue, an ornament's dummy).</summary>
    private static Optional<ManifestItem> ToListedItem(ListingTables tables, ItemHash hash, JsonElement parts)
    {
        var name = parts.ReadText("displayProperties", "name");
        var tier = parts.ReadInteger("inventory", "tierType").Bind(found => ManifestJoin.Tiers.TryGetValue(found, out var known) ? Optional.Some(known) : Optional.None<ItemTier>());
        var wearable = parts.ReadFlag("equippable", false) && name.IsSome() && (tier == Optional.Some(ItemTier.Legendary) || tier == Optional.Some(ItemTier.Exotic));
        var kind = wearable ? ToListedKind(tables, parts) : Optional.None<ManifestKind>();
        return kind.Map(found => new ManifestItem(
            hash,
            name.UnwrapOr(""),
            found,
            parts.ReadText("itemTypeDisplayName"),
            tier,
            Optional.None<string>(),
            parts.ReadString("displayProperties", "icon").Bind(ToPresent),
            parts.ReadString("iconWatermark").Bind(ToPresent)));
    }

    /// <summary>A weapon in a weapon bucket (its slot and damage type), armor in an armor bucket (its slot, set and class); none for anything else.</summary>
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
                Optional.None<ItemHash>()))
            : itemType == Optional.Some(ArmorItemType)
                ? armorSlot.Map(slot => (ManifestKind)new ManifestKind.Armor(
                    slot,
                    Optional.None<ItemHash>(),
                    parts.ReadHash("equippingBlock", "equipableItemSetHash"),
                    parts.ReadInteger("classType").Bind(ManifestJoin.ToGuardianClass)))
                : Optional.None<ManifestKind>();
    }

    /// <summary>A component's entries in the host's answer, each with its hash (a name that isn't one is left out).</summary>
    private static IEnumerable<(ItemHash Hash, JsonElement Entry)> ListEntries(JsonElement root, string component) =>
        root.ReadPath(component).Match(
            entries => entries.Value.ValueKind == JsonValueKind.Object
                ? entries.Value.EnumerateObject()
                    .Where(entry => entry.Value.ValueKind == JsonValueKind.Object)
                    .SelectMany(entry => ToSequence(ReadHashName(entry.Name).Map(hash => (hash, entry.Value))))
                : [],
            _ => []);

    private static Optional<ItemHash> ReadHashName(string name) =>
        uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var hash) ? ToItemHash(hash) : Optional.None<ItemHash>();

    private static Optional<string> ToPresent(string text) =>
        text.Length > 0 ? Optional.Some(text) : Optional.None<string>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}
