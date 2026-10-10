using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>
/// What references resolve against. <see cref="Glossary"/> is absent when glossary.yaml is missing
/// or broken: element files are still parsed (so their own errors are reported), but references
/// go unchecked rather than drowning the real problem in "unknown status" noise.
/// </summary>
internal sealed record ReferenceScope(Optional<KeywordGlossary> Glossary);

/// <summary>
/// Ids used in rules, checked against the glossary and against the kind their position needs — and the element ids
/// of <c>doesNotStackWith</c>, checked against the whole catalog.
/// </summary>
internal static class ReferenceChecking
{
    /// <summary>
    /// <c>doesNotStackWith</c> names other elements of the catalog, never the rule's own element, and never leads
    /// back to it (A → B → A, or a longer circle: every rule in it would give way and none would apply). Errors are
    /// reported at the element.
    /// </summary>
    internal static Result<Unit, Errors> CheckStackingReferences(ImmutableArray<Located<BuildElement>> elements)
    {
        var ids = elements.Select(element => element.Value.Id).ToImmutableHashSet();
        var pairs = elements
            .SelectMany(element => element.Value.Rules
                .SelectMany(rule => rule.DoesNotStackWith)
                .Distinct()
                .Select(other => (Element: element, Other: other)))
            .ToImmutableArray();
        var edges = pairs
            .GroupBy(pair => pair.Element.Value.Id)
            .ToImmutableDictionary(group => group.Key, group => group.Select(pair => pair.Other).ToImmutableHashSet());
        var errors = pairs.SelectMany(pair => DescribeStackingProblem(pair.Element, pair.Other, ids, edges)).ToImmutableArray();
        return errors.IsEmpty ? Succeed<Unit>(new Unit.Value()) : Fail<Unit>(errors);
    }

    private static IEnumerable<ParseError> DescribeStackingProblem(
        Located<BuildElement> element,
        ElementId other,
        ImmutableHashSet<ElementId> ids,
        ImmutableDictionary<ElementId, ImmutableHashSet<ElementId>> edges)
    {
        var id = element.Value.Id;
        var problem = (other == id, ids.Contains(other), LeadsTo(edges, [other], [], id)) switch
        {
            (true, _, _) => Optional.Some($"'{id}': doesNotStackWith names its own element"),
            (_, false, _) => Optional.Some($"'{id}': doesNotStackWith '{other}' is not an element of the rules"),
            (_, _, true) => Optional.Some(
                $"'{id}': doesNotStackWith '{other}' leads back to '{id}', so none of those rules would apply; "
                + "say it only on the side that gives nothing"),
            _ => Optional.None<string>(),
        };
        return problem.Match(
            some => new[] { new ParseError(element.Path, Optional.Some(element.Line), some.Value) },
            _ => []);
    }

    /// <summary>Whether following <c>doesNotStackWith</c> from <paramref name="frontier"/> reaches <paramref name="target"/>.</summary>
    private static bool LeadsTo(
        ImmutableDictionary<ElementId, ImmutableHashSet<ElementId>> edges,
        ImmutableHashSet<ElementId> frontier,
        ImmutableHashSet<ElementId> seen,
        ElementId target)
    {
        var next = frontier.SelectMany(id => edges.GetValueOrDefault(id, [])).Except(seen).Except(frontier).ToImmutableHashSet();
        return frontier.Contains(target) || (!next.IsEmpty && LeadsTo(edges, next, seen.Union(frontier), target));
    }

    /// <summary>Any glossary status (keyword damage: <c>strikeTarget.via</c>, <c>keyword:&lt;status&gt;</c>).</summary>
    internal static Result<StatusId, Errors> ReadStatus(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadStatusId(value).Bind(id => scope.CheckStatus(value, id, Optional.None<KeywordKind>()));

    /// <summary>A status that lives on the player (<c>applyBuff</c>, <c>removeBuff</c>, <c>has</c>, <c>lacks</c>…).</summary>
    internal static Result<StatusId, Errors> ReadBuff(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadStatusId(value).Bind(id => scope.CheckStatus(value, id, Optional.Some(KeywordKind.Buff)));

    /// <summary>
    /// A buff that stacks (<c>stacksMaxed</c>, <c>atMax</c>): only a status with <c>maxStacks</c> can be declared at its
    /// maximum (ADRs D3).
    /// </summary>
    internal static Result<StatusId, Errors> ReadStackingBuff(this ReferenceScope scope, YamlValue value) =>
        scope.ReadBuff(value).Bind(id => scope.Glossary.Match(
            glossary => glossary.Value.IsStacking(id)
                ? Succeed(id)
                : value.FailAt<StatusId>($"{value.Label}: '{id}' doesn't stack (no maxStacks), so it is never at max"),
            _ => Succeed(id)));

    /// <summary>A status that lives on the target (<c>debuffTarget</c>, <c>targetHas</c>).</summary>
    internal static Result<StatusId, Errors> ReadDebuff(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadStatusId(value).Bind(id => scope.CheckStatus(value, id, Optional.Some(KeywordKind.Debuff)));

    internal static Result<PickupId, Errors> ReadPickup(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadPickupId(value).Bind(id => scope.Glossary.Match(
            glossary => glossary.Value.Pickups.ContainsKey(id)
                ? Succeed(id)
                : value.FailAt<PickupId>($"{value.Label}: unknown pickup '{id}' (not in glossary.yaml)"),
            _ => Succeed(id)));

    internal static Result<SummonId, Errors> ReadSummon(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadSummonId(value).Bind(id => scope.CheckSummon(value, id));

    /// <summary>A damage source whose <c>keyword:</c> / <c>summon:</c> part names a glossary entry.</summary>
    internal static Result<DamageSource, Errors> ReadDamageSource(this ReferenceScope scope, YamlValue value) =>
        value.ParseWith(GameNotationParsing.ParseDamageSource).Bind(source => source switch
        {
            DamageSource.KeywordOf keyword => scope.CheckStatus(value, keyword.Status, Optional.None<KeywordKind>()).Map(_ => source),
            DamageSource.SummonOf summon => scope.CheckSummon(value, summon.Summon).Map(_ => source),
            _ => Succeed(source),
        });

    private static Result<StatusId, Errors> CheckStatus(
        this ReferenceScope scope,
        YamlValue value,
        StatusId id,
        Optional<KeywordKind> expected) =>
        scope.Glossary.Match(
            glossary => glossary.Value.Statuses.TryGetValue(id, out var definition)
                ? CheckStatusKind(value, id, definition.Kind, expected)
                : value.FailAt<StatusId>($"{value.Label}: unknown status '{id}' (not in glossary.yaml)"),
            _ => Succeed(id));

    private static Result<StatusId, Errors> CheckStatusKind(
        YamlValue value,
        StatusId id,
        KeywordKind actual,
        Optional<KeywordKind> expected) =>
        expected.Match(
            some => some.Value == actual
                ? Succeed(id)
                : value.FailAt<StatusId>(
                    $"{value.Label}: '{id}' is a {GameNotationParsing.ToVocabularyWord(actual)}, "
                    + $"but this position needs a {GameNotationParsing.ToVocabularyWord(some.Value)}"),
            _ => Succeed(id));

    private static Result<SummonId, Errors> CheckSummon(this ReferenceScope scope, YamlValue value, SummonId id) =>
        scope.Glossary.Match(
            glossary => glossary.Value.Summons.ContainsKey(id)
                ? Succeed(id)
                : value.FailAt<SummonId>($"{value.Label}: unknown summon '{id}' (not in glossary.yaml)"),
            _ => Succeed(id));
}
