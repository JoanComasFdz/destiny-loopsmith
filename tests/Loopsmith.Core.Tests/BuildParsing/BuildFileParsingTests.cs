using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.Tests.RuleParsing.ParsingAssertions;

namespace Loopsmith.Core.Tests.BuildParsing;

public sealed class BuildFileParsingTests
{
    private const string BuildPath = "builds/skip-grenade-hunter/build.yaml";

    /// <summary>The example of docs/rule-format.md, verbatim.</summary>
    private const string SpecExampleYaml = """
        # abridged and illustrative — the real one is builds/skip-grenade-hunter/build.yaml
        name: Skip Grenade Hunter
        author: Plunderthabooty                      # optional
        source: https://www.youtube.com/watch?v=zvd6sNS463E   # optional
        catalog: authored-0123456789ab               # optional pin (ADRs D15)
        class: hunter
        subclass: arc                                # arc|solar|void|stasis|strand|prismatic
        super: arc-staff
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        aspects: [tempest-strike, flow-state]
        fragments: [spark-of-resistance, spark-of-frequency, spark-of-shock, spark-of-discharge]
        exoticArmor: shinobus-vow                    # optional
        armorSetBonuses: [luminopotent-2pc, luminopotent-4pc]
        armorMods: [elemental-charge, elemental-charge, grenade-kickstart, grenade-kickstart, bomber]   # repeat = stacked copies
        artifactPerks: [defibrillating-blast, flashover]
        weapons:
          - { slot: kinetic, name: Festival Flight, type: strand, archetype: grenade-launcher, perks: [slice] }
        stats: { weapons: 47, class: 104, grenade: 145, super: 27, melee: 79 }   # any subset; 0..200
        """;

    private const string MinimalYaml = """
        name: Bare Hunter
        class: hunter
        subclass: arc
        super: arc-staff
        grenade: skip-grenade
        melee: combination-blow
        classAbility: gamblers-dodge
        """;

    private static Build ParseValidBuild(string yaml) =>
        AssertOk(BuildFileParsing.ParseBuildFile(new SourceText(BuildPath, yaml)));

    private static string ParseInvalidBuild(string yaml) =>
        AssertError(BuildFileParsing.ParseBuildFile(new SourceText(BuildPath, yaml)));

    private static IEnumerable<string> ToNames(IEnumerable<ElementId> ids) => ids.Select(id => id.Value);

    private static Optional<StatValue> ToStat(int value) => Optional.Some(StatValue.From(value));

    [Fact]
    public void Parses_the_spec_example()
    {
        var build = ParseValidBuild(SpecExampleYaml);

        Assert.Equal("Skip Grenade Hunter", build.Name);
        Assert.Equal(Optional.Some("Plunderthabooty"), build.Author);
        Assert.Equal(Optional.Some("https://www.youtube.com/watch?v=zvd6sNS463E"), build.SourceUrl);
        Assert.Equal(Optional.Some(CatalogVersion.From("authored-0123456789ab")), build.PinnedCatalog);
        Assert.Equal(GuardianClass.Hunter, build.Class);
        Assert.Equal(Subclass.Arc, build.Subclass);
        Assert.Equal(
            new AbilityLoadout(
                ElementId.From("arc-staff"),
                ElementId.From("skip-grenade"),
                ElementId.From("combination-blow"),
                ElementId.From("gamblers-dodge")),
            build.Abilities);
        Assert.Equal(["tempest-strike", "flow-state"], ToNames(build.Aspects));
        Assert.Equal(
            ["spark-of-resistance", "spark-of-frequency", "spark-of-shock", "spark-of-discharge"],
            ToNames(build.Fragments));
        Assert.Equal(Optional.Some(ElementId.From("shinobus-vow")), build.ExoticArmor);
        Assert.Equal(["luminopotent-2pc", "luminopotent-4pc"], ToNames(build.ArmorSetBonuses));
        Assert.Equal(["defibrillating-blast", "flashover"], ToNames(build.ArtifactPerks));

        var weapon = Assert.Single(build.Weapons);
        Assert.Equal(WeaponSlot.Kinetic, weapon.Slot);
        Assert.Equal("Festival Flight", weapon.Name);
        Assert.Equal(DamageType.Strand, weapon.Type);
        Assert.Equal(Optional.Some("grenade-launcher"), weapon.Archetype);
        Assert.Equal(["slice"], ToNames(weapon.Perks));

        Assert.Equal(
            new StatLine(ToStat(47), Optional.None<StatValue>(), ToStat(104), ToStat(145), ToStat(27), ToStat(79)),
            build.Stats);
    }

    [Fact]
    public void Repeated_mods_are_kept_in_order()
    {
        var build = ParseValidBuild(SpecExampleYaml);

        Assert.Equal(
            ["elemental-charge", "elemental-charge", "grenade-kickstart", "grenade-kickstart", "bomber"],
            ToNames(build.ArmorMods));
    }

    [Fact]
    public void Optional_fields_and_lists_may_be_omitted()
    {
        var build = ParseValidBuild(MinimalYaml);

        Assert.Equal(Optional.None<string>(), build.Author);
        Assert.Equal(Optional.None<string>(), build.SourceUrl);
        Assert.Equal(Optional.None<CatalogVersion>(), build.PinnedCatalog);
        Assert.Equal(Optional.None<ElementId>(), build.ExoticArmor);
        Assert.Empty(build.Aspects);
        Assert.Empty(build.Fragments);
        Assert.Empty(build.ArmorSetBonuses);
        Assert.Empty(build.ArmorMods);
        Assert.Empty(build.ArtifactPerks);
        Assert.Empty(build.Weapons);
        Assert.Equal(
            new StatLine(
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>(),
                Optional.None<StatValue>()),
            build.Stats);
    }

    [Fact]
    public void A_null_optional_field_counts_as_omitted()
    {
        var build = ParseValidBuild(MinimalYaml + "\nexoticArmor: ~\n");

        Assert.Equal(Optional.None<ElementId>(), build.ExoticArmor);
    }

    [Fact]
    public void Stats_cover_the_whole_range_including_health()
    {
        var build = ParseValidBuild(MinimalYaml + "\nstats: { health: 0, grenade: 200 }\n");

        Assert.Equal(ToStat(0), build.Stats.Health);
        Assert.Equal(ToStat(200), build.Stats.Grenade);
    }

    [Fact]
    public void Stats_out_of_range_are_rejected()
    {
        var message = ParseInvalidBuild(MinimalYaml + """

            stats:
              grenade: 201
              melee: -5
            """);

        Assert.Equal(
            string.Join(
                "\n",
                $"{BuildPath}:9: build.stats.grenade: 201 is out of range (Stat must be within 0..200)",
                $"{BuildPath}:10: build.stats.melee: -5 is out of range (Stat must be within 0..200)"),
            message);
    }

    [Fact]
    public void Unknown_keys_are_errors_at_their_line()
    {
        var message = ParseInvalidBuild(MinimalYaml + """

            mood: confident
            stats: { mobility: 100 }
            weapons:
              - { slot: energy, name: Ex Diris, type: arc, frame: rapid-fire }
            """);

        Assert.Contains($"{BuildPath}:8: unknown key 'mood' in build", message);
        Assert.Contains($"{BuildPath}:9: unknown key 'mobility' in build.stats", message);
        Assert.Contains($"{BuildPath}:11: unknown key 'frame' in weapon", message);
    }

    [Fact]
    public void An_ability_given_as_a_question_mark_is_unknown_and_left_out_hashes_are_kept_by_part()
    {
        var build = ParseValidBuild("""
            name: From DIM
            source: https://dim.gg/4j5nz4q
            class: hunter
            subclass: arc
            super: "?"
            grenade: "?"
            melee: combination-blow
            classAbility: gamblers-dodge
            leftOut: { items: [2005, 2006], armorMods: [3007] }
            """);

        Assert.Equal(
            new AbilityLoadout(Optional.None<ElementId>(), Optional.None<ElementId>(), ElementId.From("combination-blow"), ElementId.From("gamblers-dodge")),
            build.Abilities);
        Assert.Equal(
            [(LoadoutPart.Item, 2005u), (LoadoutPart.Item, 2006u), (LoadoutPart.ArmorMod, 3007u)],
            build.LeftOut.Select(item => (item.Part, item.Hash.Value)));
    }

    [Fact]
    public void A_left_out_list_takes_manifest_hashes_by_part()
    {
        var message = ParseInvalidBuild(MinimalYaml + """

            leftOut: { weapons: [1], items: [abc] }
            """);

        Assert.Contains($"{BuildPath}:8: unknown key 'weapons' in build.leftOut", message);
        Assert.Contains("'abc' is not a manifest hash", message);
    }

    [Fact]
    public void Missing_required_fields_are_errors()
    {
        var message = ParseInvalidBuild("""
            name: Half a Hunter
            class: hunter
            grenade: skip-grenade
            """);

        Assert.Equal(
            string.Join(
                "\n",
                $"{BuildPath}:1: build is missing 'subclass'",
                $"{BuildPath}:1: build is missing 'super'",
                $"{BuildPath}:1: build is missing 'melee'",
                $"{BuildPath}:1: build is missing 'classAbility'"),
            message);
    }

    [Fact]
    public void Every_field_error_is_reported_together()
    {
        var message = ParseInvalidBuild("""
            name: Wrong Hunter
            source: not-a-url
            class: wizard
            subclass: arc
            super: Arc Staff
            grenade: skip-grenade
            melee: combination-blow
            classAbility: gamblers-dodge
            aspects: tempest-strike
            weapons:
              - { slot: heavy, name: Festival Flight, type: strand }
            stats: { grenade: lots }
            """);

        Assert.Equal(
            string.Join(
                "\n",
                $"{BuildPath}:2: build.source: 'not-a-url' is not an absolute http(s) URL",
                $"{BuildPath}:3: build.class: 'wizard' is not one of: hunter, titan, warlock",
                $"{BuildPath}:5: build.super: 'Arc Staff' is not a kebab-case id",
                $"{BuildPath}:9: build.aspects must be a list ([ … ])",
                $"{BuildPath}:11: weapon.slot: 'heavy' is not one of: kinetic, energy, power",
                $"{BuildPath}:12: build.stats.grenade: 'lots' is not a whole number"),
            message);
    }

    [Fact]
    public void Invalid_yaml_is_an_error_at_its_line()
    {
        var message = ParseInvalidBuild("""
            name: Skip Grenade Hunter
            aspects: [tempest-strike, flow-state
            """);

        Assert.StartsWith($"{BuildPath}:", message);
        Assert.Contains("invalid YAML", message);
    }

    [Fact]
    public void A_build_file_must_be_a_mapping()
    {
        var message = ParseInvalidBuild("- just a list\n");

        Assert.Equal($"{BuildPath}:1: build must be a mapping ({{ key: value }})", message);
    }
}
