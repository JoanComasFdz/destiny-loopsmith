using System.Collections.Immutable;
using System.Globalization;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>A rule's or a passive's <c>id</c> as written, at its node.</summary>
internal sealed record WrittenRuleId(string Id, YamlValue Value);

/// <summary>
/// Every rule and passive of an element has an id of its own (ADRs D40): the element's id, a dash and a number from 1
/// (<c>combination-blow-2</c>), one numbering across its rules, its passives and its enhanced trait's rules. A number
/// isn't reused, so a gap is fine (a rule taken out leaves its number unused) and a repeat is an error.
/// </summary>
internal static class RuleIdChecking
{
    internal static Result<Unit, Errors> CheckRuleIds(YamlMap element, ElementId owner)
    {
        var written = ListWrittenIds(element);
        var malformed = written
            .Where(rule => !IsOwnedBy(rule.Id, owner))
            .SelectMany(rule => rule.Value.ToErrors(
                $"{rule.Value.Label}: '{rule.Id}' is not {owner}-<number> (its element's id, a dash and a number from 1: {owner}-1)"));
        var repeated = written
            .GroupBy(rule => rule.Id)
            .Where(same => same.Count() > 1)
            .SelectMany(same => same.Skip(1).First().Value.ToErrors(
                $"{same.Skip(1).First().Value.Label}: '{same.Key}' is given twice in '{owner}': each of its rules and passives has a number of its own"));
        return FailIfAny([.. malformed.Concat(repeated)]);
    }

    /// <summary>The ids written under <c>rules</c>, <c>passives</c> and <c>enhanced.rules</c>; a missing one is ReadRule's error.</summary>
    private static ImmutableArray<WrittenRuleId> ListWrittenIds(YamlMap element) =>
    [
        .. ListListedIds(element, "rules", "rule"),
        .. ListListedIds(element, "passives", "passive"),
        .. element.FindValue("enhanced")
            .Bind(enhanced => enhanced.Node is YamlMappingNode block ? Optional.Some(new YamlMap(enhanced.Path, block, enhanced.Label)) : Optional.None<YamlMap>())
            .Match(block => ListListedIds(block.Value, "rules", "enhanced rule"), _ => []),
    ];

    private static IEnumerable<WrittenRuleId> ListListedIds(YamlMap map, string key, string label) =>
        map.FindValue(key).Match(
            list => list.Value.Node is YamlSequenceNode items
                ? items.Children.OfType<YamlMappingNode>().SelectMany(item => ListItemId(map.Path, item, label))
                : [],
            _ => []);

    private static IEnumerable<WrittenRuleId> ListItemId(string path, YamlMappingNode item, string label) =>
        item.Children
            .Where(entry => entry.Key is YamlScalarNode { Value: "id" } && entry.Value is YamlScalarNode { Value: not null })
            .Select(entry => new WrittenRuleId(((YamlScalarNode)entry.Value).Value!, new YamlValue(path, entry.Value, $"{label}.id")));

    private static bool IsOwnedBy(string id, ElementId owner) =>
        id.StartsWith($"{owner.Value}-", StringComparison.Ordinal)
        && IsNumber(id[(owner.Value.Length + 1)..]);

    private static bool IsNumber(string text) =>
        text.Length > 0
        && text[0] != '0'
        && text.All(char.IsAsciiDigit)
        && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
