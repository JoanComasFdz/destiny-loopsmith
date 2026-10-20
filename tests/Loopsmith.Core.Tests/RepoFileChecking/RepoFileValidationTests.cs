using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.RepoFileChecking;

/// <summary>
/// Every build and loop file of the repository opens against its rules, as the designer's import opens it: the e2e
/// scenarios' fixtures (<c>*.build.yaml</c>, <c>*.loop.yaml</c> under <c>tests/Loopsmith.Web.E2E/Scenarios</c> and <c>Rules</c>).
/// A fixture meant to be refused is listed in <see cref="Refused"/> with the error it must give; one whose weapon the
/// designer reads from Bungie's full manifest is listed in <see cref="FullManifestOnly"/> with the hashes the excerpt
/// (<c>rules/manifest.yaml</c>) doesn't know, the only error it may give here.
/// Run it alone: <c>dotnet test --filter RepoFileValidationTests</c>.
/// </summary>
public sealed class RepoFileValidationTests
{
    /// <summary>The fixtures an e2e scenario imports to see them refused, each with a part of the error it shows.</summary>
    private static readonly ImmutableDictionary<string, string> Refused = new Dictionary<string, string>
    {
        ["tests/Loopsmith.Web.E2E/Scenarios/ImportExport/BuildImport/crisis-inverted-without-hash.build.yaml"] = "weapon 'Crisis Inverted' needs a `hash`",
        ["tests/Loopsmith.Web.E2E/Scenarios/ImportExport/WeaponByHash/unknown-weapon-hash.build.yaml"] = "1234567",
    }.ToImmutableDictionary();

    /// <summary>The fixtures whose weapons are read from Bungie's full manifest, each with the hashes the excerpt doesn't know.</summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<uint>> FullManifestOnly = new Dictionary<string, ImmutableArray<uint>>
    {
        ["tests/Loopsmith.Web.E2E/Scenarios/ImportExport/WeaponByHash/ogma-pr6-by-hash.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/AllClasses/Arc/Fragments/SparkOfBeacons/spark-of-beacons-spark-of-volts-matador-64.build.yaml"] = [1518956169],
        ["tests/Loopsmith.Web.E2E/Rules/Hunter/ExoticArmor/MechaneersTricksleeves/mechaneers-tricksleeves-buzzard.build.yaml"] = [805677041],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/bellowing-giant-reapers-tithe.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/crux-celeritas-reapers-tithe-strand-siphon.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/festival-flight-one-for-all-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/ogma-pr6-stats-for-all.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/ogma-pr6-unrelenting.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/scathelocke-veterans-wisdom.build.yaml"] = [3332538050],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/VeteransWisdom/veterans-wisdom-scathelocke.build.yaml"] = [3332538050],
        ["tests/Loopsmith.Web.E2E/Scenarios/Loop/Weapons/EnemyCount/speleologist-radiolaria-transposer.build.yaml"] = [412251536],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/AllStar/all-star-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/AdhesiveOrdnance/adhesive-ordnance-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/AdhesiveOrdnance/adhesive-ordnance-enhanced-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/AllStar/all-star-enhanced-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/StatsForAll/stats-for-all-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/StatsForAll/stats-for-all-enhanced-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/Unrelenting/unrelenting-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/Unrelenting/unrelenting-enhanced-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/ReapersTithe/reapers-tithe-bellowing-giant.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/ReapersTithe/reapers-tithe-enhanced-bellowing-giant.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/RadiolariaTransposer/radiolaria-transposer-speleologist.build.yaml"] = [412251536],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/RadiolariaTransposer/radiolaria-transposer-enhanced-speleologist.build.yaml"] = [412251536],
        ["tests/Loopsmith.Web.E2E/Rules/Weapons/Perks/VeteransWisdom/veterans-wisdom-enhanced-scathelocke.build.yaml"] = [3332538050],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Armor/StrandSiphon/strand-siphon-crux-celeritas.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Armor/GrenadeKickstart/grenade-kickstart-skip-grenade-crux-celeritas-strand-siphon.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Armor/Recuperation/recuperation-crux-celeritas-strand-siphon.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Armor/PowerfulAttraction/powerful-attraction-marksmans-dodge-crux-celeritas-strand-siphon.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Armor/ChargedUp/charged-up-crux-celeritas-strand-siphon.build.yaml"] = [391069235],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/ArcStasisDualSiphon/arc-stasis-dual-siphon-crisis-inverted-stay-frosty.build.yaml"] = [2812100428],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/KineticChargeSiphon/kinetic-charge-siphon-scathelocke-crisis-inverted.build.yaml"] = [3332538050],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/KineticEntropySiphon/kinetic-entropy-siphon-scathelocke-bellowing-giant.build.yaml"] = [3332538050, 412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/KineticSiphon/kinetic-siphon-scathelocke.build.yaml"] = [3332538050],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/KineticThermalSiphon/kinetic-thermal-siphon-scathelocke-ogma-pr6.build.yaml"] = [3332538050, 2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/SolarSiphon/solar-siphon-ogma-pr6.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/SolarStrandDualSiphon/solar-strand-dual-siphon-ogma-pr6-festival-flight.build.yaml"] = [2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/StasisSiphon/stasis-siphon-stay-frosty.build.yaml"] = [2812100428],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/ThermodynamicSiphon/thermodynamic-siphon-stay-frosty-ogma-pr6.build.yaml"] = [2812100428, 2342054803],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/VoidSiphon/void-siphon-bellowing-giant.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/VoidStasisDualSiphon/void-stasis-dual-siphon-bellowing-giant-stay-frosty.build.yaml"] = [412265080, 2812100428],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Helmet/VoidStrandDualSiphon/void-strand-dual-siphon-bellowing-giant-festival-flight.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/Synergy/synergy-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/Synergy/synergy-matador-64.build.yaml"] = [1518956169],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/Synergy/synergy-speleologist.build.yaml"] = [412251536],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/Synergy/synergy-bellowing-giant.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/Synergy/synergy-forensic-nightmare.build.yaml"] = [1526296434],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/SynergyEnhanced/synergy-enhanced-battle-scar.build.yaml"] = [1525080480],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/SynergyEnhanced/synergy-enhanced-matador-64.build.yaml"] = [1518956169],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/SynergyEnhanced/synergy-enhanced-speleologist.build.yaml"] = [412251536],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/SynergyEnhanced/synergy-enhanced-bellowing-giant.build.yaml"] = [412265080],
        ["tests/Loopsmith.Web.E2E/Rules/Mods/Weapons/SynergyEnhanced/synergy-enhanced-forensic-nightmare.build.yaml"] = [1526296434],
    }.ToImmutableDictionary();

    public static TheoryData<string> Opening
    {
        get
        {
            var files = ListRepoFiles();                                                            // impure

            return [.. files.Where(path => !Refused.ContainsKey(path) && !FullManifestOnly.ContainsKey(path))];
        }
    }

    public static TheoryData<string> Refusing => [.. Refused.Keys.Order(StringComparer.Ordinal)];

    public static TheoryData<string> NeedingTheFullManifest => [.. FullManifestOnly.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Opening))]
    public void A_repository_file_opens_against_the_rules(string path)
    {
        var fullPath = RepoFiles.ToPath(path);                    // pure
        var file = FileSourceFetching.ReadTextFile(fullPath);     // impure

        var opened = file.Bind(read => OpenFile(read with { Path = path }));

        Assert.True(opened is Result<DesignSession, string>.Ok, opened.Match(_ => "", error => NameFile(path, error.Failure)));
    }

    [Theory]
    [MemberData(nameof(Refusing))]
    public void A_fixture_meant_to_be_refused_is_refused_with_its_error(string path)
    {
        var fullPath = RepoFiles.ToPath(path);                    // pure
        var file = FileSourceFetching.ReadTextFile(fullPath);     // impure

        var opened = file.Bind(read => OpenFile(read with { Path = path }));

        var error = Assert.IsType<Result<DesignSession, string>.Error>(opened).Failure;
        Assert.Contains($"{path}:", error);
        Assert.Contains(Refused[path], error);
    }

    [Theory]
    [MemberData(nameof(NeedingTheFullManifest))]
    public void A_fixture_read_from_the_full_manifest_lacks_only_its_listed_hashes(string path)
    {
        var fullPath = RepoFiles.ToPath(path);                    // pure
        var file = FileSourceFetching.ReadTextFile(fullPath);     // impure

        var read = file.Map(text => text with { Path = path });
        var unknown = read.Match(ok => ListUnknownHashes(ok.Value), _ => []);
        var opened = read.Bind(OpenFile);

        Assert.Equal(FullManifestOnly[path], unknown.Select(hash => hash.Value));
        var error = Assert.IsType<Result<DesignSession, string>.Error>(opened).Failure;
        Assert.Contains($"weapon hash {FullManifestOnly[path][0]} isn't in the manifest", error);
    }

    [Fact]
    public void Every_kind_of_repository_file_is_found()
    {
        var files = ListRepoFiles();   // impure

        Assert.Contains(files, path => path.EndsWith(".build.yaml", StringComparison.Ordinal));
        Assert.All(Refused.Keys.Concat(FullManifestOnly.Keys), path => Assert.Contains(path, files));
    }

    private static Result<DesignSession, string> OpenFile(SourceText file) =>
        file.Path.EndsWith(".loop.yaml", StringComparison.Ordinal)
            ? LoopDesigning.ImportLoop(RepoCatalog.Catalog, file)
            : LoopDesigning.StartDesign(RepoCatalog.Catalog, file, Path.GetFileName(file.Path));

    /// <summary>The error, led by the file it is about (most errors already name their file and line).</summary>
    private static string NameFile(string path, string error) =>
        error.StartsWith(path, StringComparison.Ordinal) ? error : $"{path}: {error}";

    private static ImmutableArray<ItemHash> ListUnknownHashes(SourceText file) =>
        file.Path.EndsWith(".loop.yaml", StringComparison.Ordinal)
            ? LoopDesigning.ListUnknownLoopFileHashes(RepoCatalog.Catalog, file)
            : LoopDesigning.ListUnknownBuildFileHashes(RepoCatalog.Catalog, file);

    /// <summary>The repository's build and loop fixture files, relative to its root and sorted.</summary>
    private static ImmutableArray<string> ListRepoFiles()
    {
        var scenariosRoot = RepoFiles.ToPath("tests/Loopsmith.Web.E2E/Scenarios");                  // pure
        var rulesRoot = RepoFiles.ToPath("tests/Loopsmith.Web.E2E/Rules");                          // pure
        var fixtures = Directory.EnumerateFiles(scenariosRoot, "*.yaml", SearchOption.AllDirectories).ToImmutableArray();  // impure
        var ruleFixtures = Directory.EnumerateFiles(rulesRoot, "*.yaml", SearchOption.AllDirectories).ToImmutableArray();  // impure

        return [.. fixtures.Concat(ruleFixtures)                                                      // pure
            .Where(file => file.EndsWith(".build.yaml", StringComparison.Ordinal) || file.EndsWith(".loop.yaml", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
    }
}
