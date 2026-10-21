using System.Collections.Frozen;
using System.Collections.Immutable;
using Loopsmith.Functional;
using Loopsmith.ManifestTools.Core;
using Loopsmith.ManifestTools.Tests.Support;

namespace Loopsmith.ManifestTools.Tests;

/// <summary>An item's definition → its kind, slot, sockets' perks and mods, intrinsic, catalyst, breaker, tiers, plugs and description.</summary>
public sealed class ItemReadingTests
{
    private static readonly ManifestTable Buckets = Manifests.ToTable(Manifests.Buckets);

    /// <summary>Socket type 100 takes trait perks ("frames"), 101 weapon mods, 102 a masterwork, 103 an empty catalyst.</summary>
    private static readonly ManifestTable SocketTypes = Manifests.ToTable("""
        {
          "100": { "plugWhitelist": [ { "categoryIdentifier": "frames" } ] },
          "101": { "plugWhitelist": [ { "categoryIdentifier": "v400.weapon.mod_guns" }, { "categoryIdentifier": "v400.weapon.mod_empty" } ] },
          "102": { "plugWhitelist": [ { "categoryIdentifier": "v400.plugs.weapons.masterworks" } ] },
          "103": { "plugWhitelist": [ { "categoryIdentifier": "v400.empty.exotic.masterwork" }, { "categoryIdentifier": "catalysts.thunderlord" } ] },
          "104": { "plugWhitelist": [ { "categoryIdentifier": "artifact_perks" } ] },
          "105": { "plugWhitelist": [ { "categoryIdentifier": "artifact_perks" }, { "categoryIdentifier": "artifact_reset" } ] }
        }
        """);

    [Theory]
    [InlineData("""{ "inventory": { "bucketTypeHash": 4 } }""", "subclass", "")]
    [InlineData("""{ "inventory": { "bucketTypeHash": 1 }, "itemTypeDisplayName": "Hand Cannon" }""", "weapon", "kinetic")]
    [InlineData("""{ "inventory": { "bucketTypeHash": 5 } }""", "armor", "classItem")]
    [InlineData("""{ "itemTypeDisplayName": "Helmet Armor Mod", "plug": { "plugCategoryIdentifier": "enhancements.raid_garden" } }""", "activityMod", "")]
    [InlineData("""{ "plug": { "plugCategoryIdentifier": "enhancements.rivens_curse" } }""", "activityMod", "")]
    [InlineData("""{ "itemTypeDisplayName": "Arms Armor Mod" }""", "armorMod", "arms")]
    [InlineData("""{ "itemTypeDisplayName": "General Armor Mod" }""", "armorMod", "general")]
    [InlineData("""{ "itemTypeDisplayName": "Deprecated Armor Mod" }""", "armorMod", "")]
    [InlineData("""{ "itemTypeDisplayName": "Enhanced Trait" }""", "weaponPerk", "")]
    [InlineData("""{ "itemTypeDisplayName": "Arc Grenade" }""", "grenade", "")]
    [InlineData("""{ "itemTypeDisplayName": "Strand Fragment" }""", "fragment", "")]
    [InlineData("""{ "itemTypeDisplayName": "Emblem" }""", "other", "")]
    [InlineData("""{ }""", "other", "")]
    public void An_item_is_classified_by_its_bucket_then_its_plug_category_then_its_type(string item, string kind, string slot) =>
        Assert.Equal(
            new ItemKind(kind, slot.Length > 0 ? Optional.Some(slot) : Optional.None<string>()),
            ItemReading.ClassifyItem(Manifests.ToDefinition(item), Buckets));

    [Fact]
    public void A_weapons_trait_columns_are_its_frames_sockets_each_its_random_plug_set_that_can_roll_once_or_its_fixed_perk()
    {
        var weapon = Manifests.ToDefinition("""
            {
              "sockets": {
                "socketEntries": [
                  { "socketTypeHash": 100, "randomizedPlugSetHash": 7, "reusablePlugSetHash": 8 },
                  { "socketTypeHash": 102, "singleInitialItemHash": 99 },
                  { "socketTypeHash": 100, "singleInitialItemHash": 55 },
                  { "socketTypeHash": 100, "reusablePlugSetHash": 8 }
                ],
                "socketCategories": [ { "socketCategoryHash": 10, "socketIndexes": [ 0, 1, 2, 3, 9 ] } ]
              }
            }
            """);
        var plugSets = Manifests.ToTable("""
            {
              "7": { "reusablePlugItems": [ { "plugItemHash": 21 }, { "plugItemHash": 22, "currentlyCanRoll": false }, { "plugItemHash": 23, "currentlyCanRoll": true }, { "plugItemHash": 21 } ] },
              "8": { "reusablePlugItems": [ { "plugItemHash": 31 } ] }
            }
            """);

        var columns = ItemReading.ReadTraits(weapon, SocketTypes, 10, plugSets);

        Assert.Equal([[21, 23], [55], [31]], ((Optional<ImmutableArray<ImmutableArray<long>>>.Some)columns).Value.Select(column => column.ToArray()));
    }

    [Fact]
    public void A_weapon_without_sockets_or_with_a_column_it_cannot_read_has_unknown_columns()
    {
        var noSockets = Manifests.ToDefinition("""{ "sockets": {} }""");
        var unreadable = Manifests.ToDefinition("""
            { "sockets": { "socketEntries": [ { "socketTypeHash": 100, "randomizedPlugSetHash": 404 } ], "socketCategories": [ { "socketCategoryHash": 10, "socketIndexes": [ 0 ] } ] } }
            """);

        Assert.False(ItemReading.ReadTraits(noSockets, SocketTypes, 10, Manifests.ToTable("{}")).IsSome());
        Assert.False(ItemReading.ReadTraits(unreadable, SocketTypes, 10, Manifests.ToTable("{}")).IsSome());
    }

    [Fact]
    public void A_weapons_mods_are_its_mod_sockets_plug_set_once_each_without_the_empty_ones()
    {
        var weapon = Manifests.ToDefinition("""
            {
              "sockets": {
                "socketEntries": [ { "socketTypeHash": 101, "reusablePlugSetHash": 7 }, { "socketTypeHash": 102, "reusablePlugSetHash": 8 } ],
                "socketCategories": [ { "socketCategoryHash": 11, "socketIndexes": [ 0, 1 ] } ]
              }
            }
            """);
        var plugSets = Manifests.ToTable("""
            {
              "7": { "reusablePlugItems": [ { "plugItemHash": 40 }, { "plugItemHash": 41 }, { "plugItemHash": 40 }, { "plugItemHash": 42 }, { "plugItemHash": 404 } ] },
              "8": { "reusablePlugItems": [ { "plugItemHash": 50 } ] }
            }
            """);
        var items = Manifests.ToTable("""
            { "40": {}, "41": { "plug": { "plugCategoryIdentifier": "v400.weapon.mod_empty" } }, "42": { "plug": { "plugCategoryIdentifier": "v400.weapon.mod_guns" } }, "50": {} }
            """);

        Assert.Equal([40, 42], ItemReading.ReadMods(weapon, SocketTypes, 11, plugSets, items));
    }

    [Fact]
    public void An_intrinsic_is_the_first_named_intrinsics_plug_of_its_category()
    {
        var weapon = Manifests.ToDefinition("""
            {
              "sockets": {
                "socketEntries": [ { "singleInitialItemHash": 60 }, { "singleInitialItemHash": 61 }, { "singleInitialItemHash": 62 } ],
                "socketCategories": [ { "socketCategoryHash": 13, "socketIndexes": [ 2 ] }, { "socketCategoryHash": 12, "socketIndexes": [ 0, 1, 2 ] } ]
              }
            }
            """);
        var items = Manifests.ToTable("""
            {
              "60": { "hash": 60, "plug": { "plugCategoryIdentifier": "intrinsics" }, "displayProperties": { "name": "" } },
              "61": { "hash": 61, "plug": { "plugCategoryIdentifier": "intrinsics" }, "displayProperties": { "name": "Adaptive Frame" } },
              "62": { "hash": 62, "plug": { "plugCategoryIdentifier": "intrinsics" }, "displayProperties": { "name": "Exotic Perk" } }
            }
            """);

        Assert.Equal(Optional.Some(61L), ItemReading.ReadIntrinsic(weapon, 12, items));
        Assert.Equal(Optional.Some(62L), ItemReading.ReadIntrinsic(weapon, 13, items));
        Assert.False(ItemReading.ReadIntrinsic(weapon, 14, items).IsSome());
    }

    [Fact]
    public void A_frame_gives_the_breaker_its_perk_names_in_brackets()
    {
        var frame = Manifests.ToDefinition("""{ "perks": [ { "perkHash": 70 }, { "perkHash": 71 } ] }""");
        var perks = Manifests.ToTable("""
            { "70": { "displayProperties": { "name": "[Unknown] Thing" } }, "71": { "displayProperties": { "name": "[Shield-Piercing] Barrier" } } }
            """);
        var breakers = Manifests.ToTable("""
            { "485622768": { "displayProperties": { "name": "Shield Piercing" } }, "2611060930": { "displayProperties": { "name": "Disruption" } } }
            """);

        Assert.Equal(Optional.Some(485622768L), ItemReading.ReadFrameBreaker(frame, perks, breakers));
        Assert.False(ItemReading.ReadFrameBreaker(Manifests.ToDefinition("{}"), perks, breakers).IsSome());
    }

    [Fact]
    public void A_catalyst_is_a_plug_of_its_sockets_own_category_that_grants_a_perk_the_game_shows()
    {
        var weapon = Manifests.ToDefinition("""{ "sockets": { "socketEntries": [ { "socketTypeHash": 102 }, { "socketTypeHash": 103 } ] } }""");
        var items = Manifests.ToTable("""
            {
              "80": { "perks": [ { "perkHash": 90 } ] },
              "81": { "perks": [ { "perkHash": 91 } ] }
            }
            """);
        var perks = Manifests.ToTable("""
            { "90": { "isDisplayable": false, "displayProperties": { "name": "Hidden" } }, "91": { "isDisplayable": true, "displayProperties": { "name": "Thunderlord Catalyst" } } }
            """);
        var byCategory = new Dictionary<string, ImmutableArray<long>> { ["catalysts.thunderlord"] = [80, 81] }.ToFrozenDictionary();

        Assert.Equal(Optional.Some(81L), ItemReading.ReadCatalyst(weapon, SocketTypes, byCategory, items, perks));
        Assert.Equal([91], ItemReading.ReadGrantedPerks(Manifests.ToDefinition("""{ "perks": [ { "perkHash": 90 }, { "perkHash": 91 } ] }"""), perks));
    }

    [Fact]
    public void An_artifacts_tiers_are_what_each_plug_set_adds_to_the_earlier_ones_and_each_slot_takes_up_to_its_plug_sets_tier()
    {
        var artifact = Manifests.ToDefinition("""
            {
              "sockets": {
                "socketEntries": [
                  { "socketTypeHash": 104, "reusablePlugSetHash": 1, "singleInitialItemHash": 9 },
                  { "socketTypeHash": 104, "reusablePlugSetHash": 2, "singleInitialItemHash": 9 },
                  { "socketTypeHash": 104, "reusablePlugSetHash": 1, "singleInitialItemHash": 9 },
                  { "socketTypeHash": 105, "reusablePlugSetHash": 3 }
                ]
              }
            }
            """);
        var plugSets = Manifests.ToTable("""
            {
              "1": { "reusablePlugItems": [ { "plugItemHash": 9 }, { "plugItemHash": 11 }, { "plugItemHash": 12 } ] },
              "2": { "reusablePlugItems": [ { "plugItemHash": 9 }, { "plugItemHash": 11 }, { "plugItemHash": 12 }, { "plugItemHash": 21 } ] },
              "3": { "reusablePlugItems": [ { "plugItemHash": 99 } ] }
            }
            """);

        var (tiers, slots) = ItemReading.ReadArtifactTiers(artifact, plugSets, SocketTypes);

        Assert.Equal([[11, 12], [21]], tiers.Select(tier => tier.ToArray()));
        Assert.Equal([1, 2, 1], slots);
    }

    [Fact]
    public void A_subclasses_plugs_are_every_plug_its_sockets_can_roll_once_each()
    {
        var subclass = Manifests.ToDefinition("""
            { "sockets": { "socketEntries": [ { "reusablePlugSetHash": 1 }, { "reusablePlugSetHash": 2 }, { "singleInitialItemHash": 5 } ] } }
            """);
        var plugSets = Manifests.ToTable("""
            {
              "1": { "reusablePlugItems": [ { "plugItemHash": 31 }, { "plugItemHash": 32, "currentlyCanRoll": false } ] },
              "2": { "reusablePlugItems": [ { "plugItemHash": 33 }, { "plugItemHash": 31 } ] }
            }
            """);

        Assert.Equal([31, 33], ItemReading.ReadSubclassPlugs(subclass, plugSets));
    }

    [Fact]
    public void An_item_without_a_description_of_its_own_is_described_by_the_perks_the_game_shows()
    {
        var fragment = Manifests.ToDefinition("""
            { "displayProperties": { "name": "Spark of Shock", "description": "" }, "perks": [ { "perkHash": 1 }, { "perkHash": 2 }, { "perkHash": 3 }, { "perkHash": 4 }, { "perkHash": 5 } ] }
            """);
        var perks = Manifests.ToTable("""
            {
              "1": { "isDisplayable": true, "displayProperties": { "name": "Spark of Shock", "description": "Grenades jolt." } },
              "2": { "isDisplayable": true, "displayProperties": { "name": "Discipline", "description": "-10" } },
              "3": { "isDisplayable": true, "displayProperties": { "name": "Named only", "description": "" } },
              "4": { "isDisplayable": false, "displayProperties": { "name": "Hidden", "description": "Not shown." } },
              "5": { "isDisplayable": true, "displayProperties": { "name": "", "description": "Unnamed text." } }
            }
            """);

        Assert.Equal("Grenades jolt.\nDiscipline: -10\nNamed only\nUnnamed text.", ItemReading.ReadDescription(fragment, perks));
        Assert.Equal("Its own.", ItemReading.ReadDescription(Manifests.ToDefinition("""{ "displayProperties": { "description": "Its own." }, "perks": [ { "perkHash": 1 } ] }"""), perks));
    }
}
