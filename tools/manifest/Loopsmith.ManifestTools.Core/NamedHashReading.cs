using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Loopsmith.Functional;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Loopsmith.ManifestTools.Core;

/// <summary>
/// The hashes Loopsmith names: each <c>hash:</c> in rules/ (<see cref="RuleHashes"/>: elements, the glossary's subclasses,
/// armor set perks), each <c>trait:</c> in rules/ (<see cref="RuleTraits"/>: the glossary's keywords' traits) and every hash
/// tools/manifest/test-items.yaml lists (<see cref="TestItemHashes"/>: the weapons, armor and mods the Core tests equip
/// that no rule names).
/// </summary>
public sealed record NamedHashes(ImmutableHashSet<long> RuleHashes, ImmutableHashSet<long> RuleTraits, ImmutableHashSet<long> TestItemHashes);

/// <summary>The rule files' texts and test-items.yaml → the hashes they name.</summary>
public static partial class NamedHashReading
{
    /// <summary>
    /// Whether a file under the repository (its path from the root, with <c>/</c>) is a rule file the hashes are read from:
    /// a <c>.yaml</c> file under rules/, but the manifest excerpt itself.
    /// </summary>
    public static bool IsRuleSource(string path) =>
        path.Split('/') is [ "rules", .., var name ] parts
        && name.EndsWith(".yaml", StringComparison.Ordinal)
        && !(name == "manifest.yaml" && parts[^2] == "rules");

    public static Result<NamedHashes, string> ReadNamedHashes(IEnumerable<string> ruleTexts, string testItemsYaml)
    {
        ImmutableArray<string> texts = [.. ruleTexts];
        return ReadTestItemHashes(testItemsYaml).Map(testItems => new NamedHashes(
            [.. texts.SelectMany(ReadRuleHashes)], [.. texts.SelectMany(ReadRuleTraits)], testItems));
    }

    /// <summary>Every number after <c>hash:</c> (one, or a [list]).</summary>
    public static IEnumerable<long> ReadRuleHashes(string text) =>
        FindHashes().Matches(text).SelectMany(match => FindNumbers().Matches(match.Groups[1].Value).Select(number => ParseNumber(number.Value)));

    /// <summary>Every number after <c>trait:</c>.</summary>
    public static IEnumerable<long> ReadRuleTraits(string text) =>
        FindTraits().Matches(text).Select(match => ParseNumber(match.Groups[1].Value));

    /// <summary>The <c>hash</c> of each entry of test-items.yaml (a list of <c>{ hash, name, why }</c>; empty when it lists none).</summary>
    public static Result<ImmutableHashSet<long>, string> ReadTestItemHashes(string yaml) =>
        ParseYaml(yaml).Bind(items => items
            .Select(ReadTestItemHash)
            .CombineAll()
            .Map(hashes => hashes.ToImmutableHashSet())
            .MapError(failures => $"tools/manifest/test-items.yaml: {failures[0]}"));

    private static Result<long, string> ReadTestItemHash(Dictionary<string, string> item) =>
        item.TryGetValue("hash", out var text) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? new Result<long, string>.Ok(number)
            : new Result<long, string>.Error($"an item lacks its hash, a number ({string.Join(", ", item.Select(pair => $"{pair.Key}: {pair.Value}"))})");

    /// <summary>The YAML's list of items (empty for an empty text); an error for a text that isn't such a list.</summary>
    private static Result<ImmutableArray<Dictionary<string, string>>, string> ParseYaml(string yaml)
    {
        try
        {
            var items = new DeserializerBuilder().Build().Deserialize<List<Dictionary<string, string>>?>(yaml);
            return new Result<ImmutableArray<Dictionary<string, string>>, string>.Ok([.. items ?? []]);
        }
        catch (YamlException exception)
        {
            return new Result<ImmutableArray<Dictionary<string, string>>, string>.Error($"tools/manifest/test-items.yaml isn't a list of items: {exception.Message}");
        }
    }

    private static long ParseNumber(string digits) => long.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"hash:\s*(\[[^\]]*\]|[0-9]+)")]
    private static partial Regex FindHashes();

    [GeneratedRegex("[0-9]+")]
    private static partial Regex FindNumbers();

    [GeneratedRegex(@"trait:\s*([0-9]+)")]
    private static partial Regex FindTraits();
}
