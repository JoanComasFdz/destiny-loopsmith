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

    /// <summary>The <c>kind:</c> words of the excerpt; each becomes a <see cref="ManifestKind"/> case with its own data.</summary>
    private enum KindWord
    {
        Subclass, Super, Grenade, Melee, ClassAbility, Movement, Aspect, Fragment, Weapon, WeaponPerk, Armor, ArmorMod, ArtifactPerk, Other,
    }

    /// <summary>
    /// <c>{ hash, name, kind, type, tier?, slot?, damageType?, icon? }</c>. A weapon needs its <c>slot</c> (a weapon slot) and
    /// may have a <c>damageType</c>; armor needs its <c>slot</c> (an armor slot); an armor mod may have one (none: a general
    /// mod). Anywhere else both are errors.
    /// </summary>
    private static Result<Located<ManifestItem>, Errors> ReadItem(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(ItemKeys),
            map.ReadRequired("hash", ReadItemHash),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("kind", ReadVocabularyWord<KindWord>).Bind(word => ReadKind(map, word)),
            map.ReadRequired("type", ReadName),
            map.ReadOptional("tier", ReadVocabularyWord<ItemTier>),
            map.ReadOptional("icon", ReadName),
            (_, hash, name, kind, type, tier, icon) => map.ToLocated(new ManifestItem(hash, name, kind, type, tier, icon))));

    private static Result<ManifestKind, Errors> ReadKind(YamlMap map, KindWord word) =>
        word switch
        {
            KindWord.Weapon => Combine(
                map.ReadRequired("slot", ReadVocabularyWord<WeaponSlot>),
                map.ReadOptional("damageType", ReadVocabularyWord<DamageType>),
                (slot, damageType) => (ManifestKind)new ManifestKind.Weapon(slot, damageType)),
            KindWord.Armor => CheckNoDamageType(map, word).Bind(_ => map.ReadRequired("slot", ReadVocabularyWord<ArmorSlot>)
                .Map(slot => (ManifestKind)new ManifestKind.Armor(slot))),
            KindWord.ArmorMod => CheckNoDamageType(map, word).Bind(_ => map.ReadOptional("slot", ReadVocabularyWord<ArmorSlot>)
                .Map(slot => (ManifestKind)new ManifestKind.ArmorMod(slot))),
            _ => CheckNoDamageType(map, word).Bind(_ => CheckNoSlot(map, word)).Map(_ => ToSlotlessKind(word)),
        };

    private static ManifestKind ToSlotlessKind(KindWord word) =>
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
            KindWord.ArtifactPerk => new ManifestKind.ArtifactPerk(),
            _ => new ManifestKind.Other(),
        };

    private static Result<Unit, Errors> CheckNoSlot(YamlMap map, KindWord word) =>
        map.FindValue("slot").IsSome()
            ? map.FailAtKey<Unit>("slot", $"slot is only allowed on a weapon, armor or an armor mod, not on a {GameNotationParsing.ToVocabularyWord(word)}")
            : Succeed<Unit>(new Unit.Value());

    private static Result<Unit, Errors> CheckNoDamageType(YamlMap map, KindWord word) =>
        map.FindValue("damageType").IsSome()
            ? map.FailAtKey<Unit>("damageType", $"damageType is only allowed on a weapon, not on a {GameNotationParsing.ToVocabularyWord(word)}")
            : Succeed<Unit>(new Unit.Value());
}
