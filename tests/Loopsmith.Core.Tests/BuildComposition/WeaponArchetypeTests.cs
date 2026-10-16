using Loopsmith.Core.BuildComposition;
using Loopsmith.Core.BuildParsing;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.BuildComposition;

/// <summary>
/// A weapon's archetype (docs/rule-format.md, "Build file"): a weapon with a <c>hash</c> takes it from Bungie's manifest
/// (the source of truth), one without names it in <c>archetype</c>, and a weapon with neither is refused when the file is read.
/// </summary>
public sealed class WeaponArchetypeTests
{
    private const string Header = """
        name: Archetypes
        class: hunter
        subclass: arc

        """;

    private static readonly RuleCatalog Catalog =
        FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"))
            .Bind(CatalogLoading.ParseCatalog)
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void A_weapon_with_a_hash_takes_its_archetype_from_the_manifest_even_over_the_files()
    {
        var build = Validate(Header + """
            weapons:
              - { slot: kinetic, name: Festival Flight, type: strand, hash: 4019651319 }
              - { slot: energy, name: Crisis Inverted, type: arc, archetype: shotgun, hash: 2888266564 }
              - { slot: power, name: Speleologist, type: solar, archetype: machine-gun }
            """);

        Assert.Equal(
            [Optional.Some("grenade-launcher"), Optional.Some("hand-cannon"), Optional.Some("machine-gun")],
            build.Build.Weapons.Select(weapon => weapon.Archetype));
    }

    [Fact]
    public void Demolitionist_on_a_weapon_known_by_its_hash_gives_its_archetypes_one_amount()
    {
        var build = Validate(Header + """
            weapons:
              - { slot: energy, name: Crisis Inverted, type: arc, perks: [demolitionist], hash: 2888266564 }
            """);

        var demolitionist = Assert.Single(build.Equipped, equipped => equipped.Element.Id.Value == "demolitionist").Element;
        var grants = demolitionist.Rules.SelectMany(rule => rule.Then).OfType<Outcome.GrantEnergy>();

        Assert.Equal(new EnergyGrant.Fraction(new GameValue.Known(0.10m)), Assert.Single(grants).Amount);
    }

    [Fact]
    public void A_weapon_with_neither_an_archetype_nor_a_hash_is_refused_in_a_loop_files_build_too()
    {
        const string loop = """
            loop: Unknown weapon
            steps:
              - do: pack:new
            build: |
              name: Archetypes
              class: hunter
              subclass: arc
              weapons:
                - { slot: energy, name: Any Arc weapon, type: arc }
            """;

        var imported = LoopDesigning.ImportLoop(Catalog, new SourceText("unknown.loop.yaml", loop));

        var error = Assert.IsType<Result<DesignSession, string>.Error>(imported);
        Assert.Contains("weapon 'Any Arc weapon' needs an `archetype` or a `hash`", error.Failure);
    }

    private static ValidatedBuild Validate(string text) =>
        BuildFileParsing.ParseBuildFile(new SourceText("archetypes.build.yaml", text))
            .Bind(build => BuildValidation.ValidateBuild(build, Catalog))
            .Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));
}
