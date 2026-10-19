using System.Collections.Immutable;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.LoopFiles;

/// <summary>Writing build files (a build from a DIM link), and the round trip: the written file reads back as the same build.</summary>
public sealed class BuildFileWritingTests
{
    private static readonly Build MinimalBuild = ParseBuild(new SourceText("minimal.yaml", """
        name: Bare Hunter
        class: hunter
        subclass: arc
        super: "?"
        grenade: "?"
        melee: "?"
        classAbility: "?"
        """));

    [Theory]
    [InlineData("builds/skip-grenade-hunter/build.yaml")]
    [InlineData("builds/skip-grenade-hunter-ascension/build.yaml")]
    public void A_bundled_build_written_out_reads_back_the_same(string path)
    {
        var build = ParseBuild(new SourceText(path, File.ReadAllText(RepoFiles.ToPath(path))));

        var written = BuildFileWriting.WriteBuildFile(build, []);

        Assert.Equal(written, BuildFileWriting.WriteBuildFile(ParseBuild(new SourceText("written.yaml", written)), []));
    }

    [Fact]
    public void A_weapon_roll_is_written_with_a_question_mark_for_a_column_not_picked()
    {
        var weapon = new WeaponLoadout(WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.None<AmmoType>(), Optional.None<string>(), [], Optional.Some(ItemHash.From(4019651319u)),
            [Optional.None<ItemHash>(), Optional.Some(ItemHash.From(243981275u))], false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>());

        var written = BuildFileWriting.WriteBuildFile(MinimalBuild with { Weapons = [weapon] }, []);

        Assert.Contains("- {hash: 4019651319, perks: [], roll: [\"?\", 243981275]} # Festival Flight\n", written, StringComparison.Ordinal);
        Assert.Equal(weapon.Roll, Assert.Single(ParseBuild(new SourceText("written.yaml", written)).Weapons).Roll);
    }

    [Fact]
    public void A_weapons_copy_is_written_as_its_instance_id_and_read_back()
    {
        var weapon = new WeaponLoadout(WeaponSlot.Power, "Thunderlord", DamageType.Arc, Optional.None<AmmoType>(), Optional.None<string>(), [], Optional.Some(ItemHash.From(3325463374u)),
            [], false, Optional.Some(ItemInstanceId.From("6917529810172542011")), Optional.None<ItemHash>());

        var written = BuildFileWriting.WriteBuildFile(MinimalBuild with { Weapons = [weapon] }, []);

        Assert.Contains("- {hash: 3325463374, perks: [], instance: \"6917529810172542011\"} # Thunderlord\n", written, StringComparison.Ordinal);
        Assert.Equal(weapon.Instance, Assert.Single(ParseBuild(new SourceText("written.yaml", written)).Weapons).Instance);
    }

    [Fact]
    public void Unknown_abilities_left_out_hashes_and_notes_are_written_as_the_format_says()
    {
        var build = new Build(
            "Grenadier: \"skip\" #1",
            Optional.None<string>(),
            Optional.Some("https://app.destinyitemmanager.com/loadouts?loadout={\"name\":\"A\"}"),
            Optional.None<CatalogVersion>(),
            GuardianClass.Hunter,
            Subclass.Arc,
            new AbilityLoadout(new AbilityPlug.Unknown(), new AbilityPlug.Unknown(), new AbilityPlug.Unknown(), new AbilityPlug.Known(ElementId.From("gamblers-dodge")), new AbilityPlug.Empty()),
            [ElementId.From("tempest-strike")],
            [],
            Optional.None<ElementId>(),
            [],
            [ElementId.From("bomber"), ElementId.From("bomber")],
            [],
            [],
            new StatLine(Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>(), Optional.None<StatValue>()),
            [new LeftOutItem(LoadoutPart.Item, ItemHash.From(2005u)), new LeftOutItem(LoadoutPart.ArmorMod, ItemHash.From(3007u)), new LeftOutItem(LoadoutPart.Item, ItemHash.From(2006u))],
            [],
            [ItemHash.From(3008u)],
            Optional.None<ArtifactLoadout>());

        var written = BuildFileWriting.WriteBuildFile(build, ["From a DIM link.\nSecond line."]);
        var read = ParseBuild(new SourceText("written.yaml", written));

        Assert.StartsWith("# From a DIM link.\n# Second line.\n", written, StringComparison.Ordinal);
        Assert.Contains("super: \"?\"\n", written, StringComparison.Ordinal);
        Assert.Contains("leftOut: {items: [2005, 2006], armorMods: [3007]}\n", written, StringComparison.Ordinal);
        Assert.Equal(build.Name, read.Name);
        Assert.Equal(build.SourceUrl, read.SourceUrl);
        Assert.Equal(build.Abilities, read.Abilities);
        Assert.Equal(["bomber", "bomber"], read.ArmorMods.Select(id => id.Value));
        Assert.Contains("armorModPlugs: [3008]\n", written, StringComparison.Ordinal);
        Assert.Equal(build.ArmorModPlugs, read.ArmorModPlugs);
        Assert.Equal(
            [(LoadoutPart.Item, 2005u), (LoadoutPart.Item, 2006u), (LoadoutPart.ArmorMod, 3007u)],
            read.LeftOut.Select(item => (item.Part, item.Hash.Value)));
    }

    [Fact]
    public void A_jump_is_written_after_the_class_ability_and_read_back()
    {
        var known = MinimalBuild with { Abilities = MinimalBuild.Abilities with { Jump = new AbilityPlug.Known(ElementId.From("hunter-blink")) } };
        var unknown = MinimalBuild with { Abilities = MinimalBuild.Abilities with { Jump = new AbilityPlug.Unknown() } };

        var writtenKnown = BuildFileWriting.WriteBuildFile(known, []);
        var writtenUnknown = BuildFileWriting.WriteBuildFile(unknown, []);

        Assert.Contains("classAbility: \"?\"\njump: hunter-blink\naspects: []\n", writtenKnown, StringComparison.Ordinal);
        Assert.Contains("jump: \"?\"\n", writtenUnknown, StringComparison.Ordinal);
        Assert.DoesNotContain("jump:", BuildFileWriting.WriteBuildFile(MinimalBuild, []), StringComparison.Ordinal);   // no jump: no entry
        Assert.Equal(known.Abilities, ParseBuild(new SourceText("written.yaml", writtenKnown)).Abilities);
        Assert.Equal(unknown.Abilities, ParseBuild(new SourceText("written.yaml", writtenUnknown)).Abilities);
    }

    private static Build ParseBuild(SourceText file) =>
        BuildFileParsing.ParseBuildFile(file, RepoCatalog.Manifest).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
