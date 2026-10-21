using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Functional;
using Loopsmith.Core.Phrasing;
using static Loopsmith.Core.BuildParsing.ResultAccumulation;
using static Loopsmith.Core.BuildParsing.YamlReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.BuildParsing.ParseError>;

namespace Loopsmith.Core.BuildParsing;

/// <summary>
/// A <c>build.yaml</c> (see <c>docs/rule-format.md</c>) → <see cref="Build"/>. Pure. Only the
/// file's own shape is checked here; whether the ids exist in the catalog is BuildComposition's job. The one thing read
/// from elsewhere is what Bungie's manifest says of a weapon, which the file gives by <c>hash</c>: its slot, name, damage
/// type, ammo type and archetype (the manifest is the source of truth, so the file doesn't copy them).
/// </summary>
public static class BuildFileParsing
{
    /// <summary>What a weapon given by hash says of itself besides the hash: the rest is the manifest's.</summary>
    private sealed record WeaponChoices(
        Optional<string> Archetype,
        ImmutableArray<ElementId> Perks,
        ImmutableArray<Optional<ItemHash>> Roll,
        bool Catalyst,
        Optional<ItemInstanceId> Instance,
        Optional<ItemHash> Mod);

    /// <summary>A weapon of the manifest: its item (name, type), its slot and its damage type.</summary>
    private sealed record ManifestWeapon(ManifestItem Item, WeaponSlot Slot, DamageType Type, Optional<AmmoType> Ammo);

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
    /// Lists may be omitted (empty); repeated ids are kept in order (a repeated mod is a stacked copy). A weapon given by
    /// <c>hash</c> is read from <paramref name="manifest"/>: one it doesn't know is refused.
    /// </summary>
    public static Result<Build, string> ParseBuildFile(SourceText file, ManifestExcerpt manifest) =>
        LoadDocument(file, "build")
            .Bind(root => root.ToMap())
            .Bind(map => ReadBuild(manifest, map))
            .MapError(FormatErrors);

    /// <summary>
    /// The hashes of the weapons a build file gives by <c>hash</c>, in its order, read without the rest of the file: what
    /// the designer reads Bungie's full manifest for before it parses the file.
    /// </summary>
    public static Result<ImmutableArray<ItemHash>, string> ListWeaponHashes(SourceText file) =>
        LoadDocument(file, "build")
            .Bind(root => root.ToMap())
            .Bind(map => map.ReadOrDefault("weapons", weapons => weapons.ReadEach("weapon", ReadWeaponHash), []))
            .Map(hashes => hashes.SelectMany(hash => hash.Match(some => new[] { some.Value }, _ => [])).ToImmutableArray())
            .MapError(FormatErrors);

    private static Result<Optional<ItemHash>, Errors> ReadWeaponHash(YamlValue value) =>
        value.ToMap().Bind(map => map.ReadOptional("hash", ReadItemHash));

    private static Result<Build, Errors> ReadBuild(ManifestExcerpt manifest, YamlMap map) =>
        Combine(
            ReadEquipment(manifest, map),
            map.ReadOrDefault("leftOut", ReadLeftOut, []),
            map.ReadOrDefault("armorCopies", copies => copies.ReadEach("armor copy", ReadArmorCopy), []),
            map.ReadOrDefault("armorModPlugs", plugs => plugs.ReadEach("armor mod plug", ReadItemHash), []),
            map.ReadOptional("artifact", ReadArtifact),
            (build, leftOut, armorCopies, armorModPlugs, artifact) => build with { LeftOut = leftOut, ArmorCopies = armorCopies, ArmorModPlugs = armorModPlugs, Artifact = artifact });

    /// <summary>Everything but <c>leftOut</c>, <c>armorCopies</c>, <c>armorModPlugs</c> and <c>artifact</c> (one <c>Combine</c> takes at most 16 inputs).</summary>
    private static Result<Build, Errors> ReadEquipment(ManifestExcerpt manifest, YamlMap map) =>
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
            map.ReadOrDefault("weapons", weapons => weapons.ReadEach("weapon", weapon => ReadWeapon(manifest, weapon)), []),
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
    /// A weapon, given by <c>hash</c> (<see cref="ReadManifestWeapon"/>): one without is refused. <c>roll: ["?",
    /// 243981275]</c> is the perk picked in each trait column, by manifest hash, in column order ("?": not picked); <c>catalyst: true</c> turns an exotic's catalyst on (default
    /// false); <c>instance: "6917…"</c> is the copy it is (a quoted instance id); <c>mod: 3820243349</c> is the weapon mod
    /// in its mod socket, by manifest hash.
    /// </summary>
    private static Result<WeaponLoadout, Errors> ReadWeapon(ManifestExcerpt manifest, YamlValue value) =>
        value.ToMap().Bind(map => map.ReadOptional("hash", ReadItemHash).Bind(hash => hash.Match(
            some => ReadManifestWeapon(manifest, value, map, some.Value),
            _ => RefuseUnhashedWeapon(value, map))));

    /// <summary>
    /// <c>{ hash, perks?, roll?, catalyst?, instance?, mod? }</c>: its slot, name, damage type, ammo type and archetype are
    /// the manifest's. A <c>slot</c>, <c>name</c>, <c>type</c> or <c>archetype</c> the file gives too (a file written before
    /// the manifest gave them) is checked for its shape and otherwise ignored: the manifest wins. A hash the manifest
    /// doesn't know as a weapon with a damage type is refused.
    /// </summary>
    private static Result<WeaponLoadout, Errors> ReadManifestWeapon(ManifestExcerpt manifest, YamlValue value, YamlMap map, ItemHash hash) =>
        Combine(
            map.CheckKeys(WeaponKeys),
            map.ReadOptional("slot", ReadVocabularyWord<WeaponSlot>),
            map.ReadOptional("name", ReadName),
            map.ReadOptional("type", ReadVocabularyWord<DamageType>),
            map.ReadOptional("archetype", ReadSlug),
            map.ReadOrDefault("perks", ReadElementIds, []),
            map.ReadOrDefault("roll", roll => roll.ReadEach(roll.Label, ReadRollPerk), []),
            map.ReadOrDefault("catalyst", ReadBoolean, false),
            map.ReadOptional("instance", instance => instance.ParseWith(text => ItemInstanceId.TryFrom(text).ToResult())),
            map.ReadOptional("mod", ReadItemHash),
            (_, _, _, _, archetype, perks, roll, catalyst, instance, mod) => new WeaponChoices(archetype, perks, roll, catalyst, instance, mod))
            .Bind(read => FindManifestWeapon(manifest, hash).Match(
                found => Succeed(new WeaponLoadout(
                    found.Value.Slot,
                    found.Value.Item.Name,
                    found.Value.Type,
                    found.Value.Ammo,
                    new[] { found.Value.Item.Type.Bind(ManifestReading.ToArchetype), read.Archetype }.FindFirstSome(),
                    read.Perks,
                    Optional.Some(hash),
                    read.Roll,
                    read.Catalyst,
                    read.Instance,
                    read.Mod)),
                _ => value.FailAt<WeaponLoadout>($"weapon hash {hash.Value.ToString(CultureInfo.InvariantCulture)} isn't in the manifest, so the file can't be imported")));

    /// <summary>A weapon of the manifest with its damage type, by its hash.</summary>
    private static Optional<ManifestWeapon> FindManifestWeapon(ManifestExcerpt manifest, ItemHash hash) =>
        manifest.FindItem(hash).Bind(item => item.Kind is ManifestKind.Weapon { DamageType: Optional<DamageType>.Some type } kind
            ? Optional.Some(new ManifestWeapon(item, kind.Slot, type.Value, kind.Ammo))
            : Optional.None<ManifestWeapon>());

    /// <summary>
    /// A weapon without a hash is refused: its slot, damage type, archetype and ammo type are Bungie's manifest's, which only
    /// its hash names (the designer always gives one: a picked weapon, the player's own).
    /// </summary>
    private static Result<WeaponLoadout, Errors> RefuseUnhashedWeapon(YamlValue value, YamlMap map) =>
        Combine(map.CheckKeys(WeaponKeys), map.ReadOptional("name", ReadName), (_, name) => name)
            .Bind(name => value.FailAt<WeaponLoadout>(name.Match(
                some => $"weapon '{some.Value}' needs a `hash`",
                _ => "a weapon needs a `hash`")));

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
    /// hashes of what the build has that the catalog doesn't place (a legendary armor piece, a mod the rules don't know yet), by where the build holds them.
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
