using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dunet;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using static Loopsmith.Core.ManifestJoining.ManifestEntryReading;

namespace Loopsmith.Core.ManifestJoining;

/// <summary>The copy of Bungie's manifest the browser saved: each component's URL (they name its version) and its size, unpacked (when it was saved).</summary>
public sealed record SavedManifest(ImmutableDictionary<string, string> Urls, Optional<long> Bytes);

/// <summary>
/// Where the tab gets the manifest's components: the browser's saved copy (and the URLs to download when it can't be
/// read after all), or a download of these URLs.
/// </summary>
[Union]
public partial record ManifestSource
{
    partial record Saved(SavedManifest Copy, ImmutableDictionary<string, string> Fallback);
    partial record Download(ImmutableDictionary<string, string> Urls);
}

/// <summary>
/// Pure: the copy of Bungie's manifest the browser keeps between visits, as DIM keeps its own (ADRs D14, D25) — whether
/// it is still the current version (the manifest index says which), and the listing of every weapon and armor piece
/// saved with it: its key (the manifest version it was read from and the Loopsmith code that made it) and its text.
/// </summary>
public static class ManifestSaving
{
    /// <summary>
    /// The saved copy the host found (<c>{ "urls": { component: url }, "bytes": n }</c>), when it has every component;
    /// none when there's none or it doesn't read.
    /// </summary>
    public static Optional<SavedManifest> ReadSavedManifest(Optional<string> savedJson) =>
        savedJson.Bind(json => ParseDocument(json).Match(
            root => ReadSavedUrls(root.Value).Map(urls => new SavedManifest(urls, ReadBytes(root.Value))),
            _ => Optional.None<SavedManifest>()));

    /// <summary>
    /// The saved copy when it is the version the manifest index names; when the index doesn't answer, the saved copy
    /// still (the latest the browser saw); else a download: the index's version, or the pinned one without an index.
    /// </summary>
    public static ManifestSource ChooseSource(Optional<string> indexJson, string pinnedContentId, Optional<SavedManifest> saved)
    {
        var current = ManifestTransport.ReadCurrentUrls(indexJson);
        var download = ManifestTransport.ChooseUrls(indexJson, pinnedContentId);
        var usable = saved.Bind(copy => current.Match(
            urls => IsSameVersion(copy.Urls, urls.Value) ? Optional.Some(copy) : Optional.None<SavedManifest>(),
            _ => Optional.Some(copy)));
        return usable.Match<ManifestSource>(
            copy => new ManifestSource.Saved(copy.Value, download),
            _ => new ManifestSource.Download(download));
    }

    /// <summary>
    /// The key a listing is saved under: the manifest version it was read from (its components' URLs) and the code that
    /// made it (<paramref name="codeVersion"/>, the host's), so a new version or a new Loopsmith lists again.
    /// </summary>
    public static string ToListingKey(ImmutableDictionary<string, string> urls, string codeVersion) =>
        string.Join("\n", [codeVersion, .. urls.OrderBy(url => url.Key, StringComparer.Ordinal).Select(url => $"{url.Key} {url.Value}")]);

    /// <summary>The listing as the browser saves it (JSON).</summary>
    public static string ToListingText(ItemListing listing) =>
        new JsonObject
        {
            ["items"] = new JsonArray([.. listing.Items.SelectMany(item => ToSequence(ToItemNode(item)))]),
            ["sets"] = new JsonArray([.. listing.Sets.Values.OrderBy(set => set.Hash.Value).Select(ToSetNode)]),
            ["mods"] = new JsonArray([.. listing.Mods.Select(ToModNode)]),
            ["modSockets"] = new JsonArray([.. listing.ModSockets.OrderBy(piece => piece.Key.Value).Select(piece => ToModSocketsNode(piece.Key, piece.Value))]),
        }.ToJsonString();

    /// <summary>The listing the browser saved, or why it doesn't read.</summary>
    public static Result<ItemListing, string> ReadListingText(string text) =>
        ParseDocument(text)
            .MapError(problem => $"the saved listing doesn't read: {problem}")
            .Bind(ReadListing);

    private static Result<ItemListing, string> ReadListing(JsonElement root) =>
        root.ReadPath("items").IsSome() && root.ReadPath("sets").IsSome()
            ? new Result<ItemListing, string>.Ok(new ItemListing(
                [.. root.ReadArray("items").SelectMany(item => ToSequence(ReadItem(item)))],
                root.ReadArray("sets").SelectMany(set => ToSequence(ReadSet(set))).ToImmutableDictionary(set => set.Hash),
                [.. root.ReadArray("mods").SelectMany(mod => ToSequence(ReadMod(mod)))],
                root.ReadArray("modSockets").SelectMany(piece => ToSequence(ReadModSockets(piece))).ToImmutableDictionary(piece => piece.Piece, piece => piece.Sockets)))
            : new Result<ItemListing, string>.Error("the saved listing has no items or sets");

    // ── the saved copy ──────────────────────────────────────────────────────────────

    private static bool IsSameVersion(ImmutableDictionary<string, string> saved, ImmutableDictionary<string, string> current) =>
        saved.Count == current.Count && current.All(url => saved.TryGetValue(url.Key, out var held) && held == url.Value);

    /// <summary>The saved URLs, when they are every component's.</summary>
    private static Optional<ImmutableDictionary<string, string>> ReadSavedUrls(JsonElement root)
    {
        var urls = ManifestTransport.Components
            .SelectMany(name => ToSequence(root.ReadString("urls", name)).Select(url => KeyValuePair.Create(name, url)))
            .ToImmutableDictionary();
        return urls.Count == ManifestTransport.Components.Length ? Optional.Some(urls) : Optional.None<ImmutableDictionary<string, string>>();
    }

    private static Optional<long> ReadBytes(JsonElement root) =>
        root.ReadPath("bytes").Bind(found => found.ValueKind == JsonValueKind.Number && found.TryGetInt64(out var bytes) && bytes > 0
            ? Optional.Some(bytes)
            : Optional.None<long>());

    // ── the listing, written: only weapons and armor (all a listing holds) ──────────

    private static Optional<JsonNode> ToItemNode(ManifestItem item)
    {
        var kind = item.Kind switch
        {
            ManifestKind.Weapon weapon => Optional.Some(KeyValuePair.Create("weapon", ToWeaponNode(weapon))),
            ManifestKind.Armor armor => Optional.Some(KeyValuePair.Create("armor", ToArmorNode(armor))),
            _ => Optional.None<KeyValuePair<string, JsonNode>>(),
        };
        return kind.Map(found => (JsonNode)ToObject(
        [
            KeyValuePair.Create("hash", Optional.Some<JsonNode>(item.Hash.Value)),
            KeyValuePair.Create("name", Optional.Some<JsonNode>(item.Name)),
            KeyValuePair.Create("type", item.Type.Map(text => (JsonNode)text)),
            KeyValuePair.Create("tier", item.Tier.Map(tier => (JsonNode)tier.ToString())),
            KeyValuePair.Create("description", item.Description.Map(text => (JsonNode)text)),
            KeyValuePair.Create("icon", item.Icon.Map(text => (JsonNode)text)),
            KeyValuePair.Create("watermark", item.Watermark.Map(text => (JsonNode)text)),
            KeyValuePair.Create(found.Key, Optional.Some(found.Value)),
        ]));
    }

    private static JsonNode ToWeaponNode(ManifestKind.Weapon weapon) =>
        ToObject(
        [
            KeyValuePair.Create("slot", Optional.Some<JsonNode>(weapon.Slot.ToString())),
            KeyValuePair.Create("damage", weapon.DamageType.Map(type => (JsonNode)type.ToString())),
            KeyValuePair.Create("ammo", weapon.Ammo.Map(ammo => (JsonNode)ammo.ToString())),
            KeyValuePair.Create("traits", Optional.Some<JsonNode>(new JsonArray([.. weapon.Traits.Select(column => (JsonNode)ToHashArray(column.Options))]))),
            KeyValuePair.Create("intrinsic", weapon.Intrinsic.Map(ToHashNode)),
            KeyValuePair.Create("catalyst", weapon.Catalyst.Map(ToHashNode)),
            KeyValuePair.Create("breaker", weapon.Breaker.Map(ToHashNode)),
        ]);

    private static JsonNode ToArmorNode(ManifestKind.Armor armor) =>
        ToObject(
        [
            KeyValuePair.Create("slot", Optional.Some<JsonNode>(armor.Slot.ToString())),
            KeyValuePair.Create("intrinsic", armor.Intrinsic.Map(ToHashNode)),
            KeyValuePair.Create("set", armor.Set.Map(ToHashNode)),
            KeyValuePair.Create("class", armor.Class.Map(found => (JsonNode)found.ToString())),
        ]);

    /// <summary>A mod as the listing saves it: its item's fields, its category, cost and requirements.</summary>
    private static JsonNode ToModNode(ListedMod mod) =>
        ToObject(
        [
            KeyValuePair.Create("hash", Optional.Some<JsonNode>(mod.Item.Hash.Value)),
            KeyValuePair.Create("name", Optional.Some<JsonNode>(mod.Item.Name)),
            KeyValuePair.Create("type", mod.Item.Type.Map(text => (JsonNode)text)),
            KeyValuePair.Create("tier", mod.Item.Tier.Map(tier => (JsonNode)tier.ToString())),
            KeyValuePair.Create("description", mod.Item.Description.Map(text => (JsonNode)text)),
            KeyValuePair.Create("icon", mod.Item.Icon.Map(text => (JsonNode)text)),
            KeyValuePair.Create("category", Optional.Some<JsonNode>(mod.Category.Value)),
            KeyValuePair.Create("cost", Optional.Some<JsonNode>(mod.Cost.Value)),
            KeyValuePair.Create("requirements", Optional.Some<JsonNode>(new JsonArray([.. mod.Requirements.Select(text => (JsonNode)text)]))),
        ]);

    private static JsonNode ToModSocketsNode(ItemHash piece, ImmutableArray<PlugCategoryId> sockets) =>
        new JsonObject
        {
            ["piece"] = piece.Value,
            ["sockets"] = new JsonArray([.. sockets.Select(category => (JsonNode)category.Value)]),
        };

    private static JsonNode ToSetNode(ArmorSet set) =>
        new JsonObject
        {
            ["hash"] = set.Hash.Value,
            ["name"] = set.Name,
            ["perks"] = new JsonArray([.. set.Perks.Select(perk => (JsonNode)new JsonObject { ["pieces"] = perk.Pieces.Value, ["perk"] = perk.Perk.Value })]),
        };

    /// <summary>An object of the fields that are there (a missing one is left out).</summary>
    private static JsonObject ToObject(ImmutableArray<KeyValuePair<string, Optional<JsonNode>>> fields) =>
        new(fields.SelectMany(field => ToSequence(field.Value).Select(value => KeyValuePair.Create(field.Key, (JsonNode?)value))));

    private static JsonNode ToHashNode(ItemHash hash) => hash.Value;

    private static JsonArray ToHashArray(ImmutableArray<ItemHash> hashes) =>
        new([.. hashes.Select(hash => (JsonNode)hash.Value)]);

    // ── the listing, read: a field that doesn't read is none, an item without a name, hash or kind is left out ──

    private static Optional<ManifestItem> ReadItem(JsonElement item)
    {
        var kind = item.ReadPath("weapon").Bind(ReadWeapon)
            .Match(found => Optional.Some(found.Value), _ => item.ReadPath("armor").Bind(ReadArmor));
        return item.ReadHash("hash").Bind(hash => item.ReadString("name").Bind(name => kind.Map(found => new ManifestItem(
            hash,
            name,
            found,
            item.ReadString("type"),
            item.ReadString("tier").Bind(ReadEnum<ItemTier>),
            item.ReadString("description"),
            item.ReadString("icon"),
            item.ReadString("watermark")))));
    }

    private static Optional<ManifestKind> ReadWeapon(JsonElement weapon) =>
        weapon.ReadString("slot").Bind(ReadEnum<WeaponSlot>).Map(slot => (ManifestKind)new ManifestKind.Weapon(
            slot,
            weapon.ReadString("damage").Bind(ReadEnum<DamageType>),
            weapon.ReadString("ammo").Bind(ReadEnum<AmmoType>),
            [.. weapon.ReadArray("traits").Select(ReadTraitColumn)],
            weapon.ReadHash("intrinsic"),
            weapon.ReadHash("catalyst"),
            weapon.ReadHash("breaker"),
            []));

    private static TraitColumn ReadTraitColumn(JsonElement column) =>
        new(column.ValueKind == JsonValueKind.Array ? [.. column.EnumerateArray().SelectMany(hash => ToSequence(hash.ToItemHash()))] : []);

    private static Optional<ManifestKind> ReadArmor(JsonElement armor) =>
        armor.ReadString("slot").Bind(ReadEnum<ArmorSlot>).Map(slot => (ManifestKind)new ManifestKind.Armor(
            slot,
            armor.ReadHash("intrinsic"),
            armor.ReadHash("set"),
            armor.ReadString("class").Bind(ReadEnum<GuardianClass>)));

    private static Optional<ListedMod> ReadMod(JsonElement mod)
    {
        var category = mod.ReadString("category").Bind(ToPlugCategory);
        var cost = mod.ReadInteger("cost").Bind(found => ArmorEnergy.TryFrom(found).IsSuccess ? Optional.Some(ArmorEnergy.From(found)) : Optional.None<ArmorEnergy>());
        return mod.ReadHash("hash").Bind(hash => mod.ReadString("name").Bind(name => category.Bind(found => cost.Map(energy => new ListedMod(
            new ManifestItem(
                hash,
                name,
                new ManifestKind.ArmorMod(ArmorModCategories.FindSlot(found)),
                mod.ReadString("type"),
                mod.ReadString("tier").Bind(ReadEnum<ItemTier>),
                mod.ReadString("description"),
                mod.ReadString("icon"),
                Optional.None<string>()),
            found,
            energy,
            [.. mod.ReadArray("requirements").SelectMany(text => text.ValueKind == JsonValueKind.String ? ToSequence(Optional.FromNullable(text.GetString())) : [])])))));
    }

    private static Optional<(ItemHash Piece, ImmutableArray<PlugCategoryId> Sockets)> ReadModSockets(JsonElement piece) =>
        piece.ReadHash("piece").Map(hash => (hash, piece.ReadArray("sockets")
            .SelectMany(category => category.ValueKind == JsonValueKind.String ? ToSequence(Optional.FromNullable(category.GetString()).Bind(ToPlugCategory)) : [])
            .ToImmutableArray()));

    private static Optional<ArmorSet> ReadSet(JsonElement set) =>
        set.ReadHash("hash").Bind(hash => set.ReadString("name").Map(name => new ArmorSet(
            hash,
            name,
            [.. set.ReadArray("perks").SelectMany(perk => ToSequence(perk.ReadInteger("pieces").Bind(ToPieceCount)
                .Bind(pieces => perk.ReadHash("perk").Map(found => new SetPerk(pieces, found)))))])));

    private static Optional<T> ReadEnum<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, ignoreCase: false, out var found) && Enum.IsDefined(found) && !int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? Optional.Some(found)
            : Optional.None<T>();

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(found => new[] { found.Value }, _ => []);
}
