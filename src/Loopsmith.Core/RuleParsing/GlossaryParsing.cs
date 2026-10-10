using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary><c>rules/glossary.yaml</c>: the keyword vocabulary (statuses, pickups, summons) and the subclass items.</summary>
internal static class GlossaryParsing
{
    internal const string GlossaryPath = "glossary.yaml";

    private static readonly ImmutableArray<string> GlossaryKeys = ["statuses", "pickups", "summons", "subclasses"];
    private static readonly ImmutableArray<string> StatusKeys = ["id", "name", "kind", "affinity", "maxStacks", "duration"];
    private static readonly ImmutableArray<string> PickupKeys = ["id", "name", "affinity", "collectsAutomatically"];
    private static readonly ImmutableArray<string> SummonKeys = ["id", "name", "damageType"];
    private static readonly ImmutableArray<string> SubclassKeys = ["class", "subclass", "name", "hash"];

    internal static Result<KeywordGlossary, Errors> ReadGlossary(SourceText file) =>
        YamlReading.LoadDocument(file, "glossary")
            .Bind(root => root.ToMap())
            .Bind(ReadGlossaryMap);

    private static Result<KeywordGlossary, Errors> ReadGlossaryMap(YamlMap map) =>
        Combine(
            map.CheckKeys(GlossaryKeys),
            map.ReadOrDefault(
                "statuses",
                statuses => statuses.ReadEach("status", ReadStatus)
                    .Bind(items => LocatedCollections.ToUniqueDictionary(items, status => status.Id, "status")),
                ImmutableDictionary<StatusId, StatusDefinition>.Empty),
            map.ReadOrDefault(
                "pickups",
                pickups => pickups.ReadEach("pickup", ReadPickup)
                    .Bind(items => LocatedCollections.ToUniqueDictionary(items, pickup => pickup.Id, "pickup")),
                ImmutableDictionary<PickupId, PickupDefinition>.Empty),
            map.ReadOrDefault(
                "summons",
                summons => summons.ReadEach("summon", ReadSummon)
                    .Bind(items => LocatedCollections.ToUniqueDictionary(items, summon => summon.Id, "summon")),
                ImmutableDictionary<SummonId, SummonDefinition>.Empty),
            map.ReadOrDefault("subclasses", subclasses => subclasses.ReadEach("subclass", ReadSubclass).Bind(CheckSubclassHashes), []),
            (_, statuses, pickups, summons, subclasses) => new KeywordGlossary(statuses, pickups, summons, subclasses));

    private static Result<Located<StatusDefinition>, Errors> ReadStatus(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StatusKeys),
            map.ReadRequired("id", ReadStatusId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("kind", ReadVocabularyWord<KeywordKind>),
            map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
            map.ReadOptional("maxStacks", ReadMaxStacks),
            map.ReadOrDefault("duration", ReadDuration, Optional.None<Seconds>()),
            (_, id, name, kind, affinity, maxStacks, duration) =>
                map.ToLocated(new StatusDefinition(id, name, kind, affinity, maxStacks, duration))));

    private static Result<Located<PickupDefinition>, Errors> ReadPickup(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PickupKeys),
            map.ReadRequired("id", ReadPickupId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
            map.ReadRequired("collectsAutomatically", ReadBoolean),
            (_, id, name, affinity, collectsAutomatically) =>
                map.ToLocated(new PickupDefinition(id, name, affinity, collectsAutomatically))));

    private static Result<Located<SummonDefinition>, Errors> ReadSummon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(SummonKeys),
            map.ReadRequired("id", ReadSummonId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("damageType", ReadVocabularyWord<DamageType>),
            (_, id, name, damageType) => map.ToLocated(new SummonDefinition(id, name, damageType))));

    /// <summary><c>{ class: hunter, subclass: arc, name: Arcstrider, hash: 2328211300 }</c> — the hash may be a list of copies.</summary>
    private static Result<Located<SubclassDefinition>, Errors> ReadSubclass(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(SubclassKeys),
            map.ReadRequired("class", ReadVocabularyWord<GuardianClass>),
            map.ReadRequired("subclass", ReadVocabularyWord<Subclass>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("hash", ReadItemHashes),
            (_, guardianClass, subclass, name, hashes) => map.ToLocated(new SubclassDefinition(guardianClass, subclass, name, hashes))));

    /// <summary>A subclass hash names one subclass: a repeat is an error at the repeat.</summary>
    private static Result<ImmutableArray<SubclassDefinition>, Errors> CheckSubclassHashes(ImmutableArray<Located<SubclassDefinition>> subclasses)
    {
        var errors = subclasses
            .SelectMany(subclass => subclass.Value.Hashes.Select(hash => (Hash: hash, Subclass: subclass)))
            .GroupBy(entry => entry.Hash)
            .SelectMany(group => group.Skip(1).Select(repeat => new ParseError(
                repeat.Subclass.Path,
                Optional.Some(repeat.Subclass.Line),
                $"hash {group.Key} of the subclass '{repeat.Subclass.Value.Name}' is already on '{group.First().Subclass.Value.Name}'")))
            .ToImmutableArray();
        return errors.IsEmpty ? Succeed(subclasses.Select(subclass => subclass.Value).ToImmutableArray()) : Fail<ImmutableArray<SubclassDefinition>>(errors);
    }
}
