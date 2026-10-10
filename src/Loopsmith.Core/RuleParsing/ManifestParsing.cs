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
        ImmutableDictionary<ItemHash, ManifestItem>.Empty);

    private static readonly ImmutableArray<string> ManifestKeys = ["version", "damageTypes", "items"];
    private static readonly ImmutableArray<string> DamageTypeKeys = ["type", "icon"];
    private static readonly ImmutableArray<string> ItemKeys = ["hash", "name", "kind", "type", "tier", "slot", "damageType", "icon"];

    internal static Result<ManifestExcerpt, Errors> ReadManifest(SourceText file) =>
        YamlReading.LoadDocument(file, "manifest")
            .Bind(root => root.ToMap())
            .Bind(map => Combine(
                map.CheckKeys(ManifestKeys),
                map.ReadOptional("version", ReadName),
                map.ReadOrDefault("damageTypes", types => types.ReadEach("damage type", ReadDamageTypeIcon), []),
                map.ReadOrDefault("items", items => items.ReadEach("item", ReadItem)
                    .Bind(located => LocatedCollections.ToUniqueDictionary(located, item => item.Hash, "manifest item")),
                    ImmutableDictionary<ItemHash, ManifestItem>.Empty),
                (_, version, icons, items) => new ManifestExcerpt(
                    version,
                    icons.GroupBy(icon => icon.Type).ToImmutableDictionary(group => group.Key, group => group.First().Icon),
                    items)));

    private sealed record DamageTypeIcon(DamageType Type, string Icon);

    private static Result<DamageTypeIcon, Errors> ReadDamageTypeIcon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(DamageTypeKeys),
            map.ReadRequired("type", ReadVocabularyWord<DamageType>),
            map.ReadRequired("icon", ReadName),
            (_, type, icon) => new DamageTypeIcon(type, icon)));

    /// <summary>
    /// <c>{ hash, name, kind, type, tier?, slot?, damageType?, icon? }</c>. <c>slot</c> is a weapon slot on a weapon and an
    /// armor slot on armor and an armor mod; anywhere else it is an error.
    /// </summary>
    private static Result<Located<ManifestItem>, Errors> ReadItem(YamlValue value) =>
        value.ToMap().Bind(map => map.ReadRequired("kind", ReadVocabularyWord<ManifestKind>).Bind(kind => Combine(
            map.CheckKeys(ItemKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("type", ReadName),
            map.ReadOptional("tier", ReadVocabularyWord<ItemTier>),
            kind == ManifestKind.Weapon ? map.ReadOptional("slot", ReadVocabularyWord<WeaponSlot>) : Succeed(Optional.None<WeaponSlot>()),
            kind is ManifestKind.Armor or ManifestKind.ArmorMod ? map.ReadOptional("slot", ReadVocabularyWord<ArmorSlot>) : CheckNoSlot(map, kind),
            map.ReadOptional("damageType", ReadVocabularyWord<DamageType>),
            map.ReadOptional("icon", ReadName),
            (_, hash, name, type, tier, weaponSlot, armorSlot, damageType, icon) =>
                map.ToLocated(new ManifestItem(hash, name, kind, type, tier, weaponSlot, armorSlot, damageType, icon)))));

    private static Result<Optional<ArmorSlot>, Errors> CheckNoSlot(YamlMap map, ManifestKind kind) =>
        map.FindValue("slot").IsSome() && kind != ManifestKind.Weapon
            ? map.FailAtKey<Optional<ArmorSlot>>("slot", $"slot is only allowed on a weapon, armor or an armor mod, not on a {GameNotationParsing.ToVocabularyWord(kind)}")
            : Succeed(Optional.None<ArmorSlot>());
}
