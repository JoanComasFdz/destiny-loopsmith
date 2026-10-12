using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.ManifestJoining;

/// <summary>
/// The listing of every legendary and exotic weapon and armor piece of the game (the pickers without a Bungie login): each
/// name once per slot (and class), its newest version, with what a picker draws. A fixture shaped like Bungie's JSON always
/// runs; the real manifest, when one is downloaded (<c>LOOPSMITH_MANIFEST_DIR</c>), is checked against known items.
/// </summary>
public sealed class GameItemListingTests
{
    [Fact]
    public void A_name_is_listed_once_in_its_newest_version_with_its_frame_and_breaker()
    {
        var store = new ManifestStore(Fixture.Tables);

        var listed = store.ListGameItems();

        Assert.Equal([(101u, "Test Cannon"), (102u, "Test Thunder")], listed.Weapons.Select(item => (item.Hash.Value, item.Name)));
        var cannon = Assert.IsType<ManifestKind.Weapon>(listed.Weapons[0].Kind);
        Assert.Equal((WeaponSlot.Kinetic, Optional.Some(DamageType.Kinetic), Optional.Some(ItemHash.From(20u))), (cannon.Slot, cannon.DamageType, cannon.Intrinsic));
        Assert.Equal(Optional.Some(ItemHash.From(485622768u)), cannon.Breaker);                  // its frame's perk is "[Shield-Piercing] Barrier"
        Assert.Empty(cannon.Traits);                                                             // the join reads the columns of the one picked
        Assert.Equal((Optional.Some("Hand Cannon"), Optional.Some(ItemTier.Legendary), Optional.Some("/cannon-new.jpg"), Optional.Some("/new-season.png")),
            (listed.Weapons[0].Type, listed.Weapons[0].Tier, listed.Weapons[0].Icon, listed.Weapons[0].Watermark));
        Assert.Equal(Optional.Some(ItemHash.From(2611060930u)), Assert.IsType<ManifestKind.Weapon>(listed.Weapons[1].Kind).Breaker); // an exotic's own
        Assert.Equal(("Adaptive Frame", Optional.Some("/adaptive.png")), (listed.Intrinsics[ItemHash.From(20u)].Name, listed.Intrinsics[ItemHash.From(20u)].Icon));
        Assert.InRange(store.Rounds, 3, 6);
    }

    [Fact]
    public void Armor_is_listed_per_class_with_its_set_and_nothing_else_is()
    {
        var listed = new ManifestStore(Fixture.Tables).ListGameItems();

        Assert.Equal([(200u, GuardianClass.Hunter), (201u, GuardianClass.Titan)], listed.Armor.Select(item => (item.Hash.Value, Assert.IsType<ManifestKind.Armor>(item.Kind).Class.UnwrapOr(GuardianClass.Warlock))));
        Assert.Equal(Optional.Some(ItemHash.From(500u)), Assert.IsType<ManifestKind.Armor>(listed.Armor[0].Kind).Set);
        Assert.DoesNotContain(listed.Weapons.Concat(listed.Armor), item => item.Hash.Value is 100u or 103u or 104u or 105u or 106u or 107u or 300u);
    }

    [Fact]
    public void A_listings_question_becomes_the_hosts_requests_and_its_json_answer_the_listings()
    {
        var query = new ManifestListQuery(
            [ManifestTable.InventoryBucket],
            ["\"bucketTypeHash\":1"],
            [ItemHash.From(7u)],
            ManifestJoin.OutlinePaths,
            [new ManifestEntryKey(ManifestTable.SandboxPerk, ItemHash.From(9u))]);
        var none = query with { Outlines = [] };

        var request = ManifestTransport.ToListEntryRequest(query);
        var outlines = ManifestTransport.ToOutlineRequest(query);
        var answer = ManifestTransport.ReadListAnswer(
            query,
            """{ "DestinySandboxPerkDefinition": { "9": { "hash": 9 } } }""",
            """{ "DestinyInventoryItemDefinition": { "7": { "itemType": 3 } } }""",
            """{ "\"bucketTypeHash\":1": [7, 0, "no"], "other": [1] }""");

        Assert.Equal([9u], request["DestinySandboxPerkDefinition"].Select(hash => hash.Value));
        Assert.Empty(request["DestinyInventoryBucketDefinition"]);                               // the whole table
        Assert.Equal([7u], Assert.Single(outlines).Value.Select(hash => hash.Value));
        Assert.Empty(ManifestTransport.ToOutlineRequest(none));                                  // never an empty list: that would be the whole table
        var read = Assert.IsType<Result<ManifestListAnswer, string>.Ok>(answer).Value;
        Assert.Equal(2, read.Read.Entries.Count);
        Assert.Equal([7u], Assert.Single(read.Found).Value.Select(hash => hash.Value));         // 0 and "no" aren't hashes; "other" wasn't asked
        Assert.IsType<Result<ManifestListAnswer, string>.Error>(ManifestTransport.ReadListAnswer(query, "{}", "not json", "{}"));
    }

    /// <summary>The real manifest: a reissued weapon and an exotic with three versions are listed once, in their newest version.</summary>
    [Fact]
    public void The_real_manifest_lists_each_weapon_and_armor_piece_once_in_its_newest_version()
    {
        var directory = Environment.GetEnvironmentVariable("LOOPSMITH_MANIFEST_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set LOOPSMITH_MANIFEST_DIR to a downloaded manifest (tools/manifest/get-manifest.sh) to run it.");
        var store = ManifestStore.ReadDirectory(directory!);

        var listed = store.ListGameItems();
        var hunterArms = listed.Armor.Where(item => item.Kind is ManifestKind.Armor { Slot: ArmorSlot.Arms, Class: Optional<GuardianClass>.Some { Value: GuardianClass.Hunter } }).ToImmutableArray();

        Assert.Equal([4184168210u], listed.Weapons.Where(item => item.Name == "Fatebringer").Select(item => item.Hash.Value));
        Assert.Equal([1611241895u], hunterArms.Where(item => item.Name == "Shinobu's Vow").Select(item => item.Hash.Value));
        var thunderlord = Assert.Single(listed.Weapons, item => item.Name == "Thunderlord");
        Assert.Equal((WeaponSlot.Power, Optional.Some(ItemTier.Exotic)), (Assert.IsType<ManifestKind.Weapon>(thunderlord.Kind).Slot, thunderlord.Tier));
        Assert.InRange(listed.Weapons.Length, 900, 1600);
        Assert.InRange(hunterArms.Length, 100, 260);
        Assert.DoesNotContain(listed.Weapons.GroupBy(item => (Assert.IsType<ManifestKind.Weapon>(item.Kind).Slot, item.Name)), group => group.Count() > 1);
        Assert.All(listed.Weapons.Where(item => item.Tier == Optional.Some(ItemTier.Legendary)).Take(50), item => Assert.True(Assert.IsType<ManifestKind.Weapon>(item.Kind).Intrinsic.IsSome(), item.Name));
    }

    /// <summary>
    /// Bungie-shaped entries: a hand cannon in two versions (and a blue, a dummy, a weapon with no damage type, a redacted
    /// and an unequippable one of the same slot), an exotic with its own breaker, a helmet for Hunters and one for Titans, a fragment.
    /// </summary>
    private static class Fixture
    {
        private const string Buckets = """{"1":{"displayProperties":{"name":"Kinetic Weapons"}},"2":{"displayProperties":{"name":"Helmet"}},"3":{"displayProperties":{"name":"Consumables"}}}""";
        private const string Breakers = """{"485622768":{"displayProperties":{"name":"Shield Piercing"}},"2611060930":{"displayProperties":{"name":"Disruption"}}}""";
        private const string Categories = """{"7":{"displayProperties":{"name":"WEAPON PERKS"}},"8":{"displayProperties":{"name":"INTRINSIC TRAITS"}}}""";
        private const string Perks = """{"42":{"isDisplayable":false,"displayProperties":{"name":"[Shield-Piercing] Barrier"}},"600":{"isDisplayable":true,"displayProperties":{"name":"Two"}}}""";
        private const string Sets = """{"500":{"displayProperties":{"name":"Testing"},"setPerks":[{"requiredSetCount":2,"sandboxPerkHash":600}]}}""";

        private const string Sockets = """
            "sockets": { "socketEntries": [ { "socketTypeHash": 71, "singleInitialItemHash": 20 }, { "socketTypeHash": 70, "randomizedPlugSetHash": 80 } ],
                         "socketCategories": [ { "socketCategoryHash": 8, "socketIndexes": [0] }, { "socketCategoryHash": 7, "socketIndexes": [1] } ] }
            """;

        private static readonly string Items = $$"""
            {
              "100": { "displayProperties": { "name": "Test Cannon", "icon": "/cannon-old.jpg" }, "iconWatermark": "/old-season.png", "itemTypeDisplayName": "Hand Cannon", "itemType": 3, "index": 10, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 1, {{Sockets}} },
              "101": { "displayProperties": { "name": "Test Cannon", "icon": "/cannon-new.jpg" }, "iconWatermark": "/new-season.png", "itemTypeDisplayName": "Hand Cannon", "itemType": 3, "index": 20, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 1, {{Sockets}} },
              "102": { "displayProperties": { "name": "Test Thunder" }, "itemTypeDisplayName": "Hand Cannon", "itemType": 3, "index": 5, "equippable": true, "breakerTypeHash": 2611060930, "inventory": { "bucketTypeHash": 1, "tierType": 6 }, "defaultDamageType": 2 },
              "103": { "displayProperties": { "name": "Blue Cannon" }, "itemType": 3, "index": 1, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 4 }, "defaultDamageType": 1 },
              "104": { "displayProperties": { "name": "Dummy Cannon" }, "itemType": 20, "index": 1, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 1 },
              "105": { "displayProperties": { "name": "Plain Cannon" }, "itemType": 3, "index": 1, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 0 },
              "106": { "displayProperties": { "name": "Classified" }, "itemType": 3, "index": 1, "equippable": true, "redacted": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 1 },
              "107": { "displayProperties": { "name": "Display Cannon" }, "itemType": 3, "index": 1, "equippable": false, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 1 },
              "20": { "displayProperties": { "name": "Adaptive Frame", "icon": "/adaptive.png" }, "itemTypeDisplayName": "Intrinsic", "plug": { "plugCategoryIdentifier": "intrinsics" }, "perks": [ { "perkHash": 42 } ] },
              "200": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "classType": 1, "index": 3, "equippable": true, "inventory": { "bucketTypeHash": 2, "tierType": 5 }, "equippingBlock": { "equipableItemSetHash": 500 } },
              "201": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "classType": 0, "index": 4, "equippable": true, "inventory": { "bucketTypeHash": 2, "tierType": 5 } },
              "300": { "displayProperties": { "name": "Spark" }, "itemTypeDisplayName": "Arc Fragment", "itemType": 19, "index": 1, "inventory": { "bucketTypeHash": 3, "tierType": 5 } }
            }
            """;

        internal static readonly ImmutableDictionary<ManifestTable, ImmutableDictionary<ItemHash, string>> Tables = new Dictionary<ManifestTable, string>
        {
            [ManifestTable.InventoryItem] = Items,
            [ManifestTable.InventoryBucket] = Buckets,
            [ManifestTable.SocketType] = "{}",
            [ManifestTable.SocketCategory] = Categories,
            [ManifestTable.PlugSet] = "{}",
            [ManifestTable.SandboxPerk] = Perks,
            [ManifestTable.EquipableItemSet] = Sets,
            [ManifestTable.BreakerType] = Breakers,
        }.ToImmutableDictionary(entry => entry.Key, entry => ManifestStore.ReadTable(entry.Value));
    }
}
