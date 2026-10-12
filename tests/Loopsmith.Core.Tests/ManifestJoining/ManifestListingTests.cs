using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.ManifestJoining;

/// <summary>
/// The listing of every weapon and armor piece a build can wear (the pickers' "Every item"), from Bungie's lite item
/// table. A fixture shaped like Bungie's JSON always runs; a downloaded manifest with its lite item table
/// (<c>LOOPSMITH_MANIFEST_DIR</c>) must list every weapon and armor piece of the bundled excerpt as the join reads it.
/// </summary>
public sealed class ManifestListingTests
{
    [Fact]
    public void Legendary_and_exotic_weapons_and_armor_are_listed_by_name_every_version_side_by_side()
    {
        var listing = ListingStore.List(Fixture.Buckets, Fixture.Sets, ListingStore.ReadTable(Fixture.Items));

        Assert.Equal(
            ["Alpha Cannon 102", "Test Mask 200", "Zeta Rifle 100", "Zeta Rifle 101"],
            listing.Items.Select(item => $"{item.Name} {item.Hash.Value}"));
        var exotic = listing.Items.Single(item => item.Hash.Value == 102u);
        var weapon = Assert.IsType<ManifestKind.Weapon>(exotic.Kind);
        Assert.Equal((WeaponSlot.Kinetic, Optional.Some(DamageType.Solar), Optional.Some(ItemTier.Exotic)), (weapon.Slot, weapon.DamageType, exotic.Tier));
        Assert.Equal((Optional.Some("Hand Cannon"), Optional.Some("/cannon.jpg"), Optional.Some("/season-2.png")), (exotic.Type, exotic.Icon, exotic.Watermark));
        Assert.Empty(weapon.Traits);                                                             // a pick reads its perks with the join
        var mask = Assert.IsType<ManifestKind.Armor>(listing.Items.Single(item => item.Hash.Value == 200u).Kind);
        Assert.Equal((ArmorSlot.Helmet, Optional.Some(ItemHash.From(500u)), Optional.Some(GuardianClass.Hunter)), (mask.Slot, mask.Set, mask.Class));
        Assert.Equal("Testing", listing.Sets[ItemHash.From(500u)].Name);
    }

    [Fact]
    public void The_listing_reads_three_components_and_says_why_an_answer_doesnt_read()
    {
        var urls = LoopDesigning.ChooseListingUrls(Optional.None<string>(), "pinned");

        Assert.Equal(
            ["DestinyEquipableItemSetDefinition", "DestinyInventoryBucketDefinition", "DestinyInventoryItemLiteDefinition"],
            urls.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemLiteDefinition-pinned.json", urls["DestinyInventoryItemLiteDefinition"]);
        Assert.IsType<Result<ListingTables, string>.Error>(LoopDesigning.ReadListingTables("not json"));
        Assert.IsType<Result<ListingTables, string>.Error>(LoopDesigning.ReadListingTables("""{ "DestinyInventoryBucketDefinition": { "3": { "displayProperties": { "name": "Consumables" } } } }"""));
        var tables = LoopDesigning.ReadListingTables($$"""{ "DestinyInventoryBucketDefinition": {{Fixture.Buckets}} }""").Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        Assert.Equal(["1", "2"], LoopDesigning.ToListingPartsQuery(tables).Values);
        Assert.IsType<Result<ItemListing, string>.Error>(LoopDesigning.ReadItemListing(tables, "[]"));
    }

    /// <summary>The listing and the join read Bungie's manifest alike: every weapon and armor piece of the bundled excerpt is listed as the join named it.</summary>
    [Fact]
    public void The_real_manifest_lists_every_weapon_and_armor_piece_of_the_bundled_excerpt()
    {
        var directory = Environment.GetEnvironmentVariable("LOOPSMITH_MANIFEST_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory) || !ListingStore.HasLiteItems(directory), "Set LOOPSMITH_MANIFEST_DIR to a downloaded manifest with its lite item table to run it.");
        var bundled = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure))
            .Manifest;
        var wearable = bundled.Items.Values
            .Where(item => item.Kind is ManifestKind.Weapon or ManifestKind.Armor && (item.Tier == Optional.Some(ItemTier.Legendary) || item.Tier == Optional.Some(ItemTier.Exotic)))
            .ToImmutableArray();

        var listing = ListingStore.ListDirectory(directory!);
        var listed = listing.Items.ToImmutableDictionary(item => item.Hash);

        Assert.NotEmpty(wearable);
        Assert.Equal(
            wearable.OrderBy(item => item.Hash.Value).Select(DescribeItem),
            wearable.OrderBy(item => item.Hash.Value).Select(item => listed.TryGetValue(item.Hash, out var found) ? DescribeItem(found) : $"{item.Hash.Value}: missing"));
    }

    private static string DescribeItem(ManifestItem item) =>
        $"{item.Hash.Value}: {item.Name} | {item.Type.UnwrapOr("-")} | {item.Tier.Match(tier => tier.Value.ToString(), _ => "-")} | {item.Icon.UnwrapOr("-")} | "
            + item.Kind switch
            {
                ManifestKind.Weapon weapon => $"weapon {weapon.Slot} {weapon.DamageType.Match(type => type.Value.ToString(), _ => "-")}",
                ManifestKind.Armor armor => $"armor {armor.Slot} set {armor.Set.Match(hash => hash.Value.Value.ToString(), _ => "-")} class {armor.Class.Match(found => found.Value.ToString(), _ => "any")}",
                _ => item.Kind.GetType().Name,
            };

    /// <summary>Bungie-shaped lite items: two versions of a legendary, an exotic, a set's helmet, and what isn't listed.</summary>
    private static class Fixture
    {
        internal const string Buckets = """{"1":{"displayProperties":{"name":"Kinetic Weapons"}},"2":{"displayProperties":{"name":"Helmet"}},"3":{"displayProperties":{"name":"Consumables"}}}""";
        internal const string Sets = """{"500":{"displayProperties":{"name":"Testing"},"setPerks":[{"requiredSetCount":2,"sandboxPerkHash":600}]}}""";

        internal const string Items = """
            {
              "101": { "displayProperties": { "name": "Zeta Rifle", "icon": "/z2.jpg" }, "iconWatermark": "/season-2.png", "itemTypeDisplayName": "Scout Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "100": { "displayProperties": { "name": "Zeta Rifle", "icon": "/z1.jpg" }, "iconWatermark": "/season-1.png", "itemTypeDisplayName": "Scout Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "102": { "displayProperties": { "name": "Alpha Cannon", "icon": "/cannon.jpg" }, "iconWatermark": "/season-2.png", "itemTypeDisplayName": "Hand Cannon", "itemType": 3, "equippable": true, "defaultDamageType": 3, "inventory": { "bucketTypeHash": 1, "tierType": 6 } },
              "103": { "displayProperties": { "name": "Blue Rifle" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 4 } },
              "104": { "displayProperties": { "name": "Preview Rifle" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": false, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "105": { "displayProperties": { "name": "Rifle Ornament" }, "itemTypeDisplayName": "Ornament", "itemType": 19, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "106": { "displayProperties": { "name": "" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "200": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "equippable": true, "classType": 1, "inventory": { "bucketTypeHash": 2, "tierType": 5 }, "equippingBlock": { "equipableItemSetHash": 500 } },
              "201": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "equippable": true, "classType": 1, "inventory": { "bucketTypeHash": 14239492, "tierType": 5 } },
              "300": { "displayProperties": { "name": "Healing Potion" }, "itemType": 9, "equippable": false, "inventory": { "bucketTypeHash": 3, "tierType": 5 } }
            }
            """;
    }
}
