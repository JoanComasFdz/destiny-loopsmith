using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using Loopsmith.Core.Orchestration;
using Loopsmith.Core.SourceFetching;
using Loopsmith.Core.Tests.Support;

namespace Loopsmith.Core.Tests.Orchestration;

/// <summary>
/// The three states of an element in the real rules (docs/rule-format.md, Elements): in the loop (rules or passives) or
/// outside it on purpose (<c>inert</c>, with why). "Not authored yet" is no element at all: the designer marks it ⊘, and
/// the build check says nothing of it. An element with neither is a gap in the rule files, caught here, never shown to
/// the player.
/// </summary>
public sealed class ElementStatesCatalogTests
{
    private static readonly Result<ImmutableArray<SourceText>, string> RuleFiles = FileSourceFetching.ReadRuleFiles(RepoFiles.ToPath("rules"));   // impure

    private static readonly RuleCatalog Catalog =
        RuleFiles.Bind(CatalogLoading.ParseCatalog).Match(ok => ok.Value, error => throw new InvalidOperationException(error.Failure));

    [Fact]
    public void Every_element_has_rules_passives_or_says_why_it_changes_no_loop()
    {
        var silent = Catalog.Elements.Values
            .Where(element => element.Rules.IsEmpty && element.Passives.IsEmpty && !element.Inert.IsSome())
            .Select(element => $"{element.Id.Value} ({element.Name})");

        Assert.Empty(silent);
    }

    [Fact]
    public void Return_Stroke_reloads_Thunderlord_on_each_lightning_strike()
    {
        var returnStroke = Catalog.Elements[ElementId.From("thunderlord-catalyst")];

        Assert.False(returnStroke.Inert.IsSome());
        Assert.Equal(2, returnStroke.Rules.Count(rule => rule.Then.Any(outcome => outcome is Outcome.ReloadWeapon)));
    }
}
