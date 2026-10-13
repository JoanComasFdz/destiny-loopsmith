using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoadoutImporting;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Phrasing;
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
        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("gamblers-dodge")), build.Abilities.ClassAbility);
        Assert.Equal(["tempest-strike", "flow-state"], build.Aspects.Select(id => id.Value));
        Assert.Equal(["spark-of-resistance", "spark-of-frequency", "spark-of-shock", "spark-of-discharge"], build.Fragments.Select(id => id.Value));
        Assert.Equal(Optional.Some(ElementId.From("shinobus-vow")), build.ExoticArmor);
        Assert.Equal(["elemental-charge", "elemental-charge", "grenade-kickstart", "grenade-kickstart", "bomber"], build.ArmorMods.Select(id => id.Value));
        Assert.Equal([3712696020u, 2996369932u], build.ArmorModPlugs.Select(hash => hash.Value));  // one copy isn't its element's first: each copy kept, in order
        Assert.Empty(build.Weapons);
    }

    [Fact]
    public void An_ability_the_catalog_has_no_hash_for_is_unknown_and_everything_unrecognised_is_left_out()
    {
        var build = MapLoadout(SkipGrenadeLoadout);

        Assert.Equal(
            [false, false, false],
            new[] { build.Abilities.Super, build.Abilities.Grenade, build.Abilities.Melee }.Select(ability => ability.ToElement().IsSome()));
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

    [Fact]
    public void A_weapons_socket_overrides_pick_its_perks_in_the_columns_that_roll_them()
    {
        // Festival Flight: Slice (923806249) rolls in its first trait column; 1001 is in no column.
        var withPerks = MapLoadout("""{ "name": "x", "classType": 1, "equipped": [ { "hash": 2328211300 }, { "hash": 4019651319, "socketOverrides": { "1": 1001, "3": 923806249 } } ] }""");
        var without = MapLoadout("""{ "name": "x", "classType": 1, "equipped": [ { "hash": 2328211300 }, { "hash": 4019651319, "socketOverrides": { "1": 1001 } } ] }""");

        Assert.Equal([Optional.Some(ItemHash.From(923806249u)), Optional.None<ItemHash>()], Assert.Single(withPerks.Weapons).Roll);
        Assert.Empty(Assert.Single(without.Weapons).Roll);                                     // nothing a column rolls: no roll
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
        var link = new DimLink.Shared(DimShareId.From("4j5nz4q"), Link);

        var session = LoopDesigning.StartDimDesign(Catalog, link, SkipGrenadeLoadout)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var exported = LoopDesigning.ExportLoop(session);
        var reopened = LoopDesigning.ImportLoop(Catalog, new SourceText("dim.loop.yaml", exported))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal("Skip Grenade Hunter loop", session.Design.Name);
        Assert.Contains("super: \"?\"", session.Design.Build.Text);
        Assert.Equal(7, reopened.Build.Build.LeftOut.Length);
        Assert.Equal("1 item, 4 subclass plugs, 1 armor mod, 1 artifact perk", LoopDesigning.ListMissing(session.Build).DescribeLeftOut());
        Assert.DoesNotContain(session.Build.Issues, issue => issue.Message.StartsWith("Left out of the build", StringComparison.Ordinal) || issue.Message.StartsWith("Unknown (?)", StringComparison.Ordinal));
        Assert.True(LoopDesigning.IsDimLink(reopened.Build.Build.SourceUrl.UnwrapOr("")));
    }

    /// <summary>The owner's own Skip Grenade build, saved from its dim.gg share page (builds/skip-grenade-hunter/dim-loadout.json).</summary>
    [Fact]
    public void The_owners_shared_build_opens_with_its_name_link_and_what_the_rules_know()
    {
        const string path = "builds/skip-grenade-hunter/dim-loadout.json";
        var saved = new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path)));

        var session = LoopDesigning.StartSavedDimDesign(Catalog, saved)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var build = session.Build.Build;

        Assert.Equal(("Arc - Skipp grenade", GuardianClass.Hunter, Subclass.Arc), (build.Name, build.Class, build.Subclass));
        Assert.Equal(Optional.Some("https://dim.gg/2jguuoq/Arc-Skipp-grenade"), build.SourceUrl);
        Assert.Equal(
            new AbilityLoadout(
                new AbilityPlug.Known(ElementId.From("gathering-storm")),
                new AbilityPlug.Known(ElementId.From("skip-grenade")),
                new AbilityPlug.Known(ElementId.From("combination-blow")),
                new AbilityPlug.Known(ElementId.From("gamblers-dodge")),
                new AbilityPlug.Known(ElementId.From("triple-jump"))),
            build.Abilities);
        Assert.Equal(["flow-state", "tempest-strike"], build.Aspects.Select(id => id.Value));
        Assert.Equal(Optional.Some(ElementId.From("shinobus-vow")), build.ExoticArmor);
        Assert.Equal(["spark-of-resistance", "spark-of-frequency", "spark-of-shock", "spark-of-discharge"], build.Fragments.Select(id => id.Value));
        Assert.Equal(
            [
                "harmonic-siphon", "strand-siphon", "ashes-to-assets", "grenade-kickstart", "grenade-kickstart", "impact-induction",
                "charged-up", "harmonic-resistance", "concussive-dampener", "elemental-charge", "elemental-charge", "recuperation", "bomber",
                "reaper", "powerful-attraction",
            ],
            build.ArmorMods.Select(id => id.Value));
        Assert.Equal(
            [
                (WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, "grenade-launcher", 4019651319u),
                (WeaponSlot.Power, "Thunderlord", DamageType.Arc, "machine-gun", 3325463374u),
                (WeaponSlot.Energy, "Crisis Inverted", DamageType.Arc, "hand-cannon", 2888266564u),
            ],
            build.Weapons.Select(weapon => (weapon.Slot, weapon.Name, weapon.Type, weapon.Archetype.UnwrapOr(""), weapon.Hash.Map(hash => hash.Value).UnwrapOr(0u))));
        Assert.All(build.Weapons, weapon => Assert.Empty(weapon.Perks));                                                      // a loadout carries no perks
        Assert.DoesNotContain(build.LeftOut, item => item.Hash == ItemHash.From(4019651319u));
        Assert.Contains(build.LeftOut, item => item == new LeftOutItem(LoadoutPart.Item, ItemHash.From(593554567u)));         // Luminopotent Mask
        Assert.DoesNotContain(build.LeftOut, item => item.Hash == ItemHash.From(95544328u));                                  // Triple Jump: the build's jump
        Assert.Contains("jump: triple-jump", session.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_jump_the_rules_know_is_the_builds_jump_and_one_they_dont_is_left_out_with_no_part_in_a_loop()
    {
        // Arcstrider (2328211300) with Blink (95544329, in the rules); Gunslinger (2240888816) with its own Triple Jump (1128768652, not).
        var arcstrider = MapLoadout("""{ "name": "x", "classType": 1, "equipped": [ { "hash": 2328211300, "socketOverrides": { "1": 95544329 } } ] }""");
        var json = """{ "name": "x", "classType": 1, "equipped": [ { "hash": 2240888816, "socketOverrides": { "1": 1128768652 } } ] }""";
        var session = LoopDesigning.ReadDimLink(Link).Bind(link => LoopDesigning.StartDimDesign(Catalog, link, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
        var gunslinger = session.Build.Build;
        var leftOutJump = new LeftOutItem(LoadoutPart.SubclassPlug, ItemHash.From(1128768652u));

        Assert.Equal<AbilityPlug>(new AbilityPlug.Known(ElementId.From("hunter-blink")), arcstrider.Abilities.Jump);
        Assert.DoesNotContain(arcstrider.LeftOut, item => item.Hash == ItemHash.From(95544329u));
        Assert.Equal<AbilityPlug>(new AbilityPlug.Empty(), gunslinger.Abilities.Jump);
        Assert.Equal([leftOutJump], gunslinger.LeftOut);
        Assert.Equal(new LeftOutReason.NoLoopEffect("A jump: movement has no part in a loop."), LoopDesigning.ClassifyLeftOut(session.Build, leftOutJump));
        Assert.Empty(LoopDesigning.ListMissing(session.Build));
        Assert.DoesNotContain("jump:", session.Design.Build.Text, StringComparison.Ordinal);
        Assert.Contains("subclassPlugs: [1128768652]", session.Design.Build.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Arc_plug_the_rules_author_for_the_Hunter_is_left_out_of_a_Striker_and_the_loadout_opens()
    {
        // Striker (2932390016): Knockout (an aspect the rules don't know), Spark of Shock and Skip Grenade (the Hunter's in the rules).
        var json = """{ "name": "x", "classType": 0, "equipped": [ { "hash": 2932390016, "socketOverrides": { "3": 146194908, "5": 1656549674, "7": 1727069364 } } ] }""";

        var session = LoopDesigning.ReadDimLink(Link).Bind(link => LoopDesigning.StartDimDesign(Catalog, link, json))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

        Assert.Equal((GuardianClass.Titan, Subclass.Arc), (session.Build.Build.Class, session.Build.Build.Subclass));
        Assert.Equal<AbilityPlug>(new AbilityPlug.Unknown(), session.Build.Build.Abilities.Grenade);
        Assert.Empty(session.Build.Build.Fragments);
        Assert.Equal(
            [146194908u, 1656549674u, 1727069364u],
            session.Build.Build.LeftOut.Where(item => item.Part == LoadoutPart.SubclassPlug).Select(item => item.Hash.Value).Order());
        Assert.True(LoopDesigning.CanEditSubclass(session.Build));
    }

    private static Build MapLoadout(string json) =>
        DimLoadoutMapping.MapLoadout(Catalog, Link, json).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
