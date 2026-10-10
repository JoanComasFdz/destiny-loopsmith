using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
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
        "leftOut",
    ];

    private static readonly ImmutableArray<string> WeaponKeys = ["slot", "name", "type", "archetype", "perks", "hash"];
    private static readonly ImmutableArray<string> StatKeys = ["weapons", "health", "class", "grenade", "super", "melee"];
    private static readonly ImmutableArray<string> LeftOutKeys = ["items", "subclassPlugs", "armorMods", "artifactPerks"];

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
            ReadEquipment(map),
            map.ReadOrDefault("leftOut", ReadLeftOut, []),
            (build, leftOut) => build with { LeftOut = leftOut });

    /// <summary>Everything but <c>leftOut</c> (one <c>Combine</c> takes at most 16 inputs).</summary>
    private static Result<Build, Errors> ReadEquipment(YamlMap map) =>
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
                stats,
                []));

    /// <summary>Each ability is an element id, or <c>"?"</c> for one the build has but Loopsmith can't name.</summary>
    private static Result<AbilityLoadout, Errors> ReadAbilities(YamlMap map) =>
        Combine(
            map.ReadRequired("super", ReadAbility),
            map.ReadRequired("grenade", ReadAbility),
            map.ReadRequired("melee", ReadAbility),
            map.ReadRequired("classAbility", ReadAbility),
            (super, grenade, melee, classAbility) => new AbilityLoadout(super, grenade, melee, classAbility));

    private static Result<Optional<ElementId>, Errors> ReadAbility(YamlValue value) =>
        value.ParseWith(text => text == DomainPhrasing.Unknown
            ? new Result<Optional<ElementId>, string>.Ok(Optional.None<ElementId>())
            : ElementId.TryFrom(text).ToResult().Map(Optional.Some));

    private static Result<WeaponLoadout, Errors> ReadWeapon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(WeaponKeys),
            map.ReadRequired("slot", ReadVocabularyWord<WeaponSlot>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("type", ReadVocabularyWord<DamageType>),
            map.ReadOptional("archetype", ReadSlug),
            map.ReadOrDefault("perks", ReadElementIds, []),
            map.ReadOptional("hash", ReadItemHash),
            (_, slot, name, type, archetype, perks, hash) => new WeaponLoadout(slot, name, type, archetype, perks, hash)));

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

    /// <summary>
    /// <c>leftOut: { items: [2531963421], subclassPlugs: […], armorMods: […], artifactPerks: […] }</c> — the manifest
    /// hashes of what the DIM loadout had that the catalog doesn't know yet, by where the loadout listed them.
    /// </summary>
    private static Result<ImmutableArray<LeftOutItem>, Errors> ReadLeftOut(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(LeftOutKeys),
            map.ReadOrDefault("items", hashes => ReadLeftOutPart(hashes, LoadoutPart.Item), []),
            map.ReadOrDefault("subclassPlugs", hashes => ReadLeftOutPart(hashes, LoadoutPart.SubclassPlug), []),
            map.ReadOrDefault("armorMods", hashes => ReadLeftOutPart(hashes, LoadoutPart.ArmorMod), []),
            map.ReadOrDefault("artifactPerks", hashes => ReadLeftOutPart(hashes, LoadoutPart.ArtifactPerk), []),
            (_, items, plugs, mods, perks) => items.AddRange(plugs).AddRange(mods).AddRange(perks)));

    private static Result<ImmutableArray<LeftOutItem>, Errors> ReadLeftOutPart(YamlValue value, LoadoutPart part) =>
        value.ReadEach(value.Label, hash => ReadItemHash(hash).Map(read => new LeftOutItem(part, read)));

    private static Result<ItemHash, Errors> ReadItemHash(YamlValue value) =>
        value.ParseWith(text => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hash)
            ? ItemHash.TryFrom(hash).ToResult()
            : new Result<ItemHash, string>.Error($"'{text}' is not a manifest hash (an unsigned 32-bit number)"));

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
