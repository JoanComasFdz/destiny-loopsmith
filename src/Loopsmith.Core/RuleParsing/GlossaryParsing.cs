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

    private static readonly ImmutableArray<string> GlossaryKeys = ["statuses", "pickups", "summons", "subclasses", "finishers"];
    private static readonly ImmutableArray<string> StatusKeys = ["id", "name", "kind", "affinity", "maxStacks", "duration", "trait", "drawnIcon", "counted", "decay", "adjective"];
    private static readonly ImmutableArray<string> PickupKeys = ["id", "name", "affinity", "collectsAutomatically", "trait"];
    private static readonly ImmutableArray<string> SummonKeys = ["id", "name", "damageType", "trait"];
    private static readonly ImmutableArray<string> SubclassKeys = ["class", "subclass", "name", "hash"];
    private static readonly ImmutableArray<string> FinisherKeys = ["class", "name", "hash"];

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
            map.ReadOrDefault("finishers", finishers => finishers.ReadEach("finisher", ReadFinisher).Bind(CheckFinisherClasses), []),
            (_, statuses, pickups, summons, subclasses, finishers) => new KeywordGlossary(statuses, pickups, summons, subclasses, finishers));

    private static Result<Located<StatusDefinition>, Errors> ReadStatus(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(StatusKeys),
            map.ReadRequired("id", ReadStatusId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("kind", ReadVocabularyWord<KeywordKind>),
            map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
            map.ReadOptional("maxStacks", ReadMaxStacks),
            map.ReadOrDefault("duration", ReadDuration, Optional.None<Seconds>()),
            map.ReadOptional("trait", ReadItemHash),
            map.ReadOptional("drawnIcon", ReadVocabularyWord<DrawnIcon>),
            map.ReadOrDefault("counted", ReadBoolean, false),
            map.ReadOptional("decay", YamlReading.ToText),
            map.ReadOptional("adjective", ReadName),
            (_, id, name, kind, affinity, maxStacks, duration, trait, drawnIcon, counted, decay, adjective) =>
                new StatusDefinition(id, name, kind, affinity, maxStacks, duration, trait, drawnIcon, counted, decay, adjective))
            .Bind(status => CheckDrawnIcon(map, status))
            .Bind(status => CheckAdjective(map, status))
            .Bind(status => CheckCounted(map, status))
            .Map(map.ToLocated));

    /// <summary>Only a buff that stacks is counted (ADRs D33): its stacks go up to its <c>maxStacks</c>.</summary>
    private static Result<StatusDefinition, Errors> CheckCounted(YamlMap map, StatusDefinition status) =>
        status.Counted && !(status.MaxStacks.IsSome() && status.Kind.IsBuff())
            ? map.FailAtKey<StatusDefinition>("counted", $"the status '{status.Id.Value}' is counted, so it must be a buff with maxStacks")
            : Succeed(status);

    /// <summary>An adjective names a target that has the status ("Frozen"): only a debuff is on a target.</summary>
    private static Result<StatusDefinition, Errors> CheckAdjective(YamlMap map, StatusDefinition status) =>
        status.Adjective.IsSome() && status.Kind != KeywordKind.Debuff
            ? map.FailAtKey<StatusDefinition>("adjective", $"the status '{status.Id.Value}' isn't a debuff, so no target has it: an adjective is for a debuff")
            : Succeed(status);

    /// <summary>A drawn icon is for a keyword Bungie's data has no icon for: a status with a trait has the game's own.</summary>
    private static Result<StatusDefinition, Errors> CheckDrawnIcon(YamlMap map, StatusDefinition status) =>
        status.DrawnIcon.IsSome() && status.Trait.IsSome()
            ? map.FailAtKey<StatusDefinition>("drawnIcon", $"the status '{status.Id.Value}' has a trait, so the game's own icon: a drawn one is for a keyword Bungie's data has no icon for")
            : Succeed(status);

    private static Result<Located<PickupDefinition>, Errors> ReadPickup(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(PickupKeys),
            map.ReadRequired("id", ReadPickupId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("affinity", ReadVocabularyWord<Affinity>),
            map.ReadRequired("collectsAutomatically", ReadBoolean),
            map.ReadOptional("trait", ReadItemHash),
            (_, id, name, affinity, collectsAutomatically, trait) =>
                map.ToLocated(new PickupDefinition(id, name, affinity, collectsAutomatically, trait))));

    private static Result<Located<SummonDefinition>, Errors> ReadSummon(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(SummonKeys),
            map.ReadRequired("id", ReadSummonId),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("damageType", ReadVocabularyWord<DamageType>),
            map.ReadOptional("trait", ReadItemHash),
            (_, id, name, damageType, trait) => map.ToLocated(new SummonDefinition(id, name, damageType, trait))));

    /// <summary><c>{ class: hunter, subclass: arc, name: Arcstrider, hash: 2328211300 }</c> — the hash may be a list of copies.</summary>
    private static Result<Located<SubclassDefinition>, Errors> ReadSubclass(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(SubclassKeys),
            map.ReadRequired("class", ReadVocabularyWord<GuardianClass>),
            map.ReadRequired("subclass", ReadVocabularyWord<Subclass>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("hash", ReadItemHashes),
            (_, guardianClass, subclass, name, hashes) => map.ToLocated(new SubclassDefinition(guardianClass, subclass, name, hashes))));

    /// <summary><c>{ class: hunter, name: Blade Carnival, hash: 4012203003 }</c>: a class's own finisher.</summary>
    private static Result<Located<FinisherDefinition>, Errors> ReadFinisher(YamlValue value) =>
        value.ToMap().Bind(map => Combine(
            map.CheckKeys(FinisherKeys),
            map.ReadRequired("class", ReadVocabularyWord<GuardianClass>),
            map.ReadRequired("name", ReadName),
            map.ReadRequired("hash", ReadItemHash),
            (_, guardianClass, name, hash) => map.ToLocated(new FinisherDefinition(guardianClass, name, hash))));

    /// <summary>A class has one finisher of its own: a second for it is an error at the repeat.</summary>
    private static Result<ImmutableArray<FinisherDefinition>, Errors> CheckFinisherClasses(ImmutableArray<Located<FinisherDefinition>> finishers)
    {
        var errors = finishers
            .GroupBy(finisher => finisher.Value.Class)
            .SelectMany(group => group.Skip(1).Select(repeat => new ParseError(
                repeat.Path,
                Optional.Some(repeat.Line),
                $"the {group.Key.ToString().ToLowerInvariant()} already has its finisher, '{group.First().Value.Name}'")))
            .ToImmutableArray();
        return errors.IsEmpty ? Succeed(finishers.Select(finisher => finisher.Value).ToImmutableArray()) : Fail<ImmutableArray<FinisherDefinition>>(errors);
    }

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
