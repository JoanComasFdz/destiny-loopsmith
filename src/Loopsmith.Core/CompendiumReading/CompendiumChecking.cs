using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Loopsmith.Core.Domain;

namespace Loopsmith.Core.CompendiumReading;

/// <summary>One thing the check found wrong: the element, its citation, and what is wrong with it.</summary>
public sealed record CompendiumProblem(ElementId Element, string Citation, string Message);

/// <summary>An element citing a tab no parser reads yet: its citation is not checked.</summary>
public sealed record UncheckedCitation(ElementId Element, string Citation);

/// <summary>
/// What checking the rules against a snapshot found: how many elements and numbers were checked, the problems, and
/// the citations into tabs this slice doesn't read. No problem means every checked element's cited row names it and
/// states every number it takes from its source.
/// </summary>
public sealed record CompendiumCheck(int CheckedElements, int CheckedNumbers, ImmutableArray<CompendiumProblem> Problems, ImmutableArray<UncheckedCitation> NotChecked);

/// <summary>
/// The Compendium check (FR-11): each element citing <c>compendium/&lt;date&gt;/&lt;tab&gt;#&lt;row&gt;</c> must find, in
/// that row of the given snapshot, an entry with its name (or the name the aliases give it), and every number it takes
/// from its source must be one that entry states. The snapshot checked against may be newer than the cited one: a row
/// that moved or a number that changed shows up here, and the rule is re-cited.
/// </summary>
public static partial class CompendiumChecking
{
    public static CompendiumCheck CheckCatalog(RuleCatalog catalog, CompendiumSnapshot snapshot, CompendiumAliases aliases)
    {
        var cited = catalog.Elements.Values
            .SelectMany(element => element.Source is Provenance.Compendium citation
                ? [(Element: element, Citation: citation)]
                : ImmutableArray<(BuildElement Element, Provenance.Compendium Citation)>.Empty)
            .OrderBy(found => found.Element.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        var checkable = cited.Where(found => snapshot.ReadTabs.Contains(found.Citation.Tab)).ToImmutableArray();
        var results = checkable.Select(found => CheckElement(found.Element, found.Citation, snapshot, aliases)).ToImmutableArray();
        return new CompendiumCheck(
            checkable.Length,
            results.Sum(result => result.Numbers),
            [.. results.SelectMany(result => result.Problems), .. CheckAliases(catalog, aliases)],
            [.. cited.Where(found => !snapshot.ReadTabs.Contains(found.Citation.Tab)).Select(found => new UncheckedCitation(found.Element.Id, ToCitation(found.Citation)))]);
    }

    /// <summary>One line per problem and per unchecked citation, for a test's output.</summary>
    public static string DescribeCheck(CompendiumCheck check) =>
        string.Join("\n", [
            $"Checked {check.CheckedElements} elements and {check.CheckedNumbers} numbers: {check.Problems.Length} problem(s).",
            .. check.Problems.Select(problem => $"  {problem.Element.Value} ({problem.Citation}): {problem.Message}"),
            $"Not checked ({check.NotChecked.Length}, no parser for their tab yet):",
            .. check.NotChecked.Select(citation => $"  {citation.Element.Value} ({citation.Citation})")]);

    private static (int Numbers, ImmutableArray<CompendiumProblem> Problems) CheckElement(
        BuildElement element, Provenance.Compendium citation, CompendiumSnapshot snapshot, CompendiumAliases aliases)
    {
        var at = ToCitation(citation);
        var inRow = snapshot.Entries.Where(entry => entry.Tab == citation.Tab && entry.Rows.Contains(citation.Row)).ToImmutableArray();
        var names = ListCandidateNames(element, citation.Tab, aliases);
        var matched = inRow.Where(entry => names.Contains(NormalizeName(entry.Name))).ToImmutableArray();
        var numbers = ElementNumberReading.ReadNumbers(element);
        return (inRow.IsEmpty, matched.IsEmpty) switch
        {
            (true, _) => (0, [new CompendiumProblem(element.Id, at, $"row {citation.Row} of {citation.Tab} holds no entry")]),
            (_, true) => (0, [new CompendiumProblem(element.Id, at,
                $"row {citation.Row} of {citation.Tab} names {string.Join(", ", inRow.Select(entry => $"'{entry.Name}'"))}, not '{element.Name}' (add an alias if it is one of them)")]),
            _ => (numbers.Length, [.. CheckNumbers(element, at, matched[0], numbers, aliases)]),
        };
    }

    private static IEnumerable<CompendiumProblem> CheckNumbers(
        BuildElement element, string at, CompendiumEntry entry, ImmutableArray<StatedNumber> numbers, CompendiumAliases aliases)
    {
        var worded = aliases.Worded.Where(each => each.Element == element.Id).Select(each => each.Value).ToImmutableHashSet();
        return numbers
            .Where(number => !worded.Contains(number.Value))
            .Where(number => !(number.IsPvp ? entry.Numbers.All : entry.Numbers.Pve).Contains(number.Value))
            .Select(number => new CompendiumProblem(element.Id, at,
                $"{number.Label} {number.Value.ToString(CultureInfo.InvariantCulture)} is not a number '{entry.Name}' states{(number.IsPvp ? " (PvP)" : "")}"));
    }

    /// <summary>An alias for an element the rules don't have, or a worded number the element doesn't hold, is stale.</summary>
    private static IEnumerable<CompendiumProblem> CheckAliases(RuleCatalog catalog, CompendiumAliases aliases) =>
        [.. aliases.Names.Where(alias => !catalog.Elements.ContainsKey(alias.Element))
                .Select(alias => new CompendiumProblem(alias.Element, "aliases", "names: no element has this id")),
            .. aliases.Worded.Where(worded => !catalog.Elements.TryGetValue(worded.Element, out var element)
                    || !ElementNumberReading.ReadNumbers(element).Any(number => number.Value == worded.Value))
                .Select(worded => new CompendiumProblem(worded.Element, "aliases", $"worded: the element holds no {worded.Value.ToString(CultureInfo.InvariantCulture)}"))];

    /// <summary>The element's name, with each of its tab's prefixes, suffixes and dropped words taken off, and its alias.</summary>
    private static ImmutableHashSet<string> ListCandidateNames(BuildElement element, string tab, CompendiumAliases aliases)
    {
        var name = NormalizeName(element.Name);
        var prefixes = aliases.Prefixes.Where(affix => affix.Tab == tab).Select(affix => NormalizeName(affix.Text)).ToImmutableArray();
        var suffixes = aliases.Suffixes.Where(affix => affix.Tab == tab).Select(affix => NormalizeName(affix.Text)).ToImmutableArray();
        var dropped = aliases.DroppedWords.Where(words => words.Tab == tab).SelectMany(words => words.Words.Select(NormalizeName)).ToImmutableHashSet();
        var withoutPrefix = prefixes.Aggregate(name, (current, prefix) => current.StartsWith(prefix + " ", StringComparison.Ordinal) ? current[(prefix.Length + 1)..] : current);
        var stripped = suffixes.Aggregate(withoutPrefix, (current, suffix) => current.EndsWith(" " + suffix, StringComparison.Ordinal) ? current[..^(suffix.Length + 1)] : current);
        var withoutWords = string.Join(" ", stripped.Split(' ').Where(word => !dropped.Contains(word)));
        return [name, withoutPrefix, stripped, withoutWords, .. aliases.Names.Where(alias => alias.Element == element.Id).Select(alias => NormalizeName(alias.Name))];
    }

    /// <summary>Case, curly apostrophes and spacing don't matter: "Mothkeeper’s  Wraps" is "mothkeeper's wraps".</summary>
    public static string NormalizeName(string name) =>
        Spaces().Replace(name.Replace('’', '\'').Replace('‘', '\'').Trim().ToLowerInvariant(), " ");

    private static string ToCitation(Provenance.Compendium citation) =>
        $"compendium/{citation.Snapshot.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}/{citation.Tab}#{citation.Row}";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
