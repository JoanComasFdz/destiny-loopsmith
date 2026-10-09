using System.Collections.Immutable;
using System.Text;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.LoopFiles;
using static Loopsmith.Core.Tests.RuleParsing.ParsingAssertions;

namespace Loopsmith.Core.Tests.LoopFiles;

/// <summary>Writing loop files, and the round-trip rule: <c>ParseLoopFile(WriteLoopFile(design)) == design</c>.</summary>
public sealed class LoopFileWritingTests
{
    private const string BuildText = "# my build\nname: Skip Grenade Hunter   # inline comment\nclass: hunter\nweapons:\n  - { slot: kinetic, name: \"Festival Flight\" }\n";

    private static readonly ImmutableArray<LoopStep> SomeSteps =
    [
        new(new PlayerAction.UseClassAbility(), Optional.Some("Arm Slice + Reaper")),
        new(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.One), Optional.None<string>()),
        new(new PlayerAction.FireWeapon(WeaponSlot.Kinetic, HitOutcome.Damage, TargetCount.One), Optional.None<string>()),
        new(new PlayerAction.CastAbility(OffensiveAbility.Grenade, HitOutcome.Kill, TargetCount.From(3)), Optional.Some("three in the pack")),
        new(new PlayerAction.FireWeapon(WeaponSlot.Energy, HitOutcome.Damage, TargetCount.From(5)), Optional.None<string>()),
        new(new PlayerAction.CollectPickups(PickupId.From("orb-of-power")), Optional.Some("grab them all")),
        new(new PlayerAction.Wait(Seconds.From(2.25m)), Optional.None<string>()),
    ];

    private static LoopDesign CreateDesign(
        string name = "Infinite skip grenades",
        string? author = "Joan",
        string? description = "Dodge, skip grenade, shoot.\nRepeat.\n",
        string build = BuildText,
        ImmutableArray<LoopStep> steps = default) =>
        new(
            name,
            author is null ? Optional.None<string>() : Optional.Some(author),
            description is null ? Optional.None<string>() : Optional.Some(description),
            Optional.Some(CatalogVersion.From("authored-e4426d03166b")),
            new SourceText("builds/x/build.yaml", build),
            steps.IsDefault ? SomeSteps : steps);

    [Fact]
    public void Writes_the_keys_in_spec_order_with_the_build_last_as_a_literal_block()
    {
        var text = LoopFileWriting.WriteLoopFile(CreateDesign());

        Assert.Equal(
            """
            # Loopsmith loop v1
            loop: Infinite skip grenades
            author: Joan
            description: |
              Dodge, skip grenade, shoot.
              Repeat.
            catalog: authored-e4426d03166b
            steps:
              - do: class
                note: Arm Slice + Reaper
              - do: grenade:kill
              - do: kinetic
              - do: grenade:kill:3
                note: three in the pack
              - do: energy:hit:5
              - do: pickup:orb-of-power
                note: grab them all
              - do: wait:2.25
            build: |
              # my build
              name: Skip Grenade Hunter   # inline comment
              class: hunter
              weapons:
                - { slot: kinetic, name: "Festival Flight" }

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            text);
    }

    [Fact]
    public void An_empty_loop_writes_an_empty_step_list()
    {
        var text = LoopFileWriting.WriteLoopFile(CreateDesign(author: null, description: null, steps: []));

        Assert.Contains("\nsteps: []\n", text);
        Assert.DoesNotContain("\nauthor:", text);
        Assert.DoesNotContain("\ndescription:", text);
        AssertRoundTrips(CreateDesign(author: null, description: null, steps: []));
    }

    public static TheoryData<string> TrickyTexts =>
    [
        "plain",
        "",
        "null",
        "~",
        "NULL",
        "true",
        "123",
        "a: b",
        "key: value # not a comment",
        "#hash first",
        "- looks like a list",
        "'single quoted'",
        "\"double quoted\"",
        "  leading spaces",
        "trailing spaces  ",
        "tab\tinside",
        "multi\nline\ntext",
        "ends with newline\n",
        "two trailing newlines\n\n",
        "\nleading newline",
        "\n",
        "\n\n\n",
        "  \nwhitespace-only first line",
        "line with trailing space \nnext",
        "crlf\r\nline\r\n",
        "lone\rcarriage return",
        "ünïcødé ✓ ▰▱ — …",
        "emoji 🔥 inside",
        "\uFEFFbyte order mark",
        "next\u0085line",
        "line\u2028separator",
        "control\u0001char",
        "{ flow: mapping }",
        "[flow, sequence]",
        "&anchor *alias !tag |pipe >fold %directive @at `tick",
        "--- document marker",
        "... document end",
        "colon at end:",
        "question? mark",
        "a very long line " + string.Concat(Enumerable.Repeat("that keeps going ", 20)),
    ];

    [Theory]
    [MemberData(nameof(TrickyTexts))]
    public void Any_name_author_description_and_note_round_trips(string text)
    {
        var steps = SomeSteps.SetItem(0, SomeSteps[0] with { Note = Optional.Some(text) });

        AssertRoundTrips(CreateDesign(name: text, author: text, description: text, steps: steps));
    }

    [Theory]
    [MemberData(nameof(TrickyTexts))]
    public void Any_build_text_round_trips_byte_identical(string text) =>
        AssertRoundTrips(CreateDesign(build: text));

    /// <summary>Property-style: many random designs from an alphabet of YAML-significant characters (fixed seed).</summary>
    [Fact]
    public void Random_designs_round_trip()
    {
        var random = new Random(20261009);
        var failures = Enumerable.Range(0, 400)
            .Select(_ => CreateRandomDesign(random))
            .Where(design => !IsSameDesign(design, ParseWritten(design)))
            .Select(design => LoopFileWriting.WriteLoopFile(design))
            .ToList();

        Assert.True(failures.Count == 0, $"{failures.Count} designs did not round-trip, e.g.:\n{failures.FirstOrDefault()}");
    }

    private static readonly string Alphabet =
        "ab Z09 \n\n  \t#:-'\"|>{}[],&*!%@`~?\\/." + "é✓—" + "\r" + (char)0x85 + (char)0x2028 + (char)0xFEFF + (char)1;

    private static readonly ImmutableArray<string> Tokens =
        ["class", "grenade", "grenade:kill", "melee:kill", "super", "kinetic:kill", "energy", "power:kill", "pickup:orb-of-power", "wait:5", "wait:0.5"];

    private static LoopDesign CreateRandomDesign(Random random)
    {
        string NextText(int maxLength) =>
            new([.. Enumerable.Range(0, random.Next(maxLength + 1)).Select(_ => Alphabet[random.Next(Alphabet.Length)])]);
        Optional<string> NextOptionalText() =>
            random.Next(3) == 0 ? Optional.None<string>() : Optional.Some(NextText(30));
        var steps = Enumerable.Range(0, random.Next(5))
            .Select(_ => new LoopStep(ParseToken(Tokens[random.Next(Tokens.Length)]), NextOptionalText()))
            .ToImmutableArray();
        var build = random.Next(4) == 0 ? BuildText : NextText(200);
        return new LoopDesign(NextText(20), NextOptionalText(), NextOptionalText(), Optional.None<CatalogVersion>(), new SourceText("b", build), steps);
    }

    private static PlayerAction ParseToken(string token) =>
        AssertOk(Loopsmith.Core.Phrasing.ActionTokenParsing.ParseActionToken(token));

    private static LoopDesign ParseWritten(LoopDesign design)
    {
        var text = LoopFileWriting.WriteLoopFile(design);
        return LoopFileParsing.ParseLoopFile(new SourceText("round-trip.loop.yaml", text)).Match(
            ok => ok.Value,
            error => throw new Xunit.Sdk.XunitException($"Written file does not parse:\n{error.Failure}\n---\n{text}"));
    }

    private static void AssertRoundTrips(LoopDesign design)
    {
        var parsed = ParseWritten(design);

        Assert.True(
            IsSameDesign(design, parsed),
            $"Round trip changed the design.\nWritten:\n{LoopFileWriting.WriteLoopFile(design)}\nBuild text: {Escape(parsed.Build.Text)} (expected {Escape(design.Build.Text)})");
    }

    /// <summary>Records holding <c>ImmutableArray</c> don't compare by content: compare field by field.</summary>
    private static bool IsSameDesign(LoopDesign expected, LoopDesign actual) =>
        expected.Name == actual.Name
        && expected.Author == actual.Author
        && expected.Description == actual.Description
        && expected.Catalog == actual.Catalog
        && expected.Build.Text == actual.Build.Text
        && expected.Steps.SequenceEqual(actual.Steps);

    private static string Escape(string text) =>
        new StringBuilder().AppendJoin("", text.Select(c => c < ' ' || c > '~' ? $"\\u{(int)c:X4}" : c.ToString())).ToString();
}
