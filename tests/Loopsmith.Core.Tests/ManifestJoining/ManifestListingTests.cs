using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.ManifestJoining;

/// <summary>
/// The listing of every weapon and armor piece a build can wear (the pickers' "Every item"), from Bungie's item table,
/// each weapon with its intrinsic and its breaker (its own, else its frame's). A fixture shaped like Bungie's JSON always runs; a downloaded
/// manifest (<c>LOOPSMITH_MANIFEST_DIR</c>) must list every weapon and armor piece of the bundled excerpt as the join
/// reads it, breakers and intrinsics included.
/// </summary>
public sealed class ManifestListingTests
{
    [Fact]
    public void Legendary_and_exotic_weapons_and_armor_are_listed_by_name_every_version_side_by_side()
    {
        var store = new ManifestStore(Fixture.Tables);

        var listing = store.List();

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
        Assert.Equal(4, store.Rounds);                                                           // the small components, the items, the frames, their perks
        Assert.Equal([1, 2, 3, 4], store.ListingRounds);                                         // as the startup screen counts them…
        Assert.Equal(4, LoopDesigning.ListingRounds);                                            // …"step 2 of 4"
    }

    [Fact]
    public void A_weapons_breaker_is_its_own_else_its_frames()
    {
        var listing = new ManifestStore(Fixture.Tables).List();
        var breakers = listing.Items
            .Where(item => item.Kind is ManifestKind.Weapon)
            .ToImmutableDictionary(item => item.Hash.Value, item => ((ManifestKind.Weapon)item.Kind).Breaker.Match(found => found.Value.Value, _ => 0u));

        Assert.Equal(485622768u, breakers[100u]);                                               // Adaptive Frame: "[Shield-Piercing] Barrier"
        Assert.Equal(0u, breakers[101u]);                                                       // its frame names no breaker
        Assert.Equal(2611060930u, breakers[102u]);                                              // its own (Disruption), whatever its frame says
    }

    [Fact]
    public void The_listing_saved_in_the_browser_reads_back_as_it_was_listed()
    {
        var listing = new ManifestStore(Fixture.Tables).List();

        var saved = LoopDesigning.ToSavedListing(listing);
        var read = Assert.IsType<Optional<ItemListing>.Some>(LoopDesigning.ReadSavedListing(Optional.Some(saved))).Value;

        Assert.Equal(listing.Items.Select(item => item.Hash), read.Items.Select(item => item.Hash));
        Assert.Equal(listing.Sets.Keys.Order(), read.Sets.Keys.Order());
        Assert.Equal(saved, LoopDesigning.ToSavedListing(read));                                 // every field kept
    }

    [Fact]
    public void A_weapons_intrinsic_is_its_first_named_intrinsic_plug()
    {
        var listing = new ManifestStore(Fixture.Tables).List();
        var intrinsics = listing.Items
            .Where(item => item.Kind is ManifestKind.Weapon)
            .ToImmutableDictionary(item => item.Hash.Value, item => ((ManifestKind.Weapon)item.Kind).Intrinsic.Match(found => found.Value.Value, _ => 0u));

        Assert.Equal(20u, intrinsics[100u]);                                                    // a legendary's frame
        Assert.Equal(22u, intrinsics[102u]);                                                    // an exotic's own perk (the rules name an exotic weapon by it)
    }

    [Fact]
    public void The_listing_reads_six_components_the_tab_loads_at_startup_and_says_why_an_answer_doesnt_read()
    {
        var urls = LoopDesigning.ChooseManifestUrls(Optional.None<string>(), "pinned");
        var start = Assert.IsType<ListingStep.Ask>(LoopDesigning.StartListing());

        Assert.Equal(
            ["DestinyBreakerTypeDefinition", "DestinyEquipableItemSetDefinition", "DestinyInventoryBucketDefinition", "DestinyInventoryItemDefinition",
                "DestinySandboxPerkDefinition", "DestinySocketCategoryDefinition"],
            ManifestListing.Components.Order(StringComparer.Ordinal));
        Assert.All(ManifestListing.Components, component => Assert.Contains(component, urls.Keys)); // the web app loads every one at startup
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyInventoryItemDefinition-pinned.json", urls["DestinyInventoryItemDefinition"]);
        Assert.Empty(start.Query.Parts.Paths);                                                   // the first round reads the small components only
        Assert.IsType<Result<ListingStep, string>.Error>(LoopDesigning.ContinueListing(start, "not json", "{}"));
        Assert.IsType<Result<ListingStep, string>.Error>(LoopDesigning.ContinueListing(start, """{ "DestinyInventoryBucketDefinition": { "3": { "displayProperties": { "name": "Consumables" } } } }""", "{}"));
        var items = Assert.IsType<ListingStep.Ask>(Assert.IsType<Result<ListingStep, string>.Ok>(
            LoopDesigning.ContinueListing(start, $$"""{ "DestinyInventoryBucketDefinition": {{Fixture.Buckets}} }""", "{}")).Value);
        Assert.Equal(["1", "2"], items.Query.Parts.Paths.Keys.Order(StringComparer.Ordinal));
        Assert.Contains(["sockets", "socketEntries", "*", "singleInitialItemHash"], items.Query.Parts.Paths["1"]);   // a weapon's sockets, for its frame
        Assert.DoesNotContain(items.Query.Parts.Paths["2"], path => path[0] == "sockets");                               // armor needs none
        Assert.IsType<Result<ListingStep, string>.Error>(LoopDesigning.ContinueListing(items, "{}", "[]"));
        Assert.IsType<Result<ItemListing, string>.Error>(LoopDesigning.ToItemListing(new Result<ListingStep, string>.Ok(items)));
    }

    /// <summary>The listing and the join read Bungie's manifest alike: every weapon and armor piece of the bundled excerpt is listed as the join named it.</summary>
    [Fact]
    public void The_real_manifest_lists_every_weapon_and_armor_piece_of_the_bundled_excerpt()
    {
        var directory = Environment.GetEnvironmentVariable("LOOPSMITH_MANIFEST_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set LOOPSMITH_MANIFEST_DIR to a downloaded manifest (tools/manifest/get-manifest.sh) to run it.");
        var bundled = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure))
            .Manifest;
        var wearable = bundled.Items.Values
            .Where(item => item.Kind is ManifestKind.Weapon or ManifestKind.Armor && (item.Tier == Optional.Some(ItemTier.Legendary) || item.Tier == Optional.Some(ItemTier.Exotic)))
            .ToImmutableArray();

        var listing = ManifestStore.ReadDirectory(directory!).List();
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
                ManifestKind.Weapon weapon => $"weapon {weapon.Slot} {weapon.DamageType.Match(type => type.Value.ToString(), _ => "-")} breaker {weapon.Breaker.Match(hash => hash.Value.Value.ToString(), _ => "-")} intrinsic {weapon.Intrinsic.Match(hash => hash.Value.Value.ToString(), _ => "-")}",
                ManifestKind.Armor armor => $"armor {armor.Slot} set {armor.Set.Match(hash => hash.Value.Value.ToString(), _ => "-")} class {armor.Class.Match(found => found.Value.ToString(), _ => "any")}",
                _ => item.Kind.GetType().Name,
            };

    /// <summary>Bungie-shaped items: two versions of a legendary (one on a breaker frame), an exotic with its own breaker, a set's helmet, and what isn't listed.</summary>
    private static class Fixture
    {
        internal const string Buckets = """{"1":{"displayProperties":{"name":"Kinetic Weapons"}},"2":{"displayProperties":{"name":"Helmet"}},"3":{"displayProperties":{"name":"Consumables"}}}""";
        private const string Sets = """{"500":{"displayProperties":{"name":"Testing"},"setPerks":[{"requiredSetCount":2,"sandboxPerkHash":600}]}}""";
        private const string Categories = """{"8":{"displayProperties":{"name":"INTRINSIC TRAITS"}},"7":{"displayProperties":{"name":"WEAPON PERKS"}}}""";
        private const string Breakers = """{"485622768":{"displayProperties":{"name":"Shield Piercing"}},"2611060930":{"displayProperties":{"name":"Disruption"}}}""";
        private const string Perks = """{"42":{"displayProperties":{"name":"[Shield-Piercing] Barrier"}},"43":{"displayProperties":{"name":"Faster"}},"44":{"displayProperties":{"name":"[Stagger] Unstoppable"}}}""";
        private const string Sockets = """ "sockets": { "socketEntries": [ { "singleInitialItemHash": 0 }, { "singleInitialItemHash": FRAME }, { "singleInitialItemHash": 10 } ], "socketCategories": [ { "socketCategoryHash": 7, "socketIndexes": [2] }, { "socketCategoryHash": 8, "socketIndexes": [1] } ] } """;

        private const string Items = """
            {
              "101": { "displayProperties": { "name": "Zeta Rifle", "icon": "/z2.jpg" }, "iconWatermark": "/season-2.png", "itemTypeDisplayName": "Scout Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, SOCKETS21 },
              "100": { "displayProperties": { "name": "Zeta Rifle", "icon": "/z1.jpg" }, "iconWatermark": "/season-1.png", "itemTypeDisplayName": "Scout Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 }, SOCKETS20 },
              "102": { "displayProperties": { "name": "Alpha Cannon", "icon": "/cannon.jpg" }, "iconWatermark": "/season-2.png", "itemTypeDisplayName": "Hand Cannon", "itemType": 3, "equippable": true, "defaultDamageType": 3, "breakerTypeHash": 2611060930, "inventory": { "bucketTypeHash": 1, "tierType": 6 }, SOCKETS22 },
              "103": { "displayProperties": { "name": "Blue Rifle" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 4 } },
              "104": { "displayProperties": { "name": "Preview Rifle" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": false, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "105": { "displayProperties": { "name": "Rifle Ornament" }, "itemTypeDisplayName": "Ornament", "itemType": 19, "equippable": true, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "106": { "displayProperties": { "name": "" }, "itemTypeDisplayName": "Auto Rifle", "itemType": 3, "equippable": true, "defaultDamageType": 1, "inventory": { "bucketTypeHash": 1, "tierType": 5 } },
              "200": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "equippable": true, "classType": 1, "inventory": { "bucketTypeHash": 2, "tierType": 5 }, "equippingBlock": { "equipableItemSetHash": 500 } },
              "201": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "itemType": 2, "equippable": true, "classType": 1, "inventory": { "bucketTypeHash": 14239492, "tierType": 5 } },
              "300": { "displayProperties": { "name": "Healing Potion" }, "itemType": 9, "equippable": false, "inventory": { "bucketTypeHash": 3, "tierType": 5 } },
              "10": { "displayProperties": { "name": "Lightning" }, "itemTypeDisplayName": "Trait", "plug": { "plugCategoryIdentifier": "frames" } },
              "20": { "displayProperties": { "name": "Adaptive Frame" }, "itemTypeDisplayName": "Intrinsic", "plug": { "plugCategoryIdentifier": "intrinsics" }, "perks": [ { "perkHash": 43 }, { "perkHash": 42 } ] },
              "21": { "displayProperties": { "name": "Plain Frame" }, "itemTypeDisplayName": "Intrinsic", "plug": { "plugCategoryIdentifier": "intrinsics" }, "perks": [ { "perkHash": 43 } ] },
              "22": { "displayProperties": { "name": "Cannon's Own" }, "itemTypeDisplayName": "Intrinsic", "plug": { "plugCategoryIdentifier": "intrinsics" }, "perks": [ { "perkHash": 44 } ] }
            }
            """;

        internal static readonly ImmutableDictionary<ManifestTable, ImmutableDictionary<ItemHash, string>> Tables = new Dictionary<ManifestTable, string>
        {
            [ManifestTable.InventoryItem] = Items
                .Replace("SOCKETS20", Sockets.Replace("FRAME", "20", StringComparison.Ordinal), StringComparison.Ordinal)
                .Replace("SOCKETS21", Sockets.Replace("FRAME", "21", StringComparison.Ordinal), StringComparison.Ordinal)
                .Replace("SOCKETS22", Sockets.Replace("FRAME", "22", StringComparison.Ordinal), StringComparison.Ordinal),
            [ManifestTable.InventoryBucket] = Buckets,
            [ManifestTable.EquipableItemSet] = Sets,
            [ManifestTable.SocketCategory] = Categories,
            [ManifestTable.SandboxPerk] = Perks,
            [ManifestTable.BreakerType] = Breakers,
        }.ToImmutableDictionary(entry => entry.Key, entry => ManifestStore.ReadTable(entry.Value));
    }
}
