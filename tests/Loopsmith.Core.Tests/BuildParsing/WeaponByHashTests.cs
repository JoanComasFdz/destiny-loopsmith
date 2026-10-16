using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.BuildParsing;

/// <summary>
/// A build file's weapon given by <c>hash</c> (docs/rule-format.md, "Build file"): its slot, name, damage type and
/// archetype are Bungie's manifest's (the source of truth), whatever the file says; a hash the manifest doesn't know as a
/// weapon is refused.
/// </summary>
public sealed class WeaponByHashTests
{
    private const string Header = """
        name: By hash
        class: hunter
        subclass: arc

        """;

    [Fact]
    public void A_weapon_given_only_by_its_hash_takes_its_slot_name_damage_type_and_archetype_from_the_manifest()
    {
        var build = Parse(Header + """
            weapons:
              - { hash: 4019651319, perks: [slice] }
              - { hash: 2888266564 }
            """);

        Assert.Equal(
            [
                (WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.Some("grenade-launcher")),
                (WeaponSlot.Energy, "Crisis Inverted", DamageType.Arc, Optional.Some("hand-cannon")),
            ],
            build.Weapons.Select(weapon => (weapon.Slot, weapon.Name, weapon.Type, weapon.Archetype)));
        Assert.Equal(["slice"], build.Weapons[0].Perks.Select(perk => perk.Value));
    }

    [Fact]
    public void The_manifest_wins_over_a_slot_name_damage_type_and_archetype_the_file_gives_with_the_hash()
    {
        var build = Parse(Header + """
            weapons:
              - { slot: energy, name: Old Name, type: arc, archetype: shotgun, hash: 4019651319 }
            """);

        var weapon = Assert.Single(build.Weapons);
        Assert.Equal((WeaponSlot.Kinetic, "Festival Flight", DamageType.Strand, Optional.Some("grenade-launcher")), (weapon.Slot, weapon.Name, weapon.Type, weapon.Archetype));
    }

    [Theory]
    [InlineData("1234567", "weapon hash 1234567 isn't in the manifest, so the file can't be imported")]
    [InlineData("923806249", "weapon hash 923806249 isn't in the manifest, so the file can't be imported")]   // Slice: a perk, not a weapon
    public void A_weapon_hash_the_manifest_doesnt_know_as_a_weapon_is_refused_even_with_its_name(string hash, string message)
    {
        var error = ParseError(Header + $"""
            weapons:
              - {"{"} slot: energy, name: Some Weapon, type: arc, archetype: hand-cannon, hash: {hash} {"}"}
            """);

        Assert.Equal($"by-hash.build.yaml:5: {message}", error);
    }

    [Fact]
    public void The_hashes_a_build_file_gives_its_weapons_by_are_what_the_full_manifest_is_read_for_when_the_excerpt_lacks_them()
    {
        var file = new SourceText("by-hash.build.yaml", Header + """
            weapons:
              - { hash: 4019651319 }
              - { hash: 1234567, perks: [slice] }
              - { slot: power, name: Speleologist, type: solar, archetype: machine-gun }
            """);
        var loop = new SourceText("by-hash.loop.yaml", "loop: By hash\nsteps: []\nbuild: |\n" + string.Concat(file.Text.Split('\n').Select(line => $"  {line}\n")));

        Assert.Equal([ItemHash.From(1234567u)], LoopDesigning.ListUnknownBuildFileHashes(RepoCatalog.Catalog, file));
        Assert.Equal([ItemHash.From(1234567u)], LoopDesigning.ListUnknownLoopFileHashes(RepoCatalog.Catalog, loop));
    }

    [Fact]
    public void A_file_that_doesnt_read_lists_no_hashes_to_read_the_manifest_for()
    {
        var file = new SourceText("broken.build.yaml", "weapons: [ { hash: 1234567 }\n");

        Assert.Empty(LoopDesigning.ListUnknownBuildFileHashes(RepoCatalog.Catalog, file));
    }

    private static Build Parse(string text) =>
        BuildFileParsing.ParseBuildFile(new SourceText("by-hash.build.yaml", text), RepoCatalog.Manifest)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    private static string ParseError(string text) =>
        Assert.IsType<Result<Build, string>.Error>(BuildFileParsing.ParseBuildFile(new SourceText("by-hash.build.yaml", text), RepoCatalog.Manifest)).Failure;
}
