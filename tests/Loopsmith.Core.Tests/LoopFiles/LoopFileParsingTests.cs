using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using static Loopsmith.Core.Tests.RuleParsing.ParsingAssertions;

namespace Loopsmith.Core.Tests.LoopFiles;

public sealed class LoopFileParsingTests
{
    private const string LoopPath = "builds/x/loops/test.loop.yaml";

    /// <summary>The example of docs/loop-format.md, verbatim.</summary>
    private const string SpecExampleYaml = """
        # Loopsmith loop v1
        loop: Dodge, grenade, shoot             # required — the loop's name
        author: Joan                            # optional
        description: |                          # optional, free text
          Dodge to arm Slice and Reaper, skip grenade into the pack, shoot, grab Reaper's orb.
        catalog: authored-e4426d03166b          # optional — catalog version it was designed against
        steps:                                  # required (may be empty), in order
          - do: class                           # an action token (below)
            note: Arm Slice + Reaper            # optional
          - do: grenade:kill
          - do: kinetic:kill
          - do: pickup:orb-of-power
          - do: max:bolt-charge
            note: Shinobu's Vow and Flashover; the next grenade hit strikes
        build: |                                # required — the build file's full text (docs/rule-format.md)
          name: Skip Grenade Hunter
          class: hunter
          ...
        """;

    private static LoopDesign ParseValidLoop(string yaml) =>
        AssertOk(LoopFileParsing.ParseLoopFile(new SourceText(LoopPath, yaml)));

    private static string ParseInvalidLoop(string yaml) =>
        AssertError(LoopFileParsing.ParseLoopFile(new SourceText(LoopPath, yaml)));

    [Fact]
    public void Parses_the_spec_example()
    {
        var design = ParseValidLoop(SpecExampleYaml);

        Assert.Equal("Dodge, grenade, shoot", design.Name);
        Assert.Equal(Optional.Some("Joan"), design.Author);
        Assert.Equal(Optional.Some("Dodge to arm Slice and Reaper, skip grenade into the pack, shoot, grab Reaper's orb.\n"), design.Description);
        Assert.Equal(Optional.Some(CatalogVersion.From("authored-e4426d03166b")), design.Catalog);
        Assert.Equal(
            [
                new PlayerAction.UseClassAbility(),
                new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.CollectPickups(PickupId.From("orb-of-power")),
                new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge"))),
            ],
            design.Steps.Select(step => step.Action));
        Assert.Equal(
            [Optional.Some("Arm Slice + Reaper"), Optional.None<string>(), Optional.None<string>(), Optional.None<string>(), Optional.Some("Shinobu's Vow and Flashover; the next grenade hit strikes")],
            design.Steps.Select(step => step.Note));
        Assert.Equal("name: Skip Grenade Hunter\nclass: hunter\n...", design.Build.Text);   // the example has no final line break
        Assert.Equal(LoopPath + "#build", design.Build.Path);
    }

    [Fact]
    public void Optional_keys_may_be_omitted_and_steps_may_be_empty()
    {
        var design = ParseValidLoop("loop: Empty\nsteps: []\nbuild: \"name: X\"\n");

        Assert.Equal(Optional.None<string>(), design.Author);
        Assert.Equal(Optional.None<string>(), design.Description);
        Assert.Equal(Optional.None<CatalogVersion>(), design.Catalog);
        Assert.Empty(design.Steps);
        Assert.Equal("name: X", design.Build.Text);
    }

    [Fact]
    public void Action_tokens_are_read_with_the_shared_grammar()
    {
        var design = ParseValidLoop("""
            loop: Tokens
            steps:
              - do: melee
              - do: SUPER:KILL
              - do: power:kill
              - do: max:bolt-charge
              - do: END:Amplified
            build: x
            """);

        Assert.Equal(
            [
                new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Damage, TargetCount.One),
                new PlayerAction.CastAbility(OffensiveAbility.Super, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.Declare(new StateDeclaration.ReachMax(StatusId.From("bolt-charge"))),
                new PlayerAction.Declare(new StateDeclaration.EndStatus(StatusId.From("amplified"))),
            ],
            design.Steps.Select(step => step.Action));
    }

    [Fact]
    public void Unknown_keys_are_errors_at_their_line()
    {
        var error = ParseInvalidLoop("""
            loop: Typo
            autor: Joan
            steps:
              - do: class
                notes: oops
            build: x
            """);

        Assert.Contains($"{LoopPath}:2: unknown key 'autor' in loop file", error);
        Assert.Contains($"{LoopPath}:5: unknown key 'notes' in step 1", error);
    }

    [Fact]
    public void Ability_and_weapon_tokens_may_say_how_many_enemies_they_hit_or_kill()
    {
        var design = ParseValidLoop("""
            loop: Targets
            steps:
              - do: grenade:kill:3
              - do: kinetic:hit:5
              - do: energy:kill:2
              - do: super:hit
              - do: melee:kill:1
              - do: power:hit:20
            build: x
            """);

        Assert.Equal(
            [
                new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3)),
                new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.From(5)),
                new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Kill, TargetCount.From(2)),
                new PlayerAction.CastAbility(OffensiveAbility.Super, HitOutcome.Damage, TargetCount.One),
                new PlayerAction.CastAbility(OffensiveAbility.Melee, HitOutcome.Kill, TargetCount.One),
                new PlayerAction.FireWeapon(WeaponSlot.Power, HitOutcome.Damage, TargetCount.Most),
            ],
            design.Steps.Select(step => step.Action));
    }

    [Theory]
    [InlineData("grenade:kill:0", "Invalid target count in 'grenade:kill:0'")]
    [InlineData("grenade:kill:21", "Invalid target count in 'grenade:kill:21'")]
    [InlineData("kinetic:hit:many", "Invalid target count in 'kinetic:hit:many'")]
    [InlineData("grenade:3", "Unknown action 'grenade:3'")]
    [InlineData("class:kill:2", "Unknown action 'class:kill:2'")]
    [InlineData("energy:kill:2:3", "Unknown action 'energy:kill:2:3'")]
    public void A_target_count_is_a_whole_number_from_1_to_20_after_hit_or_kill(string token, string expected)
    {
        var error = ParseInvalidLoop($"loop: L\nsteps:\n  - do: {token}\nbuild: x\n");

        Assert.Contains($"{LoopPath}:3: step 1.do: {expected}", error);
    }

    [Fact]
    public void Unknown_action_tokens_are_errors_at_their_line()
    {
        var error = ParseInvalidLoop("""
            loop: Bad token
            steps:
              - do: class
              - do: grenade:explode
              - do: dance
            build: x
            """);

        Assert.Contains($"{LoopPath}:4: step 2.do: Unknown action 'grenade:explode'", error);
        Assert.Contains($"{LoopPath}:5: step 3.do: Unknown action 'dance'", error);
    }

    [Fact]
    public void Missing_required_keys_are_all_reported()
    {
        var error = ParseInvalidLoop("author: Joan\n");

        Assert.Contains($"{LoopPath}:1: loop file is missing 'loop'", error);
        Assert.Contains($"{LoopPath}:1: loop file is missing 'steps'", error);
        Assert.Contains($"{LoopPath}:1: loop file is missing 'build'", error);
    }

    [Fact]
    public void A_step_needs_an_action()
    {
        var error = ParseInvalidLoop("loop: L\nsteps:\n  - note: no action\nbuild: x\n");

        Assert.Contains($"{LoopPath}:3: step 1 is missing 'do'", error);
    }

    [Theory]
    [InlineData("loop: L\nsteps: class\nbuild: x\n", "2: loop file.steps must be a list")]
    [InlineData("loop: L\nsteps:\n  - class\nbuild: x\n", "3: step 1 must be a mapping")]
    [InlineData("loop: L\nsteps: []\nbuild:\n  name: X\n", "4: loop file.build must be a single value")]
    [InlineData("loop: [a, b]\nsteps: []\nbuild: x\n", "1: loop file.loop must be a single value")]
    [InlineData("loop: L\ncatalog: ' '\nsteps: []\nbuild: x\n", "2: loop file.catalog: Catalog version must not be empty")]
    [InlineData("- loop: L\n", "1: loop file must be a mapping")]
    [InlineData("", "1: the file is empty")]
    [InlineData("loop: L\nsteps: [\nbuild: x\n", "invalid YAML")]
    [InlineData("loop: L\nsteps: [\n", "invalid YAML")]
    [InlineData("loop: A\nloop: B\nsteps: []\nbuild: x\n", "invalid YAML")]
    public void Malformed_files_are_located_errors(string yaml, string expected)
    {
        var error = ParseInvalidLoop(yaml);

        Assert.StartsWith(LoopPath + ":", error);
        Assert.Contains(expected, error);
    }
}
