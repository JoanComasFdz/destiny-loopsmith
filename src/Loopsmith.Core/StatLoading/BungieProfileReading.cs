using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: what Loopsmith asks Bungie's API for once the player has logged in, and what it reads of the answers — their
/// Destiny membership, then their profile's armor (with the plugs in its sockets) and weapons, worn, carried and in the
/// vault, each copy with its marks (gear tier, masterwork, breaker), and the finisher each character has equipped.
/// Everything else in the answers is left alone.
/// </summary>
public static class BungieProfileReading
{
    private const string Platform = "https://www.bungie.net/Platform";

    /// <summary>The logged-in player's Bungie account and its Destiny memberships.</summary>
    public const string MembershipsUrl = Platform + "/User/GetMembershipsForCurrentUser/";

    /// <summary>
    /// The profile's parts Loopsmith reads: the vault (102), characters (200: each one's class), what they carry (201) and
    /// wear (205; an item's <c>state</c>: masterworked; the item in its Finishers slot), item instances (300: an armor piece's energy, a weapon's damage
    /// type, a copy's gear tier and breaker) and sockets (305: its plugs — an armor piece's stats, a weapon's perks).
    /// </summary>
    public static string ToProfileUrl(DestinyMembership membership) =>
        $"{Platform}/Destiny2/{membership.Type.Value.ToString(CultureInfo.InvariantCulture)}/Profile/{membership.Id.Value}/?components=102,200,201,205,300,305";

    /// <summary>
    /// Bungie's answer to a request, as text: its JSON says what went wrong, even with an error status, so the readers
    /// above read it; a server error that isn't JSON (Bungie down) is said as such.
    /// </summary>
    public static Result<string, string> ReadAnswer(int status, string text) =>
        status >= 500 && !text.TrimStart().StartsWith('{')
            ? new Result<string, string>.Error($"Bungie didn't answer (HTTP {status.ToString(CultureInfo.InvariantCulture)}). Try again later.")
            : new Result<string, string>.Ok(text);

    /// <summary>Why Bungie couldn't be asked: the browser couldn't reach it (<paramref name="why"/> is what it said).</summary>
    public static string DescribeUnreachable(string why) =>
        $"Couldn't reach Bungie ({why}). Check your connection and try again.";

    /// <summary>
    /// The membership to read: the cross-save primary one when the account has it, else the one its cross save
    /// points to, else the first; none when the account plays no Destiny 2.
    /// </summary>
    public static Result<DestinyMembership, string> ReadMembership(string json) =>
        BungieJsonReading.ReadResponse(json).Bind(response =>
        {
            var memberships = response.ReadArray("destinyMemberships")
                .SelectMany(entry => ToSequence(ReadMembershipEntry(entry)))
                .ToImmutableArray();
            var primary = response.ReadString("primaryMembershipId")
                .Bind(id => memberships.Where(entry => entry.Membership.Id.Value == id).Select(Optional.Some).FindFirstSome());
            var crossSaved = memberships.Where(entry => entry.CrossSave == 0 || entry.CrossSave == entry.Membership.Type.Value).Select(Optional.Some).FindFirstSome();
            return primary.Match(found => Optional.Some(found.Value), _ => crossSaved)
                .Match(found => Optional.Some(found.Value), _ => memberships.Select(Optional.Some).FindFirstSome())
                .Map(entry => entry.Membership)
                .ToResult(() => "Your Bungie account has no Destiny 2 characters.");
        });

    /// <summary>A membership and the platform its cross save points to (0: none).</summary>
    private sealed record MembershipEntry(DestinyMembership Membership, int CrossSave);

    private static Optional<MembershipEntry> ReadMembershipEntry(JsonElement entry) =>
        entry.ReadInteger("membershipType").Bind(type => MembershipType.TryFrom(type).ToOptional())
            .Bind(type => entry.ReadString("membershipId").Bind(id => MembershipId.TryFrom(id).ToOptional())
                .Map(id => new MembershipEntry(new DestinyMembership(type, id), entry.ReadInteger("crossSaveOverride").UnwrapOr(0))));

    /// <summary>
    /// The profile's armor (every instanced item with armor energy) and weapons (every instanced item with a damage type),
    /// worn, carried or in the vault (by which class), each copy with its marks; and each character's equipped finisher
    /// (the item it wears in the Finishers bucket).
    /// </summary>
    public static Result<BungieProfile, string> ReadProfile(string json) =>
        BungieJsonReading.ReadResponse(json).Map(response =>
        {
            var classes = response.ReadProperties("characters", "data")
                .SelectMany(character => ToSequence(ToClass(character.Value.ReadInteger("classType")).Map(found => KeyValuePair.Create(character.Key, found))))
                .ToImmutableDictionary();
            var placed = response.ReadArray("profileInventory", "data", "items").Select(item => (Item: item, Place: (ArmorPlace)new ArmorPlace.Vault()))
                .Concat(ListCharacterItems(response, "characterEquipment", classes, found => new ArmorPlace.Equipped(found)))
                .Concat(ListCharacterItems(response, "characterInventories", classes, found => new ArmorPlace.Carried(found)));
            var items = placed.ToImmutableArray();
            return new BungieProfile(
                [.. items.SelectMany(entry => ToSequence(ReadArmor(response, entry.Item, entry.Place)))],
                [.. items.SelectMany(entry => ToSequence(ReadWeapon(response, entry.Item, entry.Place)))],
                [.. items.SelectMany(entry => ToSequence(ReadFinisher(entry.Item, entry.Place)))]);
        });

    /// <summary>Bungie's Finishers bucket: the slot a character's finisher is equipped in.</summary>
    private const uint FinishersBucket = 3683254069;

    /// <summary>
    /// An item a character wears in the Finishers bucket: that character's finisher (a finisher, or the "Finishers" item:
    /// a random one of theirs each time). One carried or in the vault isn't equipped.
    /// </summary>
    private static Optional<EquippedFinisher> ReadFinisher(JsonElement item, ArmorPlace place) =>
        place is ArmorPlace.Equipped equipped && item.ReadHash("bucketHash") == Optional.Some(ItemHash.From(FinishersBucket))
            ? item.ReadHash("itemHash").Map(hash => new EquippedFinisher(equipped.Class, hash))
            : Optional.None<EquippedFinisher>();

    private static IEnumerable<(JsonElement Item, ArmorPlace Place)> ListCharacterItems(
        JsonElement response, string component, ImmutableDictionary<string, GuardianClass> classes, Func<GuardianClass, ArmorPlace> place) =>
        response.ReadProperties(component, "data")
            .Where(character => classes.ContainsKey(character.Key))
            .SelectMany(character => character.Value.ReadArray("items").Select(item => (Item: item, Place: place(classes[character.Key]))));

    /// <summary>An item that is a piece of armor: it has an instance, and the instance has armor energy.</summary>
    private static Optional<OwnedArmor> ReadArmor(JsonElement response, JsonElement item, ArmorPlace place) =>
        item.ReadString("itemInstanceId").Bind(instance => ItemInstanceId.TryFrom(instance).ToOptional().Bind(id => item.ReadHash("itemHash").Bind(hash =>
            response.ReadPath("itemComponents", "instances", "data", instance, "energy").Map(energy => new OwnedArmor(
                id,
                hash,
                place,
                ReadPlugs(response, instance),
                energy.ReadInteger("energyCapacity"),
                ReadMarks(response, item, instance))))));

    /// <summary>The plugs in a copy's sockets: the enabled ones, socket by socket.</summary>
    private static ImmutableArray<ItemHash> ReadPlugs(JsonElement response, string instance) =>
    [
        .. response.ReadArray("itemComponents", "sockets", "data", instance, "sockets")
            .Where(socket => socket.ReadFlag("isEnabled", true))
            .SelectMany(socket => ToSequence(socket.ReadHash("plugHash"))),
    ];

    /// <summary>An item that is a weapon: it has an instance, and the instance a damage type (armor's has none).</summary>
    private static Optional<OwnedWeapon> ReadWeapon(JsonElement response, JsonElement item, ArmorPlace place) =>
        item.ReadString("itemInstanceId").Bind(instance => ItemInstanceId.TryFrom(instance).ToOptional().Bind(id => item.ReadHash("itemHash").Bind(hash =>
            response.ReadInteger("itemComponents", "instances", "data", instance, "damageType")
                .Bind(type => type > 0 ? Optional.Some(type) : Optional.None<int>())
                .Map(_ => new OwnedWeapon(id, hash, place, ReadMarks(response, item, instance), ReadPlugs(response, instance))))));

    /// <summary>
    /// A copy's marks: its instance's <c>gearTier</c> (1–5), the item's <c>state</c> masterwork flag (4), its instance's
    /// <c>breakerTypeHash</c> (0: none) and its power level (its instance's <c>primaryStat</c> value).
    /// </summary>
    private static GearMarks ReadMarks(JsonElement response, JsonElement item, string instance) =>
        new(
            response.ReadInteger("itemComponents", "instances", "data", instance, "gearTier").Bind(tier => GearTier.TryFrom(tier).ToOptional()),
            (item.ReadInteger("state").UnwrapOr(0) & MasterworkState) != 0,
            response.ReadHash("itemComponents", "instances", "data", instance, "breakerTypeHash"),
            response.ReadInteger("itemComponents", "instances", "data", instance, "primaryStat", "value").Bind(power => PowerLevel.TryFrom(power).ToOptional()));

    /// <summary>Bungie's <c>ItemState.Masterwork</c>: the item has a masterwork plug.</summary>
    private const int MasterworkState = 4;

    /// <summary>Bungie's class type: 0 Titan, 1 Hunter, 2 Warlock (3: unknown).</summary>
    private static Optional<GuardianClass> ToClass(Optional<int> classType) =>
        classType.Bind(type => type switch
        {
            0 => Optional.Some(GuardianClass.Titan),
            1 => Optional.Some(GuardianClass.Hunter),
            2 => Optional.Some(GuardianClass.Warlock),
            _ => Optional.None<GuardianClass>(),
        });

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
