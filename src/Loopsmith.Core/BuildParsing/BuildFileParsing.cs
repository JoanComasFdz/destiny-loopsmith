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
        "jump", "aspects", "fragments", "exoticArmor", "armorSetBonuses", "armorMods", "artifact", "artifactPerks", "weapons", "stats",
        "leftOut", "armorCopies", "armorModPlugs",
    ];

    private static readonly ImmutableArray<string> WeaponKeys = ["slot", "name", "type", "archetype", "perks", "hash", "roll", "catalyst", "instance", "mod"];
    private static readonly ImmutableArray<string> StatKeys = ["weapons", "health", "class", "grenade", "super", "melee"];
    private static readonly ImmutableArray<string> LeftOutKeys = ["items", "subclassPlugs", "armorMods", "artifactPerks"];
    private static readonly ImmutableArray<string> ArmorCopyKeys = ["hash", "instance"];
    private static readonly ImmutableArray<string> ArtifactKeys = ["hash", "slots"];

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
            map.ReadOrDefault("armorCopies", copies => copies.ReadEach("armor copy", ReadArmorCopy), []),
            map.ReadOrDefault("armorModPlugs", plugs => plugs.ReadEach("armor mod plug", ReadItemHash), []),
            map.ReadOptional("artifact", ReadArtifact),
            (build, leftOut, armorCopies, armorModPlugs, artifact) => build with { LeftOut = leftOut, ArmorCopies = armorCopies, ArmorModPlugs = armorModPlugs, Artifact = artifact });

    /// <summary>Everything but <c>leftOut</c>, <c>armorCopies</c>, <c>armorModPlugs</c> and <c>artifact</c> (one <c>Combine</c> takes at most 16 inputs).</summary>
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
                [],
                [],
                [],
                Optional.None<ArtifactLoadout>()));

    /// <summary>
    /// Each ability, and the jump, is an element id, or <c>"?"</c> for one the build has but Loopsmith can't name; a slot
    /// left out holds nothing (a build started from scratch, or a jump the rules don't have: <c>leftOut.subclassPlugs</c>).
    /// </summary>
    private static Result<AbilityLoadout, Errors> ReadAbilities(YamlMap map) =>
        Combine(
            map.ReadOrDefault("super", ReadAbility, new AbilityPlug.Empty()),
            map.ReadOrDefault("grenade", ReadAbility, new AbilityPlug.Empty()),
            map.ReadOrDefault("melee", ReadAbility, new AbilityPlug.Empty()),
            map.ReadOrDefault("classAbility", ReadAbility, new AbilityPlug.Empty()),
            map.ReadOrDefault("jump", ReadAbility, new AbilityPlug.Empty()),
            (super, grenade, melee, classAbility, jump) => new AbilityLoadout(super, grenade, melee, classAbility, jump));

    private static Result<AbilityPlug, Errors> ReadAbility(YamlValue value) =>
        value.ParseWith(text => text == DomainPhrasing.Unknown
            ? new Result<AbilityPlug, string>.Ok(new AbilityPlug.Unknown())
            : ElementId.TryFrom(text).ToResult().Map(id => (AbilityPlug)new AbilityPlug.Known(id)));

    /// <summary>
    /// <c>{ slot, name, type, archetype?, perks?, hash?, roll?, catalyst?, instance?, mod? }</c>, with an <c>archetype</c> or a <c>hash</c>; <c>roll: ["?", 243981275]</c> is
    /// the perk picked in each trait column, by manifest hash, in column order ("?": not picked); <c>catalyst: true</c>
    /// turns an exotic's catalyst on (default false); <c>instance: "6917…"</c> is the copy it is (a quoted instance id);
    /// <c>mod: 3820243349</c> is the weapon mod in its mod socket, by manifest hash.
    /// </summary>
    private static Result<WeaponLoadout, Errors> ReadWeapon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(WeaponKeys),
            map.ReadRequired("slot", ReadVocabularyWord<WeaponSlot>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("type", ReadVocabularyWord<DamageType>),
            map.ReadOptional("archetype", ReadSlug),
            map.ReadOrDefault("perks", ReadElementIds, []),
            map.ReadOptional("hash", ReadItemHash),
            map.ReadOrDefault("roll", roll => roll.ReadEach(roll.Label, ReadRollPerk), []),
            map.ReadOrDefault("catalyst", ReadBoolean, false),
            map.ReadOptional("instance", instance => instance.ParseWith(text => ItemInstanceId.TryFrom(text).ToResult())),
            map.ReadOptional("mod", ReadItemHash),
            (_, slot, name, type, archetype, perks, hash, roll, catalyst, instance, mod) => new WeaponLoadout(slot, name, type, archetype, perks, hash, roll, catalyst, instance, mod)))
            .Bind(weapon => RequireArchetypeOrHash(value, weapon));

    /// <summary>
    /// A weapon whose archetype no one knows is refused: its <c>hash</c> gives it (from Bungie's manifest, when the build is
    /// composed), or else its <c>archetype</c> says it. A perk that goes by the archetype (Demolitionist's) would show both amounts.
    /// </summary>
    private static Result<WeaponLoadout, Errors> RequireArchetypeOrHash(YamlValue value, WeaponLoadout weapon) =>
        weapon.Archetype.IsSome() || weapon.Hash.IsSome()
            ? Succeed(weapon)
            : value.FailAt<WeaponLoadout>($"weapon '{weapon.Name}' needs an `archetype` or a `hash`");

    private static Result<bool, Errors> ReadBoolean(YamlValue value) =>
        value.ParseWith<bool>(text => text switch
        {
            "true" => new Result<bool, string>.Ok(true),
            "false" => new Result<bool, string>.Ok(false),
            _ => new Result<bool, string>.Error($"'{text}' is not true or false"),
        });

    private static Result<Optional<ItemHash>, Errors> ReadRollPerk(YamlValue value) =>
        value.ToText().Bind(text => text == DomainPhrasing.Unknown
            ? Succeed(Optional.None<ItemHash>())
            : ReadItemHash(value).Map(Optional.Some));

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

    /// <summary>
    /// <c>{ hash: 593554567, instance: "6917530185805135602" }</c>: which copy of an armor piece the build wears, by the
    /// copy's instance id (a number, quoted: it is larger than a YAML reader's integers).
    /// </summary>
    private static Result<ArmorCopy, Errors> ReadArmorCopy(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(ArmorCopyKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("instance", instance => instance.ParseWith(text => ItemInstanceId.TryFrom(text).ToResult())),
            (_, hash, instance) => new ArmorCopy(hash, instance)));

    /// <summary>
    /// <c>artifact: { hash: 3821685388, slots: [3585856470, "?", …] }</c>: the artifact the build equips and the perk in
    /// each of its perk slots, by manifest hash, in slot order (<c>"?"</c>: an empty slot).
    /// </summary>
    private static Result<ArtifactLoadout, Errors> ReadArtifact(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(ArtifactKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadOrDefault("slots", slots => slots.ReadEach("artifact slot", ReadRollPerk), []),
            (_, hash, slots) => new ArtifactLoadout(hash, slots)));

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
