using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary><c>rules/glossary.yaml</c>: the keyword vocabulary (statuses, pickups, summons).</summary>
internal static class GlossaryParsing
{
    internal const string GlossaryPath = "glossary.yaml";

    private static readonly ImmutableArray<string> GlossaryKeys = ["statuses", "pickups", "summons"];
    private static readonly ImmutableArray<string> StatusKeys = ["id", "name", "kind", "affinity", "maxStacks", "duration"];
    private static readonly ImmutableArray<string> PickupKeys = ["id", "name", "affinity", "collectsAutomatically"];
    private static readonly ImmutableArray<string> SummonKeys = ["id", "name", "damageType"];

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
            (_, statuses, pickups, summons) => new KeywordGlossary(statuses, pickups, summons));

    private static Result<Located<StatusDefinition>, Errors> ReadStatus(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StatusKeys),
            map.ReadRequired("id", ReadStatusId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("kind", ReadVocabularyWord<KeywordKind>),
            map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
            map.ReadOptional("maxStacks", ReadStackCount),
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
}
