using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;
using Loopsmith.RuleViewer.Core;
using Loopsmith.RuleViewer.Tests.Support;

namespace Loopsmith.RuleViewer.Tests;

/// <summary>
/// Every claim of a rule file has its e2e rule scenario once the file has one (docs/given-when-then-rule-scenarios.md,
/// "Coverage"): a claim is one of its rule's flows, and a rule scenario names its rule in its Gherkin's
/// <c># Rule &lt;name&gt;:</c> line. A claim the designer can't show is left out by its class's summary
/// (<c>Leaves out &lt;name&gt;: why</c>) and counts as covered. A file with no rule scenario yet is listed, not failed,
/// so the files are covered one pull request at a time.
/// </summary>
public sealed partial class RuleScenarioCoverageTests(ITestOutputHelper output)
{
    private static readonly string RulesFolder = Path.Combine(RepoCatalog.Root, "tests", "Loopsmith.Web.E2E", "Rules");

    private static readonly ImmutableArray<string> ScenarioFiles =
        [.. Directory.EnumerateFiles(RulesFolder, "*Tests.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];

    private static readonly ImmutableDictionary<string, int> Named = CountNames(RuleLine());

    private static readonly ImmutableDictionary<string, int> LeftOut = CountNames(LeftOutLine());

    /// <summary>Each rule file's rules, by full name, with how many claims (flows) each makes.</summary>
    private static readonly ImmutableDictionary<string, ImmutableArray<(string Rule, int Claims)>> RulesByFile =
        RepoCatalog.Catalog.Elements.Values
            .SelectMany(element => ListRuleNames(element).Select(rule => (element.RuleFile, Rule: rule)))
            .Distinct()
            .GroupBy(pair => pair.RuleFile)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.Select(pair => (pair.Rule, Claims: CountClaims(pair.Rule))).OrderBy(pair => pair.Rule, StringComparer.Ordinal).ToImmutableArray());

    public static TheoryData<string> StartedFiles() =>
        [.. RulesByFile.Where(file => file.Value.Any(rule => Named.ContainsKey(rule.Rule))).Select(file => file.Key).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(StartedFiles))]
    public void A_rule_file_with_a_rule_scenario_has_one_for_every_claim(string ruleFile)
    {
        var missing = RulesByFile[ruleFile]
            .Select(rule => (rule.Rule, Missing: rule.Claims - Named.GetValueOrDefault(rule.Rule) - LeftOut.GetValueOrDefault(rule.Rule)))
            .Where(rule => rule.Missing > 0)
            .Select(rule => $"{rule.Rule} ({rule.Missing} of its claims)")
            .ToImmutableArray();

        Assert.True(missing.IsEmpty, $"rules/{ruleFile} has rule scenarios, but none for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void The_rule_files_with_no_rule_scenario_yet_are_listed()
    {
        var unstarted = RulesByFile
            .Where(file => !file.Value.Any(rule => Named.ContainsKey(rule.Rule)))
            .Select(file => $"rules/{file.Key}: {file.Value.Sum(rule => rule.Claims)} claims")
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        output.WriteLine(unstarted.IsEmpty ? "Every rule file has its rule scenarios." : $"No rule scenario yet:\n{string.Join("\n", unstarted)}");

        Assert.All(unstarted, line => Assert.StartsWith("rules/", line));
    }

    [Fact]
    public void Every_rule_a_scenario_names_is_a_rule_in_the_rules_folder()
    {
        var known = RulesByFile.Values.SelectMany(rules => rules.Select(rule => rule.Rule)).ToImmutableHashSet();

        var unknown = Named.Keys.Concat(LeftOut.Keys).Where(name => !known.Contains(name)).Order(StringComparer.Ordinal).ToImmutableArray();

        Assert.True(unknown.IsEmpty, $"Rule scenarios name rules no rule file has: {string.Join(", ", unknown)}");
    }

    private static IEnumerable<string> ListRuleNames(BuildElement element) =>
        element.Rules.Select(rule => element.ToRuleName(rule.Id)).Concat(element.Passives.Select(passive => element.ToRuleName(passive.Id)));

    private static int CountClaims(string ruleName) =>
        RuleViewing.PickRuleFlows(RepoCatalog.Catalog, ruleName).Match(ok => ok.Value.Length, _ => 0);

    private static ImmutableDictionary<string, int> CountNames(Regex line) =>
        ScenarioFiles
            .SelectMany(file => line.Matches(File.ReadAllText(file)).Select(match => match.Groups["name"].Value))
            .GroupBy(name => name, StringComparer.Ordinal)
            .ToImmutableDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    [GeneratedRegex(@"#\s*Rule\s+(?<name>[a-z0-9-]+):")]
    private static partial Regex RuleLine();

    [GeneratedRegex(@"Leaves out\s+(?<name>[a-z0-9-]+):")]
    private static partial Regex LeftOutLine();
}
