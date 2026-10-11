using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// <c>rules/manifest.yaml</c>: Loopsmith's excerpt of the Bungie manifest, written by
/// <c>tools/manifest/extract_manifest.py</c> (docs/rule-format.md, "Manifest").
/// </summary>
internal static class ManifestParsing
{
    internal const string ManifestPath = "manifest.yaml";

    internal static readonly ManifestExcerpt NoManifest = new(
        Optional.None<string>(),
        ImmutableDictionary<DamageType, string>.Empty,
        ImmutableDictionary<ItemHash, ArmorSet>.Empty,
        ImmutableDictionary<ItemHash, ManifestItem>.Empty,
        ImmutableDictionary<ItemHash, StatPlug>.Empty);

    private static readonly ImmutableArray<string> ManifestKeys = ["version", "damageTypes", "armorSets", "items", "statPlugs"];
    private static readonly ImmutableArray<string> StatPlugKeys = ["hash", "kind", "stats", "onlyEmptyStats"];
    private static readonly ImmutableArray<string> PlugStatKeys = ["stat", "value", "class"];
    private static readonly ImmutableArray<string> DamageTypeKeys = ["type", "icon"];
    private static readonly ImmutableArray<string> ArmorSetKeys = ["hash", "name", "perks"];
    private static readonly ImmutableArray<string> SetPerkKeys = ["pieces", "perk"];
    private static readonly ImmutableArray<string> ItemKeys =
        ["hash", "name", "kind", "type", "tier", "slot", "damageType", "traits", "intrinsic", "catalyst", "set", "perks", "description", "icon"];

    /// <summary>The keys only some kinds have, each with the kinds it belongs to (and how an error names them).</summary>
    private static readonly ImmutableArray<(string Key, ImmutableArray<KindWord> Kinds, string Where)> KindKeys =
    [
        ("slot", [KindWord.Weapon, KindWord.Armor, KindWord.ArmorMod], "a weapon, armor or an armor mod"),
        ("damageType", [KindWord.Weapon], "a weapon"),
        ("traits", [KindWord.Weapon], "a weapon"),
        ("catalyst", [KindWord.Weapon], "a weapon"),
        ("intrinsic", [KindWord.Weapon, KindWord.Armor], "a weapon or armor"),
        ("set", [KindWord.Armor], "armor"),
        ("perks", [KindWord.Catalyst, KindWord.Artifact], "a catalyst or an artifact"),
    ];

    internal static Result<ManifestExcerpt, Errors> ReadManifest(SourceText file) =>
        YamlReading.LoadDocument(file, "manifest")
            .Bind(root => root.ToMap())
            .Bind(map => Combine(
                map.CheckKeys(ManifestKeys),
                map.ReadOptional("version", ReadName),
                map.ReadOrDefault("damageTypes", types => types.ReadEach("damage type", ReadDamageTypeIcon), []),
                map.ReadOrDefault("armorSets", sets => sets.ReadEach("armor set", ReadArmorSet)
                    .Bind(located => LocatedCollections.ToUniqueDictionary(located, set => set.Hash, "armor set")),
                    ImmutableDictionary<ItemHash, ArmorSet>.Empty),
                map.ReadOrDefault("items", items => items.ReadEach("item", ReadItem)
                    .Bind(located => LocatedCollections.ToUniqueDictionary(located, item => item.Hash, "manifest item")),
                    ImmutableDictionary<ItemHash, ManifestItem>.Empty),
                map.ReadOrDefault("statPlugs", plugs => plugs.ReadEach("stat plug", ReadStatPlug)
                    .Bind(located => LocatedCollections.ToUniqueDictionary(located, plug => plug.Hash, "stat plug")),
                    ImmutableDictionary<ItemHash, HashedStatPlug>.Empty),
                (_, version, icons, sets, items, statPlugs) => new ManifestExcerpt(
                    version,
                    icons.GroupBy(icon => icon.Type).ToImmutableDictionary(group => group.Key, group => group.First().Icon),
                    sets,
                    items,
                    statPlugs.ToImmutableDictionary(entry => entry.Key, entry => entry.Value.Plug))));

    /// <summary>A stat plug with its hash, while the excerpt's list becomes a dictionary.</summary>
    private sealed record HashedStatPlug(ItemHash Hash, StatPlug Plug);

    /// <summary><c>{ hash, kind: base|masterwork|tuning|mod|subclass, stats: [{ stat, value, class? }], onlyEmptyStats? }</c>.</summary>
    private static Result<Located<HashedStatPlug>, Errors> ReadStatPlug(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StatPlugKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("kind", ReadVocabularyWord<StatPlugKind>),
            map.ReadOrDefault("stats", stats => stats.ReadEach("plug stat", ReadPlugStat), []),
            map.ReadOrDefault("onlyEmptyStats", ReadBoolean, false),
            (_, hash, kind, stats, onlyEmpty) => map.ToLocated(new HashedStatPlug(hash, new StatPlug(kind, stats, onlyEmpty)))));

    private static Result<PlugStat, Errors> ReadPlugStat(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PlugStatKeys),
            map.ReadRequired("stat", ReadVocabularyWord<StatKind>),
            map.ReadRequired("value", ReadSignedInteger),
            map.ReadOptional("class", ReadVocabularyWord<GuardianClass>),
            (_, stat, points, forClass) => new PlugStat(stat, points, forClass)));

    private sealed record DamageTypeIcon(DamageType Type, string Icon);

    private static Result<DamageTypeIcon, Errors> ReadDamageTypeIcon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(DamageTypeKeys),
            map.ReadRequired("type", ReadVocabularyWord<DamageType>),
            map.ReadRequired("icon", ReadName),
            (_, type, icon) => new DamageTypeIcon(type, icon)));

    /// <summary><c>{ hash, name, perks: [{ pieces: 2, perk: 2769443639 }, …] }</c>: the perks in order of their piece count.</summary>
    private static Result<Located<ArmorSet>, Errors> ReadArmorSet(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(ArmorSetKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("name", ReadName),
            map.ReadOrDefault("perks", perks => perks.ReadEach("set perk", ReadSetPerk), []),
            (_, hash, name, perks) => map.ToLocated(new ArmorSet(hash, name, [.. perks.OrderBy(perk => perk.Pieces)]))));

    private static Result<SetPerk, Errors> ReadSetPerk(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(SetPerkKeys),
            map.ReadRequired("pieces", ReadPieceCount),
            map.ReadRequired("perk", ReadItemHash),
            (_, pieces, perk) => new SetPerk(pieces, perk)));

    private static Result<PieceCount, Errors> ReadPieceCount(YamlValue value) =>
        ReadPositiveInteger(value).Bind(pieces => PieceCount.TryFrom(pieces).ToResult().MapError(message => value.ToErrors(message)));

    /// <summary>The <c>kind:</c> words of the excerpt; each becomes a <see cref="ManifestKind"/> case with its own data.</summary>
    private enum KindWord
    {
        Subclass, Super, Grenade, Melee, ClassAbility, Movement, Aspect, Fragment, Weapon, WeaponPerk, Intrinsic, Catalyst, Armor,
        ArmorMod, ArtifactPerk, Artifact, Perk, Other,
    }

    /// <summary>
    /// <c>{ hash, name, kind, type?, tier?, description?, icon?, … }</c>. A weapon needs its <c>slot</c> (a weapon slot) and
    /// may have a <c>damageType</c>, <c>traits</c> (its trait columns, each a list of the perk hashes it rolls with:
    /// <c>[[a, b], [c]]</c>), an <c>intrinsic</c> and a <c>catalyst</c> (hashes); armor needs its <c>slot</c> (an armor
    /// slot) and may have an <c>intrinsic</c> and a <c>set</c> (an armor set's hash); an armor mod may have a slot (none: a
    /// general mod); a catalyst has the <c>perks</c> it grants, an artifact the perks it unlocks. Anywhere else these keys
    /// are errors.
    /// </summary>
    private static Result<Located<ManifestItem>, Errors> ReadItem(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(ItemKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("kind", ReadVocabularyWord<KindWord>).Bind(word => CheckKindKeys(map, word).Bind(_ => ReadKind(map, word))),
            map.ReadOptional("type", ReadName),
            map.ReadOptional("tier", ReadVocabularyWord<ItemTier>),
            map.ReadOptional("description", ReadName),
            map.ReadOptional("icon", ReadName),
            (_, hash, name, kind, type, tier, description, icon) => map.ToLocated(new ManifestItem(hash, name, kind, type, tier, description, icon))));

    private static Result<ManifestKind, Errors> ReadKind(YamlMap map, KindWord word) =>
        word switch
        {
            KindWord.Weapon => Combine(
                map.ReadRequired("slot", ReadVocabularyWord<WeaponSlot>),
                map.ReadOptional("damageType", ReadVocabularyWord<DamageType>),
                map.ReadOrDefault("traits", columns => columns.ReadEach("trait column", ReadTraitColumn), []),
                map.ReadOptional("intrinsic", ReadItemHash),
                map.ReadOptional("catalyst", ReadItemHash),
                (slot, damageType, traits, intrinsic, catalyst) => (ManifestKind)new ManifestKind.Weapon(slot, damageType, traits, intrinsic, catalyst)),
            KindWord.Armor => Combine(
                map.ReadRequired("slot", ReadVocabularyWord<ArmorSlot>),
                map.ReadOptional("intrinsic", ReadItemHash),
                map.ReadOptional("set", ReadItemHash),
                (slot, intrinsic, set) => (ManifestKind)new ManifestKind.Armor(slot, intrinsic, set)),
            KindWord.ArmorMod => map.ReadOptional("slot", ReadVocabularyWord<ArmorSlot>).Map(slot => (ManifestKind)new ManifestKind.ArmorMod(slot)),
            KindWord.Catalyst => map.ReadOrDefault("perks", perks => perks.ReadEach("perk", ReadItemHash), [])
                .Map(perks => (ManifestKind)new ManifestKind.ExoticCatalyst(perks)),
            KindWord.Artifact => map.ReadOrDefault("perks", perks => perks.ReadEach("perk", ReadItemHash), [])
                .Map(perks => (ManifestKind)new ManifestKind.Artifact(perks)),
            _ => Succeed(ToPlainKind(word)),
        };

    /// <summary>A trait column: the hashes of the perks it rolls with; at least one.</summary>
    private static Result<TraitColumn, Errors> ReadTraitColumn(YamlValue value) =>
        value.ReadEach("perk", ReadItemHash).Bind(options => options.IsEmpty
            ? value.FailAt<TraitColumn>("a trait column needs at least one perk hash")
            : Succeed(new TraitColumn(options)));

    private static ManifestKind ToPlainKind(KindWord word) =>
        word switch
        {
            KindWord.Subclass => new ManifestKind.Subclass(),
            KindWord.Super => new ManifestKind.Super(),
            KindWord.Grenade => new ManifestKind.Grenade(),
            KindWord.Melee => new ManifestKind.Melee(),
            KindWord.ClassAbility => new ManifestKind.ClassAbility(),
            KindWord.Movement => new ManifestKind.Movement(),
            KindWord.Aspect => new ManifestKind.Aspect(),
            KindWord.Fragment => new ManifestKind.Fragment(),
            KindWord.WeaponPerk => new ManifestKind.WeaponPerk(),
            KindWord.Intrinsic => new ManifestKind.IntrinsicPerk(),
            KindWord.ArtifactPerk => new ManifestKind.ArtifactPerk(),
            KindWord.Perk => new ManifestKind.Perk(),
            _ => new ManifestKind.Other(),
        };

    /// <summary>The first key the item has that its kind doesn't (<c>slot is only allowed on a weapon, armor or an armor mod, not on a grenade</c>).</summary>
    private static Result<Unit, Errors> CheckKindKeys(YamlMap map, KindWord word) =>
        KindKeys.Where(entry => !entry.Kinds.Contains(word) && map.FindValue(entry.Key).IsSome()).ToImmutableArray() switch
        {
            [] => Succeed<Unit>(new Unit.Value()),
            [var entry, ..] => map.FailAtKey<Unit>(entry.Key, $"{entry.Key} is only allowed on {entry.Where}, not on a {GameNotationParsing.ToVocabularyWord(word)}"),
        };
}
