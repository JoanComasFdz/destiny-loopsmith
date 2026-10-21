using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.LoopFiles;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.LoopFiles;

/// <summary>Changing one weapon of a build file in place: everything else, comments included, stays as its author wrote it.</summary>
public sealed class BuildFileEditingTests
{
    private const string Header = """
        # The author's note.
        name: Bare Hunter
        class: hunter
        subclass: arc
        super: "?"
        grenade: "?"
        melee: "?"
        classAbility: "?"

        """;

    private static readonly WeaponLoadout Picked = new(
        WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.Some(AmmoType.Special), Optional.None<string>(), [], Optional.Some(ItemHash.From(4019651319u)),
        [Optional.Some(ItemHash.From(923806249u))], false, Optional.None<ItemInstanceId>(), Optional.None<ItemHash>());

    [Fact]
    public void A_flow_weapon_entry_is_replaced_with_its_name_after_it_and_every_other_line_kept()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              # Slice per the video.
              - { slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }   # the GL
              - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }
            stats: { weapons: 47 }
            """);

        var edited = ReplaceWeapon(file, 0, Picked);

        // The comment after the entry on its line is the entry's: a weapon given by hash is followed by its name.
        Assert.Equal(file.Text.Replace(
            "{ slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }   # the GL",
            "{hash: 4019651319, perks: [], roll: [923806249]} # Festival Flight",
            StringComparison.Ordinal), edited);
        Assert.Equal(Picked.Roll, ParseBuild(edited).Weapons[0].Roll);
    }

    [Fact]
    public void A_block_weapon_entry_becomes_one_flow_entry_where_it_was()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }
              - slot: kinetic
                name: "Festival Flight"
                perks:
                  - slice
            # Stats follow.
            stats: { weapons: 47 }
            """);

        var edited = ReplaceWeapon(file, 1, Picked);

        Assert.EndsWith("""
            weapons:
              - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }
              - {hash: 4019651319, perks: [], roll: [923806249]} # Festival Flight
            # Stats follow.
            stats: { weapons: 47 }
            """, edited, StringComparison.Ordinal);
        Assert.StartsWith("# The author's note.\n", edited, StringComparison.Ordinal);
        Assert.Equal(2, ParseBuild(edited).Weapons.Length);
    }

    [Fact]
    public void A_weapon_added_goes_on_its_own_line_after_the_last_one_and_comments_stay()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }   # the energy one
            # Stats follow.
            stats: { weapons: 47 }
            """);

        var edited = AddWeapon(file, Picked);

        Assert.EndsWith("""
            weapons:
              - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }   # the energy one
              - {hash: 4019651319, perks: [], roll: [923806249]} # Festival Flight
            # Stats follow.
            stats: { weapons: 47 }
            """, edited, StringComparison.Ordinal);
        Assert.Equal(2, ParseBuild(edited).Weapons.Length);
    }

    [Theory]
    [InlineData("weapons: [{ slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }]\n", 2)]
    [InlineData("weapons: []\n", 1)]
    [InlineData("weapons: [{ slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }, ]\n", 2)]
    [InlineData("stats: { weapons: 47 }\n", 1)]
    public void A_weapon_is_added_to_a_flow_list_or_as_a_new_weapons_entry(string weapons, int count)
    {
        var edited = AddWeapon(new SourceText("b.yaml", Header + weapons), Picked);

        Assert.Equal(count, ParseBuild(edited).Weapons.Length);
        Assert.Equal(Picked.Roll, ParseBuild(edited).Weapons[^1].Roll);
    }

    [Fact]
    public void A_weapon_given_by_hash_replaced_twice_keeps_one_name_after_it()
    {
        var file = new SourceText("b.yaml", Header + "weapons:\n  - { hash: 2888266564 } # Crisis Inverted\nstats: { weapons: 47 }\n");

        var once = ReplaceWeapon(file, 0, Picked);
        var twice = ReplaceWeapon(file with { Text = once }, 0, Picked with { Roll = [] });

        Assert.EndsWith("weapons:\n  - {hash: 4019651319, perks: []} # Festival Flight\nstats: { weapons: 47 }\n", twice, StringComparison.Ordinal);
    }

    [Fact]
    public void A_weapon_given_by_hash_in_a_flow_list_has_no_name_after_it()
    {
        var file = new SourceText("b.yaml", Header + "weapons: [{ hash: 2888266564 }, { hash: 3325463374 }]\n");

        var edited = ReplaceWeapon(file, 0, Picked);

        Assert.EndsWith("weapons: [{hash: 4019651319, perks: [], roll: [923806249]}, { hash: 3325463374 }]\n", edited, StringComparison.Ordinal);
    }

    [Fact]
    public void A_weapon_removed_from_a_block_list_takes_its_lines_and_the_rest_stays()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              # Slice per the video.
              - { slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }   # the GL
              - slot: energy
                name: Crisis Inverted
                type: arc
                hash: 2888266564
            # Stats follow.
            stats: { weapons: 47 }
            """);

        var first = RemoveWeapon(file, 0);
        var second = RemoveWeapon(file, 1);

        Assert.EndsWith("""
            weapons:
              # Slice per the video.
              - slot: energy
                name: Crisis Inverted
                type: arc
                hash: 2888266564
            # Stats follow.
            stats: { weapons: 47 }
            """, first, StringComparison.Ordinal);
        Assert.EndsWith("""
            weapons:
              # Slice per the video.
              - { slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }   # the GL
            # Stats follow.
            stats: { weapons: 47 }
            """, second, StringComparison.Ordinal);
        Assert.Equal(["Crisis Inverted"], ParseBuild(first).Weapons.Select(weapon => weapon.Name));
        Assert.Equal(["Festival Flight"], ParseBuild(second).Weapons.Select(weapon => weapon.Name));
    }

    [Theory]
    [InlineData(0, "weapons: [{ slot: power, name: Thunderlord, type: arc, hash: 3325463374 }]\n")]
    [InlineData(1, "weapons: [{ slot: kinetic, name: Festival Flight, type: strand, hash: 4019651319 }]\n")]
    public void A_weapon_removed_from_a_flow_list_takes_its_comma(int index, string kept)
    {
        var file = new SourceText("b.yaml", Header + "weapons: [{ slot: kinetic, name: Festival Flight, type: strand, hash: 4019651319 }, { slot: power, name: Thunderlord, type: arc, hash: 3325463374 }]\n");

        var edited = RemoveWeapon(file, index);

        Assert.Equal(Header + kept, edited);
        Assert.Single(ParseBuild(edited).Weapons);
    }

    [Theory]
    [InlineData("weapons:\n  - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }\nstats: { weapons: 47 }\n")]
    [InlineData("weapons: [{ slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }]\nstats: { weapons: 47 }\n")]
    public void The_last_weapon_removed_takes_the_weapons_entry_with_it(string weapons)
    {
        var edited = RemoveWeapon(new SourceText("b.yaml", Header + weapons), 0);

        Assert.Equal(Header + "stats: { weapons: 47 }\n", edited);
        Assert.Empty(ParseBuild(edited).Weapons);
        Assert.IsType<Result<string, string>.Error>(BuildFileEditing.RemoveWeapon(new SourceText("b.yaml", Header + weapons), 1));
    }

    [Fact]
    public void A_weapon_the_file_does_not_have_is_an_error()
    {
        var file = new SourceText("b.yaml", Header + "weapons:\n  - { slot: energy, name: Crisis Inverted, type: arc, hash: 2888266564 }\n");

        var past = Assert.IsType<Result<string, string>.Error>(BuildFileEditing.ReplaceWeapon(file, 1, Picked));
        var none = Assert.IsType<Result<string, string>.Error>(BuildFileEditing.ReplaceWeapon(new SourceText("b.yaml", Header), 0, Picked));

        Assert.Contains("the build has no weapon 2", past.Failure);
        Assert.Contains("the build has no weapons", none.Failure);
    }

    [Theory]
    [InlineData("weapons: [\n  { slot: kinetic }\nstats: {}\n")]
    [InlineData("weapons:\n  - { slot: kinetic\n")]
    public void Unreadable_yaml_is_an_error_not_an_exception(string text)
    {
        var edited = BuildFileEditing.ReplaceWeapon(new SourceText("b.yaml", text), 0, Picked);

        Assert.Contains("b.yaml:", Assert.IsType<Result<string, string>.Error>(edited).Failure);
    }

    private static string ReplaceWeapon(SourceText file, int index, WeaponLoadout weapon) =>
        BuildFileEditing.ReplaceWeapon(file, index, weapon).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string AddWeapon(SourceText file, WeaponLoadout weapon) =>
        BuildFileEditing.AddWeapon(file, weapon).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string RemoveWeapon(SourceText file, int index) =>
        BuildFileEditing.RemoveWeapon(file, index).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static Build ParseBuild(string text) =>
        BuildFileParsing.ParseBuildFile(new SourceText("edited.yaml", text), RepoCatalog.Manifest).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
