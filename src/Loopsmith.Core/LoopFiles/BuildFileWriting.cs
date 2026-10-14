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

    /// <summary>One weapon entry as the build file writes it, <c>{ slot: kinetic, name: …, roll: ["?", 243981275] }</c> (no line break).</summary>
    internal static string WriteWeapon(WeaponLoadout weapon)
    {
        ImmutableArray<ParsingEvent> events = [new StreamStart(), new DocumentStart(), .. ToWeaponEvents(weapon), new DocumentEnd(true), new StreamEnd()];
        return EmitYaml(events).TrimEnd('\n');
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
        .. ToAbilityEntry("jump", build.Abilities.Jump),
        .. ToListEntry("aspects", build.Aspects),
        .. ToListEntry("fragments", build.Fragments),
        .. build.ExoticArmor.Match(exotic => ToEntry("exoticArmor", exotic.Value.Value), _ => []),
        .. ToListEntry("armorSetBonuses", build.ArmorSetBonuses),
        .. ToListEntry("armorMods", build.ArmorMods),
        .. build.Artifact.Match(artifact => (ImmutableArray<ParsingEvent>)[ToKey("artifact"), .. ToArtifactValue(artifact.Value)], _ => []),
        .. ToListEntry("artifactPerks", build.ArtifactPerks),
        .. ToWeaponsEntry(build.Weapons),
        .. ToStatsEntry(build.Stats),
        .. ToLeftOutEntry(build.LeftOut),
        .. ToArmorCopiesEntry(build.ArmorCopies),
        .. ToHashListEntry("armorModPlugs", build.ArmorModPlugs),
        new MappingEnd(),
        new DocumentEnd(true),
        new StreamEnd(),
    ];

    private static ImmutableArray<ParsingEvent> ToEntry(string key, string value) =>
        [ToKey(key), new Scalar(null, null, value, ScalarStyle.Any, true, true)];

    /// <summary>An ability's id, or <c>"?"</c> (quoted, as the format writes it) when it isn't known; no entry for an empty slot.</summary>
    private static ImmutableArray<ParsingEvent> ToAbilityEntry(string key, AbilityPlug ability) =>
        ability.Match(
            known => ToEntry(key, known.Element.Value),
            _ => [ToKey(key), new Scalar(null, null, DomainPhrasing.Unknown, ScalarStyle.DoubleQuoted, false, true)],
            _ => []);

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
        .. ToRollEntry(weapon.Roll),
        .. weapon.Catalyst ? ToEntry("catalyst", "true") : [],
        .. weapon.Instance.Match(instance => (ImmutableArray<ParsingEvent>)[ToKey("instance"), new Scalar(null, null, instance.Value.Value, ScalarStyle.DoubleQuoted, false, true)], _ => []),
        .. weapon.Mod.Match(mod => ToEntry("mod", mod.Value.Value.ToString(CultureInfo.InvariantCulture)), _ => []),
        new MappingEnd(),
    ];

    /// <summary><c>{hash: 3821685388, slots: [3585856470, "?", …]}</c>: the artifact and the perk in each slot, <c>"?"</c> for an empty one.</summary>
    private static ImmutableArray<ParsingEvent> ToArtifactValue(ArtifactLoadout artifact) =>
    [
        new MappingStart(null, null, true, MappingStyle.Flow),
        ToKey("hash"),
        ToValue(artifact.Hash.Value.ToString(CultureInfo.InvariantCulture)),
        ToKey("slots"),
        .. ToPicksValue(artifact.Slots),
        new MappingEnd(),
    ];

    /// <summary><c>["?", 243981275]</c>: picks by place, <c>"?"</c> for none.</summary>
    private static ImmutableArray<ParsingEvent> ToPicksValue(ImmutableArray<Optional<ItemHash>> picks) =>
    [
        new SequenceStart(null, null, true, SequenceStyle.Flow),
        .. picks.Select(pick => pick.Match(
            hash => (ParsingEvent)ToValue(hash.Value.Value.ToString(CultureInfo.InvariantCulture)),
            _ => new Scalar(null, null, DomainPhrasing.Unknown, ScalarStyle.DoubleQuoted, false, true))),
        new SequenceEnd(),
    ];

    /// <summary><c>roll: ["?", 243981275]</c>: each trait column's pick in column order, <c>"?"</c> for none; nothing when empty.</summary>
    private static ImmutableArray<ParsingEvent> ToRollEntry(ImmutableArray<Optional<ItemHash>> roll) =>
        roll.IsEmpty
            ? []
            :
            [ToKey("roll"), .. ToPicksValue(roll)];

    /// <summary>A build's <c>stats:</c> value on its own, <c>{weapons: 47, class: 104}</c> (<c>{}</c> when none is known).</summary>
    internal static string WriteStats(StatLine stats)
    {
        ImmutableArray<ParsingEvent> events =
            [new StreamStart(), new DocumentStart(), new MappingStart(null, null, true, MappingStyle.Flow), .. ToStatEntries(stats), new MappingEnd(), new DocumentEnd(true), new StreamEnd()];
        return EmitYaml(events).TrimEnd('\n');
    }

    private static ImmutableArray<ParsingEvent> ToStatsEntry(StatLine stats)
    {
        var known = ToStatEntries(stats);
        return known.IsEmpty
            ? []
            : [ToKey("stats"), new MappingStart(null, null, true, MappingStyle.Flow), .. known, new MappingEnd()];
    }

    /// <summary>The known stats as key and value events, in the game's order (health first); the unknown ("?") ones left out.</summary>
    private static ImmutableArray<ParsingEvent> ToStatEntries(StatLine stats) =>
        [
            .. StatReading.GameOrder.SelectMany(stat => stats.ReadStat(stat).Match(
                value => ToEntry(ToVocabularyWord(stat), value.Value.Value.ToString(CultureInfo.InvariantCulture)),
                _ => [])),
        ];

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

    /// <summary><c>armorModPlugs: [3847471926]</c>; nothing when empty.</summary>
    private static ImmutableArray<ParsingEvent> ToHashListEntry(string key, ImmutableArray<ItemHash> hashes) =>
        hashes.IsEmpty ? [] : [ToKey(key), .. ToHashListValue(hashes)];

    private static ImmutableArray<ParsingEvent> ToHashListValue(ImmutableArray<ItemHash> hashes) =>
    [
        new SequenceStart(null, null, true, SequenceStyle.Flow),
        .. hashes.Select(hash => (ParsingEvent)ToValue(hash.Value.ToString(CultureInfo.InvariantCulture))),
        new SequenceEnd(),
    ];

    /// <summary>
    /// The build's mod entries as the build file writes them — <c>armorMods</c> (always, <c>[]</c> when none),
    /// <c>leftOut</c> (its mods with the rest of what it leaves out) and <c>armorModPlugs</c> — an entry with none is
    /// removed by an edit.
    /// </summary>
    internal static ImmutableArray<(string Key, Optional<string> Value)> WriteModEntries(Build build) =>
    [
        ("armorMods", Optional.Some(WriteValue([.. ToListEntry("armorMods", build.ArmorMods).Skip(1)]))),
        ("leftOut", ToLeftOutEntry(build.LeftOut) is { IsEmpty: false } leftOut ? Optional.Some(WriteValue([.. leftOut.Skip(1)])) : Optional.None<string>()),
        ("armorModPlugs", build.ArmorModPlugs.IsEmpty ? Optional.None<string>() : Optional.Some(WriteValue(ToHashListValue(build.ArmorModPlugs)))),
    ];

    /// <summary><c>armorCopies: [{hash: 593554567, instance: "6917530185805135602"}, …]</c>; nothing when the build says none.</summary>
    private static ImmutableArray<ParsingEvent> ToArmorCopiesEntry(ImmutableArray<ArmorCopy> copies) =>
        copies.IsEmpty ? [] : [ToKey("armorCopies"), .. ToArmorCopiesValue(copies)];

    private static ImmutableArray<ParsingEvent> ToArmorCopiesValue(ImmutableArray<ArmorCopy> copies) =>
    [
        new SequenceStart(null, null, true, SequenceStyle.Flow),
        .. copies.SelectMany(copy => (ImmutableArray<ParsingEvent>)
        [
            new MappingStart(null, null, true, MappingStyle.Flow),
            ToKey("hash"),
            ToValue(copy.Piece.Value.ToString(CultureInfo.InvariantCulture)),
            ToKey("instance"),
            new Scalar(null, null, copy.Instance.Value, ScalarStyle.DoubleQuoted, false, true),
            new MappingEnd(),
        ]),
        new SequenceEnd(),
    ];

    /// <summary>
    /// The build's armor entries as the build file writes them — <c>exoticArmor</c>, <c>armorSetBonuses</c>,
    /// <c>leftOut</c>, <c>armorCopies</c> — each key with its value's text, or none when the build has nothing for it
    /// (so an edit removes the entry).
    /// </summary>
    internal static ImmutableArray<(string Key, Optional<string> Value)> WriteArmorEntries(Build build) =>
    [
        ("exoticArmor", build.ExoticArmor.Map(exotic => exotic.Value)),
        ("armorSetBonuses", build.ArmorSetBonuses.IsEmpty ? Optional.None<string>() : Optional.Some(WriteValue([.. ToListEntry("armorSetBonuses", build.ArmorSetBonuses).Skip(1)]))),
        ("leftOut", ToLeftOutEntry(build.LeftOut) is { IsEmpty: false } leftOut ? Optional.Some(WriteValue([.. leftOut.Skip(1)])) : Optional.None<string>()),
        ("armorCopies", build.ArmorCopies.IsEmpty ? Optional.None<string>() : Optional.Some(WriteValue(ToArmorCopiesValue(build.ArmorCopies)))),
    ];

    /// <summary>
    /// The build's subclass entries as the build file writes them — <c>super</c>, <c>grenade</c>, <c>melee</c>,
    /// <c>classAbility</c>, <c>jump</c> (<c>"?"</c> when unknown; none when the slot is empty), <c>aspects</c>, <c>fragments</c>,
    /// <c>leftOut</c> (none when the build leaves nothing out) — an entry with none is removed by an edit.
    /// </summary>
    internal static ImmutableArray<(string Key, Optional<string> Value)> WriteSubclassEntries(Build build) =>
    [
        .. new[] { ("super", build.Abilities.Super), ("grenade", build.Abilities.Grenade), ("melee", build.Abilities.Melee), ("classAbility", build.Abilities.ClassAbility), ("jump", build.Abilities.Jump) }
            .Select(ability => (ability.Item1, ToAbilityEntry(ability.Item1, ability.Item2) is { IsEmpty: false } entry ? Optional.Some(WriteValue([.. entry.Skip(1)])) : Optional.None<string>())),
        ("aspects", Optional.Some(WriteValue([.. ToListEntry("aspects", build.Aspects).Skip(1)]))),
        ("fragments", Optional.Some(WriteValue([.. ToListEntry("fragments", build.Fragments).Skip(1)]))),
        ("leftOut", ToLeftOutEntry(build.LeftOut) is { IsEmpty: false } leftOut ? Optional.Some(WriteValue([.. leftOut.Skip(1)])) : Optional.None<string>()),
    ];

    /// <summary>
    /// The build's artifact entries as the build file writes them — <c>artifact</c> (its hash and slots), <c>artifactPerks</c> and
    /// <c>leftOut</c> (its artifact perks the rules don't know, with the rest of what it leaves out) — an entry with none
    /// is removed by an edit.
    /// </summary>
    internal static ImmutableArray<(string Key, Optional<string> Value)> WriteArtifactEntries(Build build) =>
    [
        ("artifact", build.Artifact.Map(artifact => WriteValue(ToArtifactValue(artifact)))),
        ("artifactPerks", build.ArtifactPerks.IsEmpty ? Optional.None<string>() : Optional.Some(WriteValue([.. ToListEntry("artifactPerks", build.ArtifactPerks).Skip(1)]))),
        ("leftOut", ToLeftOutEntry(build.LeftOut) is { IsEmpty: false } leftOut ? Optional.Some(WriteValue([.. leftOut.Skip(1)])) : Optional.None<string>()),
    ];

    /// <summary>One value's events (a flow list or mapping) as text, on one line.</summary>
    private static string WriteValue(ImmutableArray<ParsingEvent> value)
    {
        ImmutableArray<ParsingEvent> events = [new StreamStart(), new DocumentStart(), .. value, new DocumentEnd(true), new StreamEnd()];
        return EmitYaml(events).TrimEnd('\n');
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
