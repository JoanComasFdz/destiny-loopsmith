using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>
/// A small hand-made manifest and the hashes Loopsmith names → rules/manifest.yaml, whole. The expected text is what the
/// Python extractor this tool replaced wrote from the same manifest (its generator line aside): a weapon with a fixed
/// trait column and an intrinsic frame that gives it its breaker, the trait's enhanced copy, an armor set piece with its
/// set and the set's perks, a named sandbox perk, a stat mod, a damage type and a breaker without an icon, and hashes and
/// traits the manifest lacks.
/// </summary>
public sealed class ExcerptWritingTests
{
    private const string Items = """
        {
          "1000": { "hash": 1000, "displayProperties": { "name": "Crisis  Inverted", "description": "", "icon": "/w.png" }, "itemTypeDisplayName": "Hand Cannon",
                    "inventory": { "bucketTypeHash": 1, "tierType": 5 }, "defaultDamageType": 2, "equippingBlock": { "ammoType": 1 }, "iconWatermark": "/wm.png",
                    "sockets": { "socketEntries": [ { "socketTypeHash": 100, "singleInitialItemHash": 2000 }, { "socketTypeHash": 102, "singleInitialItemHash": 3000 } ],
                                 "socketCategories": [ { "socketCategoryHash": 10, "socketIndexes": [ 0 ] }, { "socketCategoryHash": 12, "socketIndexes": [ 1 ] } ] } },
          "2000": { "hash": 2000, "displayProperties": { "name": "Rampage", "description": "Kills \"stack\" \ue001 damage." }, "itemTypeDisplayName": "Trait",
                    "inventory": { "tierType": 2 } },
          "2001": { "hash": 2001, "displayProperties": { "name": "Rampage", "description": "Stronger." }, "itemTypeDisplayName": "Enhanced Trait" },
          "3000": { "hash": 3000, "displayProperties": { "name": "Adaptive Frame", "description": "Dependable." }, "itemTypeDisplayName": "Intrinsic",
                    "plug": { "plugCategoryIdentifier": "intrinsics" }, "perks": [ { "perkHash": 70 } ] },
          "4000": { "hash": 4000, "displayProperties": { "name": "Luminopotent Mask", "description": "", "icon": "/m.png" }, "itemTypeDisplayName": "Helmet",
                    "inventory": { "bucketTypeHash": 3, "tierType": 5 }, "classType": 1, "equippingBlock": { "equipableItemSetHash": 500 }, "iconWatermark": "/wm2.png" },
          "5000": { "hash": 5000, "displayProperties": { "name": "Grenade Mod" }, "plug": { "plugCategoryHash": 2487827355 },
                    "investmentStats": [ { "statTypeHash": 1735777505, "value": 10 } ] }
        }
        """;

    private const string Buckets = """
        { "1": { "displayProperties": { "name": "Kinetic Weapons" } }, "3": { "displayProperties": { "name": "Helmet" } } }
        """;

    private const string DamageTypes = """
        { "3373582085": { "enumValue": 2, "displayProperties": { "icon": "/arc.png" } }, "1": { "enumValue": 1, "displayProperties": { "icon": "/kinetic.png" } }, "9": { "enumValue": 5, "displayProperties": { "icon": "/raid.png" } } }
        """;

    private const string SocketTypes = """
        { "100": { "plugWhitelist": [ { "categoryIdentifier": "frames" } ] }, "102": { "plugWhitelist": [ { "categoryIdentifier": "intrinsics" } ] } }
        """;

    private const string SocketCategories = """
        { "10": { "displayProperties": { "name": "WEAPON PERKS" } }, "11": { "displayProperties": { "name": "WEAPON MODS" } }, "12": { "displayProperties": { "name": "INTRINSIC TRAITS" } }, "13": { "displayProperties": { "name": "ARMOR PERKS" } } }
        """;

    private const string Perks = """
        { "70": { "displayProperties": { "name": "[Shield-Piercing] Barrier" } }, "600": { "isDisplayable": true, "displayProperties": { "name": "Two Piece", "description": "Two — pieces.", "icon": "/p2.png" } }, "601": { "isDisplayable": true, "displayProperties": { "name": "Four Piece" } } }
        """;

    private const string ItemSets = """
        { "500": { "displayProperties": { "name": "Luminopotent" }, "setPerks": [ { "requiredSetCount": 4, "sandboxPerkHash": 601 }, { "requiredSetCount": 2, "sandboxPerkHash": 600 } ] } }
        """;

    private const string BreakerTypes = """
        { "485622768": { "enumValue": 1, "displayProperties": { "name": "Shield Piercing", "icon": "/sp.png" } }, "2611060930": { "enumValue": 2, "displayProperties": { "name": "Disruption" } } }
        """;

    private const string ItemConstants = """
        { "1": { "gearTierOverlayImagePaths": [ "/t1.png", "/t2.png" ], "masterworkOverlayPath": "/mw.png" } }
        """;

    private const string Traits = """
        { "2935077680": { "displayProperties": { "name": "Bolt Charge", "icon": "/bolt.png" } }, "7": { "displayProperties": { "name": "No icon" } } }
        """;

    private const string Stats = """
        { "2996146975": { "displayProperties": { "name": "Weapons", "icon": "/weapons.png" } } }
        """;

    private const string Expected = """
        # Loopsmith's excerpt of the Bungie manifest (docs/rule-format.md, "Manifest"): names, descriptions, icons,
        # slots, perks and armor sets for every hash the rules and tools/manifest/test-items.yaml name, and every plug that
        # changes an armor stat (statPlugs: for adding up a player's own armor, as DIM does). Generated by
        # tools/manifest/Loopsmith.ManifestTools - don't edit by hand, rerun it. Bungie's data, used under its API terms.
        version: "v1"
        damageTypes:
          - { type: kinetic, icon: /kinetic.png }
          - { type: arc, icon: /arc.png }
        breakerTypes:
          - { hash: 485622768, name: "Shield Piercing", icon: /sp.png }
        armorStats:
          - { stat: weapons, name: "Weapons", icon: /weapons.png }
        itemConstants:
          tierOverlays: [/t1.png, /t2.png]
          masterworkOverlay: /mw.png
        keywordTraits:
          - { hash: 2935077680, name: "Bolt Charge", icon: /bolt.png }
        armorSets:
          - { hash: 500, name: "Luminopotent", perks: [{ pieces: 2, perk: 600 }, { pieces: 4, perk: 601 }] }
        items:
          - { hash: 600, name: "Two Piece", kind: perk, description: "Two \u2014 pieces.", icon: /p2.png }
          - { hash: 601, name: "Four Piece", kind: perk }
          - { hash: 1000, name: "Crisis Inverted", kind: weapon, type: "Hand Cannon", tier: legendary, slot: kinetic, damageType: arc, ammo: primary, breaker: 485622768, traits: [[2000]], intrinsic: 3000, watermark: /wm.png, icon: /w.png }
          - { hash: 2000, name: "Rampage", kind: weaponPerk, type: "Trait", tier: basic, description: "Kills \"stack\" damage." }
          - { hash: 2001, name: "Rampage", kind: weaponPerk, type: "Enhanced Trait", description: "Stronger." }
          - { hash: 3000, name: "Adaptive Frame", kind: intrinsic, type: "Intrinsic", description: "Dependable." }
          - { hash: 4000, name: "Luminopotent Mask", kind: armor, type: "Helmet", tier: legendary, slot: helmet, set: 500, class: hunter, watermark: /wm2.png, icon: /m.png }
        statPlugs:
          - { hash: 5000, kind: statMod, stats: [{ stat: grenade, value: 10 }] }

        """;

    private static readonly NamedHashes Named = new([1000, 2000, 9, 600], [2935077680, 7], [4000]);

    [Fact]
    public void The_excerpt_holds_what_the_rules_and_test_items_name_and_the_stat_plugs()
    {
        var excerpt = ExcerptWriting.WriteExcerpt(ToManifest(), Named, "v1").ToValue();

        Assert.Equal(Expected, excerpt.Text);
        Assert.Equal("wrote 7 items, 1 armor sets and 1 stat plugs to rules/manifest.yaml", excerpt.Summary);
    }

    [Fact]
    public void What_the_manifest_lacks_is_reported_the_named_hashes_first_then_the_armor_stats_then_the_traits() =>
        Assert.Equal(
            [9, 392767087, 1943323491, 1735777505, 144602215, 4244567218, 7],
            ExcerptWriting.WriteExcerpt(ToManifest(), Named, "v1").ToValue().Missing);

    [Fact]
    public void The_same_manifest_writes_the_same_excerpt() =>
        Assert.Equal(
            ExcerptWriting.WriteExcerpt(ToManifest(), Named, "v1").ToValue().Text,
            ExcerptWriting.WriteExcerpt(ToManifest(), Named with { RuleHashes = [600, 9, 2000, 1000] }, "v1").ToValue().Text);

    [Fact]
    public void A_manifest_without_a_socket_category_the_excerpt_reads_is_an_error_naming_it() =>
        Assert.Equal(
            "DestinySocketCategoryDefinition.json has no socket category named WEAPON PERKS.",
            ExcerptWriting.WriteExcerpt(Manifests.ToComponents(socketCategories: "{}"), Named, "v1").ToFailure());

    private static ManifestComponents ToManifest() =>
        Manifests.ToComponents(
            items: Items,
            buckets: Buckets,
            damageTypes: DamageTypes,
            socketTypes: SocketTypes,
            socketCategories: SocketCategories,
            perks: Perks,
            itemSets: ItemSets,
            breakerTypes: BreakerTypes,
            itemConstants: ItemConstants,
            traits: Traits,
            stats: Stats);
}
