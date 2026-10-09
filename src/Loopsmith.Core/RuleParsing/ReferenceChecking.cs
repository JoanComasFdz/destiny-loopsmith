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

/// <summary>Ids used in rules, checked against the glossary and against the kind their position needs.</summary>
internal static class ReferenceChecking
{
    /// <summary>Any glossary status (keyword damage: <c>strikeTarget.via</c>, <c>keyword:&lt;status&gt;</c>).</summary>
    internal static Result<StatusId, Errors> ReadStatus(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadStatusId(value).Bind(id => scope.CheckStatus(value, id, Optional.None<KeywordKind>()));

    /// <summary>A status that lives on the player (<c>applyBuff</c>, <c>removeBuff</c>, <c>has</c>, <c>lacks</c>…).</summary>
    internal static Result<StatusId, Errors> ReadBuff(this ReferenceScope scope, YamlValue value) =>
        ValueReading.ReadStatusId(value).Bind(id => scope.CheckStatus(value, id, Optional.Some(KeywordKind.Buff)));

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
