using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Phrasing;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Loopsmith.Core.LoopFiles;

/// <summary>
/// <see cref="Build"/> → <c>build.yaml</c> text (docs/rule-format.md), for a build composed elsewhere than in a file (a
/// DIM link): the loop file embeds it like any other build. Pure. Written through YamlDotNet's emitter, so it reads back
/// as the same build. Each note becomes a comment line at the top (what a DIM link had that the build leaves out).
/// </summary>
public static class BuildFileWriting
{
    private static readonly EmitterSettings Settings = new(
        bestIndent: 2,
        bestWidth: int.MaxValue,
        isCanonical: false,
        maxSimpleKeyLength: 1024,
        skipAnchorName: false,
        indentSequences: true,
        newLine: "\n",
        useUtf16SurrogatePairs: false);

    public static string WriteBuildFile(Build build, ImmutableArray<string> notes)
    {
        var events = ListEvents(build, notes);
        return EmitYaml(events);
    }

    /// <summary>Keys in the order of the format: identity, class and subclass, abilities, the lists, weapons, stats.</summary>
    private static ImmutableArray<ParsingEvent> ListEvents(Build build, ImmutableArray<string> notes) =>
    [
        new StreamStart(),
        new DocumentStart(),
        .. notes.SelectMany(note => note.Split('\n')).Select(line => new Comment(line, false)),
        new MappingStart(null, null, true, MappingStyle.Block),
        .. ToEntry("name", build.Name),
        .. build.Author.Match(author => ToEntry("author", author.Value), _ => []),
        .. build.SourceUrl.Match(url => ToEntry("source", url.Value), _ => []),
        .. build.PinnedCatalog.Match(catalog => ToEntry("catalog", catalog.Value.Value), _ => []),
        .. ToEntry("class", ToVocabularyWord(build.Class)),
        .. ToEntry("subclass", ToVocabularyWord(build.Subclass)),
        .. ToAbilityEntry("super", build.Abilities.Super),
        .. ToAbilityEntry("grenade", build.Abilities.Grenade),
        .. ToAbilityEntry("melee", build.Abilities.Melee),
        .. ToAbilityEntry("classAbility", build.Abilities.ClassAbility),
        .. ToListEntry("aspects", build.Aspects),
        .. ToListEntry("fragments", build.Fragments),
        .. build.ExoticArmor.Match(exotic => ToEntry("exoticArmor", exotic.Value.Value), _ => []),
        .. ToListEntry("armorSetBonuses", build.ArmorSetBonuses),
        .. ToListEntry("armorMods", build.ArmorMods),
        .. ToListEntry("artifactPerks", build.ArtifactPerks),
        .. ToWeaponsEntry(build.Weapons),
        .. ToStatsEntry(build.Stats),
        .. ToLeftOutEntry(build.LeftOut),
        new MappingEnd(),
        new DocumentEnd(true),
        new StreamEnd(),
    ];

    private static ImmutableArray<ParsingEvent> ToEntry(string key, string value) =>
        [ToKey(key), new Scalar(null, null, value, ScalarStyle.Any, true, true)];

    /// <summary>An ability's id, or <c>"?"</c> (quoted, as the format writes it) when it isn't known.</summary>
    private static ImmutableArray<ParsingEvent> ToAbilityEntry(string key, Optional<ElementId> ability) =>
        ability.Match(
            known => ToEntry(key, known.Value.Value),
            _ => [ToKey(key), new Scalar(null, null, DomainPhrasing.Unknown, ScalarStyle.DoubleQuoted, false, true)]);

    private static ImmutableArray<ParsingEvent> ToListEntry(string key, ImmutableArray<ElementId> ids) =>
    [
        ToKey(key),
        new SequenceStart(null, null, true, SequenceStyle.Flow),
        .. ids.Select(id => (ParsingEvent)ToValue(id.Value)),
        new SequenceEnd(),
    ];

    private static ImmutableArray<ParsingEvent> ToWeaponsEntry(ImmutableArray<WeaponLoadout> weapons) =>
        weapons.IsEmpty
            ? []
            :
            [
                ToKey("weapons"),
                new SequenceStart(null, null, true, SequenceStyle.Block),
                .. weapons.SelectMany(weapon => ToWeaponEvents(weapon)),
                new SequenceEnd(),
            ];

    private static ImmutableArray<ParsingEvent> ToWeaponEvents(WeaponLoadout weapon) =>
    [
        new MappingStart(null, null, true, MappingStyle.Flow),
        .. ToEntry("slot", ToVocabularyWord(weapon.Slot)),
        .. ToEntry("name", weapon.Name),
        .. ToEntry("type", ToVocabularyWord(weapon.Type)),
        .. weapon.Archetype.Match(archetype => ToEntry("archetype", archetype.Value), _ => []),
        .. ToListEntry("perks", weapon.Perks),
        .. weapon.Hash.Match(hash => ToEntry("hash", hash.Value.Value.ToString(CultureInfo.InvariantCulture)), _ => []),
        new MappingEnd(),
    ];

    private static ImmutableArray<ParsingEvent> ToStatsEntry(StatLine stats)
    {
        var known = new (string Key, Optional<StatValue> Value)[]
            {
                ("weapons", stats.Weapons), ("health", stats.Health), ("class", stats.Class),
                ("grenade", stats.Grenade), ("super", stats.Super), ("melee", stats.Melee),
            }
            .SelectMany(stat => stat.Value.Match(
                value => ToEntry(stat.Key, value.Value.Value.ToString(CultureInfo.InvariantCulture)),
                _ => []))
            .ToImmutableArray();
        return known.IsEmpty
            ? []
            : [ToKey("stats"), new MappingStart(null, null, true, MappingStyle.Flow), .. known, new MappingEnd()];
    }

    /// <summary><c>leftOut: { items: […], subclassPlugs: […], … }</c>, each part in the loadout's order; nothing when empty.</summary>
    private static ImmutableArray<ParsingEvent> ToLeftOutEntry(ImmutableArray<LeftOutItem> leftOut)
    {
        var parts = new (string Key, LoadoutPart Part)[]
            {
                ("items", LoadoutPart.Item), ("subclassPlugs", LoadoutPart.SubclassPlug),
                ("armorMods", LoadoutPart.ArmorMod), ("artifactPerks", LoadoutPart.ArtifactPerk),
            }
            .Select(part => (part.Key, Hashes: leftOut.Where(item => item.Part == part.Part).Select(item => item.Hash).ToImmutableArray()))
            .Where(part => !part.Hashes.IsEmpty)
            .SelectMany(part => (ImmutableArray<ParsingEvent>)
            [
                ToKey(part.Key),
                new SequenceStart(null, null, true, SequenceStyle.Flow),
                .. part.Hashes.Select(hash => (ParsingEvent)ToValue(hash.Value.ToString(CultureInfo.InvariantCulture))),
                new SequenceEnd(),
            ])
            .ToImmutableArray();
        return parts.IsEmpty
            ? []
            : [ToKey("leftOut"), new MappingStart(null, null, true, MappingStyle.Flow), .. parts, new MappingEnd()];
    }

    private static Scalar ToKey(string key) => new(null, null, key, ScalarStyle.Plain, true, false);

    private static Scalar ToValue(string value) => new(null, null, value, ScalarStyle.Any, true, true);

    /// <summary>A closed vocabulary word: the camelCase name of an enum member (<c>classAbility</c>).</summary>
    private static string ToVocabularyWord<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        return string.Concat(name[..1].ToLowerInvariant(), name[1..]);
    }

    private static string EmitYaml(ImmutableArray<ParsingEvent> events)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var emitter = new Emitter(writer, Settings);
        foreach (var parsingEvent in events)
        {
            emitter.Emit(parsingEvent);
        }

        return writer.ToString();
    }
}
