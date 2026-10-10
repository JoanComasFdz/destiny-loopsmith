using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>Scalar fields shared by the glossary, elements and rule bodies, validated into Domain values.</summary>
internal static class ValueReading
{
    // Valid by construction (1 ≥ 0), so this From cannot throw.
    internal static readonly StackCount OneStack = StackCount.From(1);

    internal static Result<string, Errors> ReadName(YamlValue value) =>
        value.ParseWith<string>(text => string.IsNullOrWhiteSpace(text)
            ? new Result<string, string>.Error("must not be empty")
            : new Result<string, string>.Ok(text));

    internal static Result<ElementId, Errors> ReadElementId(YamlValue value) =>
        value.ParseWith(text => ElementId.TryFrom(text).ToResult());

    internal static Result<StatusId, Errors> ReadStatusId(YamlValue value) =>
        value.ParseWith(text => StatusId.TryFrom(text).ToResult());

    internal static Result<PickupId, Errors> ReadPickupId(YamlValue value) =>
        value.ParseWith(text => PickupId.TryFrom(text).ToResult());

    internal static Result<SummonId, Errors> ReadSummonId(YamlValue value) =>
        value.ParseWith(text => SummonId.TryFrom(text).ToResult());

    internal static Result<ItemHash, Errors> ReadItemHash(YamlValue value) =>
        value.ParseWith(text => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hash)
            ? ItemHash.TryFrom(hash).ToResult()
            : new Result<ItemHash, string>.Error($"'{text}' is not a manifest hash (an unsigned 32-bit number)"));

    /// <summary><c>hash: 1727069364</c>, or a list when copies of the item share its text (<c>hash: [3712696020, 2996369932]</c>).</summary>
    internal static Result<ImmutableArray<ItemHash>, Errors> ReadItemHashes(YamlValue value) =>
        value.Node is YamlSequenceNode
            ? value.ReadEach("hash", ReadItemHash)
            : ReadItemHash(value).Map(hash => ImmutableArray.Create(hash));

    /// <summary>A kebab-case word that is not an id of the glossary (weapon archetypes, weapon stats).</summary>
    internal static Result<string, Errors> ReadSlug(YamlValue value) =>
        value.ParseWith<string>(text => ElementId.IsSlug(text)
            ? new Result<string, string>.Ok(text)
            : new Result<string, string>.Error($"'{text}' is not kebab-case"));

    internal static Result<int, Errors> ReadPositiveInteger(YamlValue value) =>
        value.ParseWith(text => ParseWholeNumber(text, minimum: 1));

    internal static Result<int, Errors> ReadNonNegativeInteger(YamlValue value) =>
        value.ParseWith(text => ParseWholeNumber(text, minimum: 0));

    /// <summary>A stack count of at least one (<c>stacks</c>, <c>extra</c>).</summary>
    internal static Result<StackCount, Errors> ReadStackCount(YamlValue value) =>
        value.ParseWith(text => ParseWholeNumber(text, minimum: 1).Bind(count => StackCount.TryFrom(count).ToResult()));

    /// <summary>A stacking status's cap, at least 2 (<c>maxStacks</c>): a status that doesn't stack has none.</summary>
    internal static Result<StackCount, Errors> ReadMaxStacks(YamlValue value) =>
        value.ParseWith(text => ParseWholeNumber(text, minimum: 2).Bind(count => StackCount.TryFrom(count).ToResult()));

    /// <summary>A number of enemies, 1..20 (<c>atLeast</c>).</summary>
    internal static Result<TargetCount, Errors> ReadTargetCount(YamlValue value) =>
        value.ParseWith(text => ParseWholeNumber(text, minimum: 1).Bind(count => TargetCount.TryFrom(count).ToResult()));

    internal static Result<bool, Errors> ReadBoolean(YamlValue value) =>
        value.ParseWith<bool>(text => text switch
        {
            "true" or "True" or "TRUE" => new Result<bool, string>.Ok(true),
            "false" or "False" or "FALSE" => new Result<bool, string>.Ok(false),
            _ => new Result<bool, string>.Error($"'{text}' is not true or false"),
        });

    internal static Result<Optional<Seconds>, Errors> ReadDuration(YamlValue value) =>
        value.ParseWith(GameNotationParsing.ParseDuration);

    internal static Result<GameValue, Errors> ReadGameValue(YamlValue value) =>
        value.ParseWith(GameNotationParsing.ParseGameValue);

    internal static Result<TEnum, Errors> ReadVocabularyWord<TEnum>(YamlValue value)
        where TEnum : struct, Enum =>
        value.ParseWith(GameNotationParsing.ParseVocabularyWord<TEnum>);

    private static Result<int, string> ParseWholeNumber(string text, int minimum) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum
            ? new Result<int, string>.Ok(number)
            : new Result<int, string>.Error($"'{text}' is not a whole number ≥ {minimum}");
}
