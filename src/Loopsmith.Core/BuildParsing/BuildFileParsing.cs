using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.BuildParsing.ResultAccumulation;
using static Loopsmith.Core.BuildParsing.YamlReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.BuildParsing.ParseError>;

namespace Loopsmith.Core.BuildParsing;

/// <summary>
/// <c>builds/&lt;build&gt;/build.yaml</c> (see <c>docs/rule-format.md</c>) → <see cref="Build"/>. Pure. Only the
/// file's own shape is checked here; whether the ids exist in the catalog is BuildComposition's job.
/// </summary>
public static class BuildFileParsing
{
    private static readonly ImmutableArray<string> BuildKeys =
    [
        "name", "author", "source", "catalog", "class", "subclass", "super", "grenade", "melee", "classAbility",
        "aspects", "fragments", "exoticArmor", "armorSetBonuses", "armorMods", "artifactPerks", "weapons", "stats",
    ];

    private static readonly ImmutableArray<string> WeaponKeys = ["slot", "name", "type", "archetype", "perks"];
    private static readonly ImmutableArray<string> StatKeys = ["weapons", "health", "class", "grenade", "super", "melee"];

    private static readonly StatLine NoStats = new(
        Optional.None<StatValue>(),
        Optional.None<StatValue>(),
        Optional.None<StatValue>(),
        Optional.None<StatValue>(),
        Optional.None<StatValue>(),
        Optional.None<StatValue>());

    /// <summary>
    /// Parses one build file; every problem is reported at once, one <c>file:line: message</c> per line.
    /// Lists may be omitted (empty); repeated ids are kept in order (a repeated mod is a stacked copy).
    /// </summary>
    public static Result<Build, string> ParseBuildFile(SourceText file) =>
        LoadDocument(file, "build")
            .Bind(root => root.ToMap())
            .Bind(ReadBuild)
            .MapError(FormatErrors);

    private static Result<Build, Errors> ReadBuild(YamlMap map) =>
        Combine(
            map.CheckKeys(BuildKeys),
            map.ReadRequired("name", ReadName),
            map.ReadOptional("author", ReadName),
            map.ReadOptional("source", ReadWebAddress),
            map.ReadOptional("catalog", value => value.ParseWith(text => CatalogVersion.TryFrom(text).ToResult())),
            map.ReadRequired("class", ReadVocabularyWord<GuardianClass>),
            map.ReadRequired("subclass", ReadVocabularyWord<Subclass>),
            ReadAbilities(map),
            map.ReadOrDefault("aspects", ReadElementIds, []),
            map.ReadOrDefault("fragments", ReadElementIds, []),
            map.ReadOptional("exoticArmor", ReadElementId),
            map.ReadOrDefault("armorSetBonuses", ReadElementIds, []),
            map.ReadOrDefault("armorMods", ReadElementIds, []),
            map.ReadOrDefault("artifactPerks", ReadElementIds, []),
            map.ReadOrDefault("weapons", weapons => weapons.ReadEach("weapon", ReadWeapon), []),
            map.ReadOrDefault("stats", ReadStats, NoStats),
            (_, name, author, source, catalog, guardianClass, subclass, abilities, aspects, fragments, exoticArmor,
                armorSetBonuses, armorMods, artifactPerks, weapons, stats) => new Build(
                name,
                author,
                source,
                catalog,
                guardianClass,
                subclass,
                abilities,
                aspects,
                fragments,
                exoticArmor,
                armorSetBonuses,
                armorMods,
                artifactPerks,
                weapons,
                stats));

    private static Result<AbilityLoadout, Errors> ReadAbilities(YamlMap map) =>
        Combine(
            map.ReadRequired("super", ReadElementId),
            map.ReadRequired("grenade", ReadElementId),
            map.ReadRequired("melee", ReadElementId),
            map.ReadRequired("classAbility", ReadElementId),
            (super, grenade, melee, classAbility) => new AbilityLoadout(super, grenade, melee, classAbility));

    private static Result<WeaponLoadout, Errors> ReadWeapon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(WeaponKeys),
            map.ReadRequired("slot", ReadVocabularyWord<WeaponSlot>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("type", ReadVocabularyWord<DamageType>),
            map.ReadOptional("archetype", ReadSlug),
            map.ReadOrDefault("perks", ReadElementIds, []),
            (_, slot, name, type, archetype, perks) => new WeaponLoadout(slot, name, type, archetype, perks)));

    /// <summary><c>stats: { weapons: 47, class: 104, … }</c> — any subset, each 0..200.</summary>
    private static Result<StatLine, Errors> ReadStats(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StatKeys),
            map.ReadOptional("weapons", ReadStatValue),
            map.ReadOptional("health", ReadStatValue),
            map.ReadOptional("class", ReadStatValue),
            map.ReadOptional("grenade", ReadStatValue),
            map.ReadOptional("super", ReadStatValue),
            map.ReadOptional("melee", ReadStatValue),
            (_, weapons, health, guardianClass, grenade, super, melee) =>
                new StatLine(weapons, health, guardianClass, grenade, super, melee)));

    private static Result<StatValue, Errors> ReadStatValue(YamlValue value) =>
        value.ParseWith(text => ParseInteger(text).Bind(number => StatValue.TryFrom(number).ToResult()
            .MapError(message => $"{number} is out of range ({message})")));

    private static Result<ImmutableArray<ElementId>, Errors> ReadElementIds(YamlValue value) =>
        value.ReadEach(value.Label, ReadElementId);

    private static Result<ElementId, Errors> ReadElementId(YamlValue value) =>
        value.ParseWith(text => ElementId.TryFrom(text).ToResult());

    private static Result<string, Errors> ReadName(YamlValue value) =>
        value.ParseWith<string>(text => string.IsNullOrWhiteSpace(text)
            ? new Result<string, string>.Error("must not be empty")
            : new Result<string, string>.Ok(text));

    /// <summary>A kebab-case word that is not a catalog id (a weapon archetype: <c>grenade-launcher</c>).</summary>
    private static Result<string, Errors> ReadSlug(YamlValue value) =>
        value.ParseWith<string>(text => ElementId.IsSlug(text)
            ? new Result<string, string>.Ok(text)
            : new Result<string, string>.Error($"'{text}' is not kebab-case"));

    private static Result<string, Errors> ReadWebAddress(YamlValue value) =>
        value.ParseWith<string>(text =>
            Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? new Result<string, string>.Ok(text)
                : new Result<string, string>.Error($"'{text}' is not an absolute http(s) URL"));

    private static string FormatErrors(Errors errors) =>
        string.Join(
            "\n",
            errors
                .Distinct()
                .OrderBy(error => error.Line)
                .Select(error => $"{error.Path}:{error.Line}: {error.Message}"));
}
