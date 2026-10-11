using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.ManifestJoining;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.ManifestJoining;

/// <summary>
/// The manifest join: Bungie's full manifest → the excerpt some hashes need, the way tools/manifest/extract_manifest.py
/// writes rules/manifest.yaml. A fixture shaped like Bungie's JSON always runs; the real manifest, when one is
/// downloaded (<c>LOOPSMITH_MANIFEST_DIR</c>), must give every item of the bundled excerpt exactly as it is.
/// </summary>
public sealed class ManifestJoinTests
{
    [Fact]
    public void A_weapon_brings_its_columns_intrinsic_catalyst_and_the_catalysts_perk_and_armor_its_set()
    {
        var store = new ManifestStore(Fixture.Tables);

        var excerpt = store.Join([ItemHash.From(100u), ItemHash.From(200u)]);
        var weapon = Assert.IsType<ManifestKind.Weapon>(excerpt.Items[ItemHash.From(100u)].Kind);
        var armor = Assert.IsType<ManifestKind.Armor>(excerpt.Items[ItemHash.From(200u)].Kind);

        Assert.Equal((WeaponSlot.Power, Optional.Some(DamageType.Arc)), (weapon.Slot, weapon.DamageType));
        Assert.Equal([[10u, 11u], [12u]], weapon.Traits.Select(column => column.Options.Select(hash => hash.Value)));
        Assert.Equal((Optional.Some(ItemHash.From(20u)), Optional.Some(ItemHash.From(30u))), (weapon.Intrinsic, weapon.Catalyst));
        Assert.Equal([ItemHash.From(40u)], Assert.IsType<ManifestKind.ExoticCatalyst>(excerpt.Items[ItemHash.From(30u)].Kind).Perks);
        Assert.Equal((Optional.Some("Strikes refill the magazine."), Optional.None<string>()), (excerpt.Items[ItemHash.From(40u)].Description, excerpt.Items[ItemHash.From(40u)].Type));
        Assert.Equal("Fires faster.", excerpt.Items[ItemHash.From(10u)].Description.UnwrapOr(""));                 // icon glyph and double space gone
        Assert.Equal((ArmorSlot.Helmet, Optional.Some(ItemHash.From(500u))), (armor.Slot, armor.Set));
        Assert.Equal([(2, 600u), (4, 601u)], excerpt.ArmorSets[ItemHash.From(500u)].Perks.Select(perk => (perk.Pieces.Value, perk.Perk.Value)));
        Assert.Equal(Optional.Some("Two pieces worn."), excerpt.Items[ItemHash.From(600u)].Description);
        Assert.InRange(store.Rounds, 2, 8);
    }

    [Fact]
    public void An_artifact_brings_the_perks_of_its_sockets_plug_sets_without_the_empty_plug()
    {
        var store = new ManifestStore(Fixture.Tables);

        var excerpt = store.Join([ItemHash.From(400u)]);

        var artifact = Assert.IsType<ManifestKind.Artifact>(excerpt.Items[ItemHash.From(400u)].Kind);
        Assert.Equal([402u, 403u, 404u], artifact.Perks.Select(hash => hash.Value));
    }

    [Fact]
    public void A_hash_the_manifest_doesnt_have_is_asked_once_and_left_out()
    {
        var store = new ManifestStore(Fixture.Tables);

        var excerpt = store.Join([ItemHash.From(999u)]);

        Assert.Empty(excerpt.Items);
        Assert.Equal(1, store.Rounds);
    }

    [Fact]
    public void An_aspects_text_is_its_perks_effect_and_its_stat_change()
    {
        var store = new ManifestStore(Fixture.Tables);

        var excerpt = store.Join([ItemHash.From(300u)]);

        Assert.IsType<ManifestKind.Fragment>(excerpt.Items[ItemHash.From(300u)].Kind);
        Assert.Equal(Optional.Some("Your grenades jolt.\n-10 Grenade"), excerpt.Items[ItemHash.From(300u)].Description);
    }

    [Fact]
    public void The_components_urls_come_from_the_index_else_from_the_pinned_content_id()
    {
        var index = """{ "Response": { "jsonWorldComponentContentPaths": { "en": { """
            + string.Join(", ", Enum.GetValues<ManifestTable>().Select(table => $"\"{ManifestTransport.ToComponentName(table)}\": \"/common/destiny2_content/json/en/{table}-new.json\""))
            + " } } } }";
        var elsewhere = index.Replace("/common/destiny2_content/json/en/PlugSet-new.json", "//evil.example/PlugSet.json", StringComparison.Ordinal);

        var fromIndex = ManifestTransport.ChooseUrls(Optional.Some(index), "pinned");
        var pinned = ManifestTransport.ChooseUrls(Optional.None<string>(), "a8ba855a");
        var broken = ManifestTransport.ChooseUrls(Optional.Some("<html>500</html>"), "a8ba855a");
        var notBungies = ManifestTransport.ChooseUrls(Optional.Some(elsewhere), "a8ba855a");

        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/PlugSet-new.json", fromIndex["DestinyPlugSetDefinition"]);
        Assert.Equal("https://www.bungie.net/common/destiny2_content/json/en/DestinyPlugSetDefinition-a8ba855a.json", pinned["DestinyPlugSetDefinition"]);
        Assert.Equal(pinned, broken);
        Assert.Equal(pinned, notBungies);                                                        // a path off the component folder isn't taken
    }

    [Fact]
    public void A_query_becomes_the_hosts_request_and_search_and_its_json_answer_the_joins()
    {
        var query = new ManifestQuery(
            [new ManifestEntryKey(ManifestTable.InventoryItem, ItemHash.From(7u)), new ManifestEntryKey(ManifestTable.InventoryItem, ItemHash.From(8u)), new ManifestEntryKey(ManifestTable.SandboxPerk, ItemHash.From(9u))],
            [ManifestTable.SocketCategory],
            [PlugCategoryId.From("x.masterwork")]);

        var request = ManifestTransport.ToEntryRequest(query);
        var search = ManifestTransport.ToPlugSearch(query);
        var answer = ManifestTransport.ReadAnswer(
            query,
            """{ "DestinyInventoryItemDefinition": { "7": "{\"hash\":7}", "x": "{}", "0": "{}" }, "Unknown": { "1": "{}" } }""",
            """{ "\"plugCategoryIdentifier\":\"x.masterwork\"": [30, 31, 0, "no"], "bad": [1] }""");

        Assert.Equal([7u, 8u], request["DestinyInventoryItemDefinition"].Select(hash => hash.Value));
        Assert.Equal([9u], request["DestinySandboxPerkDefinition"].Select(hash => hash.Value));
        Assert.Empty(request["DestinySocketCategoryDefinition"]);                                // the whole table
        Assert.Equal(("DestinyInventoryItemDefinition", "\"plugCategoryIdentifier\":\"x.masterwork\""), (search.Component, Assert.Single(search.Needles)));
        var read = Assert.IsType<Result<ManifestAnswer, string>.Ok>(answer).Value;
        Assert.Equal("{\"hash\":7}", Assert.Single(read.Entries).Value);
        Assert.Equal([30u, 31u], Assert.Single(read.Plugs).Value.Select(hash => hash.Value)); // 0 and "no" aren't hashes
        Assert.IsType<Result<ManifestAnswer, string>.Error>(ManifestTransport.ReadAnswer(query, "not json", "{}"));
        Assert.IsType<Result<ManifestAnswer, string>.Error>(ManifestTransport.ReadAnswer(query, "{}", "[]"));
    }

    [Fact]
    public void A_dim_loadouts_unknown_hashes_are_what_the_excerpt_lacks()
    {
        var catalog = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        const string loadout = """{ "name": "x", "classType": 1, "equipped": [ { "hash": 3325463374 }, { "hash": 1363886209 } ], "parameters": { "mods": [ 99999 ] } }""";

        var unknown = LoopDesigning.ListUnknownLoadoutHashes(catalog, loadout).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal([1363886209u, 99999u], unknown.Select(hash => hash.Value));                // Thunderlord is in the excerpt
    }

    /// <summary>The C# join and the Python extractor read Bungie's manifest the same way: every bundled item, joined anew, is the same.</summary>
    [Fact]
    public void The_real_manifest_joins_to_the_bundled_excerpt()
    {
        var directory = Environment.GetEnvironmentVariable("LOOPSMITH_MANIFEST_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set LOOPSMITH_MANIFEST_DIR to a downloaded manifest (tools/manifest/get-manifest.sh) to run it.");
        var bundled = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure))
            .Manifest;
        var store = ManifestStore.ReadDirectory(directory!);

        var joined = store.Join(bundled.Items.Keys);

        Assert.Equal(
            bundled.Items.Values.OrderBy(item => item.Hash.Value).Select(DescribeItem),
            bundled.Items.Keys.Order().Select(hash => joined.Items.TryGetValue(hash, out var item) ? DescribeItem(item) : $"{hash.Value}: missing"));
        Assert.Equal(
            bundled.ArmorSets.Values.Select(DescribeSet).Order(),
            joined.ArmorSets.Values.Select(DescribeSet).Order());
    }

    private static string DescribeItem(ManifestItem item) =>
        $"{item.Hash.Value}: {item.Name} | {DescribeKind(item.Kind)} | {item.Type.UnwrapOr("-")} | {item.Tier.Match(tier => tier.Value.ToString(), _ => "-")} | {item.Icon.UnwrapOr("-")} | {item.Description.UnwrapOr("-")}";

    private static string DescribeKind(ManifestKind kind) =>
        kind switch
        {
            ManifestKind.Weapon weapon => $"weapon {weapon.Slot} {weapon.DamageType.Match(type => type.Value.ToString(), _ => "-")} "
                + $"[{string.Join(" / ", weapon.Traits.Select(column => string.Join(",", column.Options.Select(hash => hash.Value))))}] "
                + $"intrinsic {weapon.Intrinsic.Match(hash => hash.Value.Value.ToString(), _ => "-")} catalyst {weapon.Catalyst.Match(hash => hash.Value.Value.ToString(), _ => "-")}",
            ManifestKind.Armor armor => $"armor {armor.Slot} intrinsic {armor.Intrinsic.Match(hash => hash.Value.Value.ToString(), _ => "-")} set {armor.Set.Match(hash => hash.Value.Value.ToString(), _ => "-")}",
            ManifestKind.ArmorMod mod => $"armorMod {mod.Slot.Match(slot => slot.Value.ToString(), _ => "general")}",
            ManifestKind.ExoticCatalyst catalyst => $"catalyst [{string.Join(",", catalyst.Perks.Select(hash => hash.Value))}]",
            ManifestKind.Artifact artifact => $"artifact [{string.Join(",", artifact.Perks.Select(hash => hash.Value))}]",
            _ => kind.GetType().Name,
        };

    private static string DescribeSet(ArmorSet set) =>
        $"{set.Hash.Value} {set.Name}: {string.Join(", ", set.Perks.Select(perk => $"{perk.Pieces.Value}→{perk.Perk.Value}"))}";

    /// <summary>Bungie-shaped entries: a heavy Arc exotic (two fixed... one rolling column, an intrinsic, a catalyst), a helmet of a set, a fragment.</summary>
    private static class Fixture
    {
        private const string Buckets = """{"1":{"displayProperties":{"name":"Power Weapons"}},"2":{"displayProperties":{"name":"Helmet"}},"3":{"displayProperties":{"name":"Consumables"}}}""";
        private const string Categories = """{"7":{"displayProperties":{"name":"WEAPON PERKS"}},"8":{"displayProperties":{"name":"INTRINSIC TRAITS"}},"9":{"displayProperties":{"name":"ARMOR PERKS"}}}""";
        private const string SocketTypes = """{"70":{"plugWhitelist":[{"categoryIdentifier":"frames"}]},"71":{"plugWhitelist":[{"categoryIdentifier":"intrinsics"}]},"72":{"plugWhitelist":[{"categoryIdentifier":"x.machinegun.masterwork"},{"categoryIdentifier":"v400.empty.exotic.masterwork"}]}}""";
        private const string PlugSets = """{"80":{"reusablePlugItems":[{"plugItemHash":10,"currentlyCanRoll":true},{"plugItemHash":11},{"plugItemHash":13,"currentlyCanRoll":false},{"plugItemHash":10}]},"81":{"reusablePlugItems":[{"plugItemHash":401},{"plugItemHash":402},{"plugItemHash":403}]},"82":{"reusablePlugItems":[{"plugItemHash":401},{"plugItemHash":403},{"plugItemHash":404}]}}""";
        private const string Sets = """{"500":{"displayProperties":{"name":"Testing"},"setPerks":[{"requiredSetCount":4,"sandboxPerkHash":601},{"requiredSetCount":2,"sandboxPerkHash":600}]}}""";
        private const string Perks = """{"40":{"isDisplayable":true,"displayProperties":{"name":"Return","description":"Strikes refill the magazine."}},"41":{"isDisplayable":false,"displayProperties":{"name":""}},"50":{"isDisplayable":true,"displayProperties":{"name":"Spark","description":"Your grenades jolt."}},"51":{"isDisplayable":true,"displayProperties":{"name":"-10 Grenade","description":"-10 Grenade"}},"600":{"isDisplayable":true,"displayProperties":{"name":"Two","description":"Two pieces worn."}},"601":{"isDisplayable":true,"displayProperties":{"name":"Four","description":"Four pieces worn."}}}""";

        private const string Items = """
            {
              "100": { "displayProperties": { "name": "Test Thunder", "icon": "/w.jpg" }, "itemTypeDisplayName": "Machine Gun", "inventory": { "bucketTypeHash": 1, "tierType": 6 }, "defaultDamageType": 2,
                       "sockets": { "socketEntries": [ { "socketTypeHash": 71, "singleInitialItemHash": 20 }, { "socketTypeHash": 70, "randomizedPlugSetHash": 80 }, { "socketTypeHash": 70, "singleInitialItemHash": 12 }, { "socketTypeHash": 72, "singleInitialItemHash": 31 } ],
                                    "socketCategories": [ { "socketCategoryHash": 8, "socketIndexes": [0] }, { "socketCategoryHash": 7, "socketIndexes": [1, 2, 3] } ] } },
              "10": { "displayProperties": { "name": "Lightning", "description": "Fires  faster." }, "itemTypeDisplayName": "Trait", "plug": { "plugCategoryIdentifier": "frames" } },
              "11": { "displayProperties": { "name": "Frenzy" }, "itemTypeDisplayName": "Trait", "plug": { "plugCategoryIdentifier": "frames" } },
              "12": { "displayProperties": { "name": "Fixed" }, "itemTypeDisplayName": "Trait", "plug": { "plugCategoryIdentifier": "frames" } },
              "20": { "displayProperties": { "name": "Havoc", "description": "Kills strike." }, "itemTypeDisplayName": "Intrinsic", "plug": { "plugCategoryIdentifier": "intrinsics" } },
              "30": { "displayProperties": { "name": "Test Catalyst" }, "plug": { "plugCategoryIdentifier": "x.machinegun.masterwork" }, "perks": [ { "perkHash": 41 }, { "perkHash": 40 } ] },
              "31": { "displayProperties": { "name": "Empty Catalyst Socket" }, "plug": { "plugCategoryIdentifier": "v400.empty.exotic.masterwork" } },
              "200": { "displayProperties": { "name": "Test Mask" }, "itemTypeDisplayName": "Helmet", "inventory": { "bucketTypeHash": 2, "tierType": 5 }, "equippingBlock": { "equipableItemSetHash": 500 } },
              "300": { "displayProperties": { "name": "Spark", "description": "" }, "itemTypeDisplayName": "Arc Fragment", "inventory": { "bucketTypeHash": 3 }, "perks": [ { "perkHash": 50 }, { "perkHash": 51 } ] },
              "400": { "displayProperties": { "name": "Test Tablet" }, "itemTypeDisplayName": "Artifact", "inventory": { "bucketTypeHash": 3, "tierType": 5 },
                       "sockets": { "socketEntries": [ { "socketTypeHash": 70, "singleInitialItemHash": 401, "reusablePlugSetHash": 81 }, { "socketTypeHash": 70, "singleInitialItemHash": 401, "reusablePlugSetHash": 82 } ] } }
            }
            """;

        internal static readonly ImmutableDictionary<ManifestTable, ImmutableDictionary<ItemHash, string>> Tables = new Dictionary<ManifestTable, string>
        {
            [ManifestTable.InventoryItem] = Items.Replace("Fires  faster.", "Fires  faster.", StringComparison.Ordinal),
            [ManifestTable.InventoryBucket] = Buckets,
            [ManifestTable.SocketType] = SocketTypes,
            [ManifestTable.SocketCategory] = Categories,
            [ManifestTable.PlugSet] = PlugSets,
            [ManifestTable.SandboxPerk] = Perks,
            [ManifestTable.EquipableItemSet] = Sets,
        }.ToImmutableDictionary(entry => entry.Key, entry => ManifestStore.ReadTable(entry.Value));
    }
}
