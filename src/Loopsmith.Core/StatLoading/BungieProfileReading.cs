using System.Collections.Immutable;
using System.Text.Json;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;

namespace Loopsmith.Core.StatLoading;

/// <summary>
/// Pure: what Loopsmith asks Bungie's API for once the player has logged in, and what it reads of the answers — their
/// Destiny membership, then their profile's armor (worn, carried and in the vault, with the plugs in its sockets) and
/// each character's current stats. Everything else in the answers is left alone.
/// </summary>
public static class BungieProfileReading
{
    private const string Platform = "https://www.bungie.net/Platform";

    /// <summary>The logged-in player's Bungie account and its Destiny memberships.</summary>
    public const string MembershipsUrl = Platform + "/User/GetMembershipsForCurrentUser/";

    /// <summary>
    /// Bungie's ids of the six armor stats, as a character's and an item's stats name them (Mobility, Resilience,
    /// Recovery, Discipline, Intellect and Strength before Edge of Fate renamed them).
    /// </summary>
    private static readonly ImmutableDictionary<uint, StatKind> StatHashes = ImmutableDictionary.CreateRange<uint, StatKind>(
    [
        KeyValuePair.Create(2996146975u, StatKind.Weapons),
        KeyValuePair.Create(392767087u, StatKind.Health),
        KeyValuePair.Create(1943323491u, StatKind.Class),
        KeyValuePair.Create(1735777505u, StatKind.Grenade),
        KeyValuePair.Create(144602215u, StatKind.Super),
        KeyValuePair.Create(4244567218u, StatKind.Melee),
    ]);

    /// <summary>The armor stat a Bungie stat id names, when it is one of the six.</summary>
    public static Optional<StatKind> FindArmorStat(uint statHash) =>
        StatHashes.TryGetValue(statHash, out var stat) ? Optional.Some(stat) : Optional.None<StatKind>();

    /// <summary>
    /// The profile's parts Loopsmith reads: the vault (102), characters with their stats (200), what they carry (201) and
    /// wear (205), item instances (300: an armor piece's energy) and sockets (305: its plugs).
    /// </summary>
    public static string ToProfileUrl(DestinyMembership membership) =>
        $"{Platform}/Destiny2/{membership.Type}/Profile/{Uri.EscapeDataString(membership.Id)}/?components=102,200,201,205,300,305";

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
                .Bind(id => memberships.Where(entry => entry.Membership.Id == id).Select(Optional.Some).FindFirstSome());
            var crossSaved = memberships.Where(entry => entry.CrossSave == 0 || entry.CrossSave == entry.Membership.Type).Select(Optional.Some).FindFirstSome();
            return primary.Match(found => Optional.Some(found.Value), _ => crossSaved)
                .Match(found => Optional.Some(found.Value), _ => memberships.Select(Optional.Some).FindFirstSome())
                .Map(entry => entry.Membership)
                .ToResult(() => "Your Bungie account has no Destiny 2 characters.");
        });

    private sealed record MembershipEntry(DestinyMembership Membership, int CrossSave);

    private static Optional<MembershipEntry> ReadMembershipEntry(JsonElement entry) =>
        entry.ReadInteger("membershipType").Bind(type => entry.ReadString("membershipId").Map(id =>
            new MembershipEntry(new DestinyMembership(type, id), entry.ReadInteger("crossSaveOverride").UnwrapOr(0))));

    /// <summary>
    /// The profile's armor — every instanced item with armor energy, worn, carried or in the vault — and each
    /// character's class and current stats.
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
            var armor = placed.SelectMany(entry => ToSequence(ReadArmor(response, entry.Item, entry.Place))).ToImmutableArray();
            var characters = response.ReadProperties("characters", "data")
                .SelectMany(character => ToSequence(ToClass(character.Value.ReadInteger("classType"))
                    .Map(found => new CharacterStats(found, ReadStatLine(character.Value)))))
                .ToImmutableArray();
            return new BungieProfile(armor, characters);
        });

    private static IEnumerable<(JsonElement Item, ArmorPlace Place)> ListCharacterItems(
        JsonElement response, string component, ImmutableDictionary<string, GuardianClass> classes, Func<GuardianClass, ArmorPlace> place) =>
        response.ReadProperties(component, "data")
            .Where(character => classes.ContainsKey(character.Key))
            .SelectMany(character => character.Value.ReadArray("items").Select(item => (Item: item, Place: place(classes[character.Key]))));

    /// <summary>An item that is a piece of armor: it has an instance, and the instance has armor energy.</summary>
    private static Optional<OwnedArmor> ReadArmor(JsonElement response, JsonElement item, ArmorPlace place) =>
        item.ReadString("itemInstanceId").Bind(instance => item.ReadHash("itemHash").Bind(hash =>
            response.ReadPath("itemComponents", "instances", "data", instance, "energy").Map(energy => new OwnedArmor(
                instance,
                hash,
                place,
                [.. response.ReadArray("itemComponents", "sockets", "data", instance, "sockets")
                    .Where(socket => socket.ReadFlag("isEnabled", true))
                    .SelectMany(socket => ToSequence(socket.ReadHash("plugHash")))],
                energy.ReadInteger("energyCapacity")))));

    /// <summary>A character's six armor stats from its <c>stats</c> (Bungie's stat id → value; a value outside 0..200 is unknown).</summary>
    private static StatLine ReadStatLine(JsonElement character)
    {
        var values = character.ReadProperties("stats")
            .SelectMany(stat => uint.TryParse(stat.Key, out var hash) ? ToSequence(FindArmorStat(hash).Map(found => (Stat: found, Value: stat.Value))) : [])
            .SelectMany(stat => stat.Value.ValueKind == JsonValueKind.Number && stat.Value.TryGetInt32(out var number)
                ? ToSequence(StatValue.TryFrom(number).ToOptional().Map(value => KeyValuePair.Create(stat.Stat, value)))
                : [])
            .ToImmutableDictionary();
        return new StatLine(
            ReadValue(values, StatKind.Weapons),
            ReadValue(values, StatKind.Health),
            ReadValue(values, StatKind.Class),
            ReadValue(values, StatKind.Grenade),
            ReadValue(values, StatKind.Super),
            ReadValue(values, StatKind.Melee));
    }

    private static Optional<StatValue> ReadValue(ImmutableDictionary<StatKind, StatValue> values, StatKind stat) =>
        values.TryGetValue(stat, out var value) ? Optional.Some(value) : Optional.None<StatValue>();

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
