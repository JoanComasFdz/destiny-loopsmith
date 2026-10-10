using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;

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
        WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.None<string>(), [], Optional.Some(ItemHash.From(4019651319u)),
        [Optional.Some(ItemHash.From(923806249u))]);

    [Fact]
    public void A_flow_weapon_entry_is_replaced_and_every_other_line_kept()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              # Slice per the video.
              - { slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }   # the GL
              - { slot: energy, name: Any Arc weapon, type: arc }
            stats: { weapons: 47 }
            """);

        var edited = ReplaceWeapon(file, 0, Picked);

        Assert.Equal(file.Text.Replace(
            "{ slot: kinetic, name: Festival Flight, type: strand, perks: [slice], hash: 4019651319 }",
            "{slot: kinetic, name: Festival Flight, type: strand, perks: [], hash: 4019651319, roll: [923806249]}",
            StringComparison.Ordinal), edited);
        Assert.Equal(Picked.Roll, ParseBuild(edited).Weapons[0].Roll);
    }

    [Fact]
    public void A_block_weapon_entry_becomes_one_flow_entry_where_it_was()
    {
        var file = new SourceText("b.yaml", Header + """
            weapons:
              - { slot: energy, name: Any Arc weapon, type: arc }
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
              - { slot: energy, name: Any Arc weapon, type: arc }
              - {slot: kinetic, name: Festival Flight, type: strand, perks: [], hash: 4019651319, roll: [923806249]}
            # Stats follow.
            stats: { weapons: 47 }
            """, edited, StringComparison.Ordinal);
        Assert.StartsWith("# The author's note.\n", edited, StringComparison.Ordinal);
        Assert.Equal(2, ParseBuild(edited).Weapons.Length);
    }

    [Fact]
    public void A_weapon_the_file_does_not_have_is_an_error()
    {
        var file = new SourceText("b.yaml", Header + "weapons:\n  - { slot: energy, name: Any Arc weapon, type: arc }\n");

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

    private static Build ParseBuild(string text) =>
        BuildFileParsing.ParseBuildFile(new SourceText("edited.yaml", text)).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
