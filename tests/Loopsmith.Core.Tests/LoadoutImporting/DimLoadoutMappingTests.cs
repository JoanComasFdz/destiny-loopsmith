using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoadoutImporting;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.LoadoutImporting;

/// <summary>
/// docs/loop-format.md "Starting from a DIM link": what the catalog recognises by manifest hash becomes the build, the
/// rest is left out, nothing is guessed. Hashes below 10000 stand for items the catalog doesn't know.
/// </summary>
public sealed class DimLoadoutMappingTests
{
    private const string Link = "https://dim.gg/4j5nz4q";

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    /// <summary>
    /// The Skip Grenade build as DIM shares it: the Arc Hunter subclass (Arcstrider) with its plugs in socket order
    /// (Gambler's Dodge as its Solar copy, a jump, the super, melee and grenade, two aspects, four fragments), Shinobu's
    /// Vow, a weapon, and the mods (Elemental Charge once by each of its hashes).
    /// </summary>
    private const string SkipGrenadeLoadout = """
        {
          "id": "abc", "name": "Skip Grenade Hunter", "classType": 1, "clearSpace": false,
          "equipped": [
            { "id": "1", "hash": 2328211300, "socketOverrides": {
                "0": 2816982785, "1": 1001, "2": 1002, "3": 1003, "4": 1004,
                "5": 4194622037, "6": 4194622036,
                "7": 1727069366, "8": 1727069361, "9": 1727069364, "10": 1727069362 } },
            { "id": "2", "hash": 1053737370 },
            { "id": "3", "hash": "2005" }
          ],
          "unequipped": [ { "id": "4", "hash": 2006 } ],
          "parameters": {
            "mods": [3712696020, 2996369932, 4182064480, 4182064480, 4188291233, 3007],
            "artifactUnlocks": { "unlockedItemHashes": [4008], "seasonNumber": 27 }
          }
        }
        """;

    [Fact]
    public void What_the_catalog_knows_by_hash_becomes_the_build()
    {
        var build = MapLoadout(SkipGrenadeLoadout);

        Assert.Equal(("Skip Grenade Hunter", GuardianClass.Hunter, Subclass.Arc), (build.Name, build.Class, build.Subclass));
        Assert.Equal(Optional.Some(Link), build.SourceUrl);
        Assert.Equal(Optional.Some(ElementId.From("gamblers-dodge")), build.Abilities.ClassAbility);
        Assert.Equal(["tempest-strike", "flow-state"], build.Aspects.Select(id => id.Value));
        Assert.Equal(["spark-of-resistance", "spark-of-frequency", "spark-of-shock", "spark-of-discharge"], build.Fragments.Select(id => id.Value));
        Assert.Equal(Optional.Some(ElementId.From("shinobus-vow")), build.ExoticArmor);
        Assert.Equal(["elemental-charge", "elemental-charge", "grenade-kickstart", "grenade-kickstart", "bomber"], build.ArmorMods.Select(id => id.Value));
        Assert.Empty(build.Weapons);
    }

    [Fact]
    public void An_ability_the_catalog_has_no_hash_for_is_unknown_and_everything_unrecognised_is_left_out()
    {
        var build = MapLoadout(SkipGrenadeLoadout);

        Assert.Equal(
            [false, false, false],
            new[] { build.Abilities.Super, build.Abilities.Grenade, build.Abilities.Melee }.Select(ability => ability.IsSome()));
        Assert.Equal(
            [
                (LoadoutPart.SubclassPlug, 1001u), (LoadoutPart.SubclassPlug, 1002u), (LoadoutPart.SubclassPlug, 1003u),
                (LoadoutPart.SubclassPlug, 1004u), (LoadoutPart.Item, 2005u), (LoadoutPart.ArmorMod, 3007u),
                (LoadoutPart.ArtifactPerk, 4008u),
            ],
            build.LeftOut.Select(item => (item.Part, item.Hash.Value)));
    }

    [Fact]
    public void The_DIM_APIs_answer_wraps_the_loadout()
    {
        var build = MapLoadout($$"""{ "loadout": {{SkipGrenadeLoadout}} }""");

        Assert.Equal("Skip Grenade Hunter", build.Name);
    }

    [Fact]
    public void The_optimizers_exotic_counts_when_none_is_equipped_and_an_unnamed_loadout_gets_a_name()
    {
        var build = MapLoadout("""{ "name": " ", "equipped": [ { "hash": 2328211300 } ], "parameters": { "exoticArmorHash": 1053737370 } }""");

        Assert.Equal(("DIM loadout", Optional.Some(ElementId.From("shinobus-vow"))), (build.Name, build.ExoticArmor));
    }

    [Theory]
    [InlineData("""{ "name": "No subclass", "equipped": [ { "hash": 1053737370 } ] }""", "can't tell this DIM loadout's subclass")]
    [InlineData("""{ "name": "Empty", "equipped": [] }""", "no equipped items")]
    [InlineData("""{ "name": "Titan", "classType": 0, "equipped": [ { "hash": 2328211300 } ] }""", "another class")]
    [InlineData("""not json""", "isn't JSON")]
    [InlineData("""[1, 2]""", "isn't a loadout")]
    public void A_loadout_without_a_recognised_subclass_of_its_class_does_not_start_a_build(string json, string message)
    {
        var error = Assert.IsType<Result<Build, string>.Error>(DimLoadoutMapping.MapLoadout(Catalog, Link, json));

        Assert.Contains(message, error.Failure);
    }

    [Fact]
    public void A_DIM_loadout_starts_a_design_whose_build_file_keeps_what_was_left_out()
    {
        var link = new DimLink.Shared("4j5nz4q", Link);

        var session = LoopDesigning.StartDimDesign(Catalog, link, SkipGrenadeLoadout)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var exported = LoopDesigning.ExportLoop(session);
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("dim.loop.yaml", exported))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal("Skip Grenade Hunter loop", session.Design.Name);
        Assert.Contains("super: \"?\"", session.Design.Build.Text);
        Assert.Equal(7, reopened.Build.Build.LeftOut.Length);
        Assert.Contains(session.Build.Issues, issue => issue.Message.StartsWith("Left out of the build: 1 item, 4 subclass plugs, 1 armor mod, 1 artifact perk", StringComparison.Ordinal));
        Assert.Contains(session.Build.Issues, issue => issue.Message.StartsWith("Unknown (?): super, grenade, melee", StringComparison.Ordinal));
        Assert.True(LoopDesigning.IsDimLink(reopened.Build.Build.SourceUrl.UnwrapOr("")));
    }

    private static Build MapLoadout(string json) =>
        DimLoadoutMapping.MapLoadout(Catalog, Link, json).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
