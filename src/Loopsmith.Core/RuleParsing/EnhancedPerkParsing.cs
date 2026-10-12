using System.Collections.Immutable;
using Loopsmith.Core.Domain;
using Loopsmith.Core.Functional;
using YamlDotNet.RepresentationModel;
using static Loopsmith.Core.RuleParsing.ResultAccumulation;
using static Loopsmith.Core.RuleParsing.ValueReading;
using Errors = System.Collections.Immutable.ImmutableArray<Loopsmith.Core.RuleParsing.ParseError>;

namespace Loopsmith.Core.RuleParsing;

/// <summary>What an enhanced trait changes: each value of the perk's rules (a duration, a game value) → the enhanced one.</summary>
internal sealed record EnhancedValues(ImmutableDictionary<Seconds, Seconds> Durations, ImmutableDictionary<GameValue, GameValue> Values)
{
    internal static readonly EnhancedValues None = new(ImmutableDictionary<Seconds, Seconds>.Empty, ImmutableDictionary<GameValue, GameValue>.Empty);
}

/// <summary>
/// A weapon perk's <c>enhanced:</c> block — the enhanced trait an enhanced or crafted copy of a weapon rolls, its own plug:
/// <c>{ hash, values: { 8s: 9s }, description }</c>. Its element (<c>&lt;id&gt;-enhanced</c>, "Slice (enhanced)") is the
/// perk with the Compendium row's ↑ figures in place of the base ones: <c>values</c> maps a value the perk's rules use
/// (a duration such as <c>8s</c>, a game value such as <c>0.87%</c>) to the enhanced one; one they don't use is an error,
/// so a typo can't leave the base figure in. Without <c>values</c> the rules are the perk's (the enhanced figures are in
/// its description only). Other plugs of the same enhanced trait are recognised by name (<see cref="EnhancedPerks"/>).
/// </summary>
internal static class EnhancedPerkParsing
{
    private static readonly ImmutableArray<string> EnhancedKeys = ["hash", "values", "description"];

    /// <summary>The element of the perk's enhanced trait, when its YAML has an <c>enhanced:</c> block.</summary>
    internal static Result<Optional<BuildElement>, Errors> ReadEnhanced(YamlMap map, BuildElement perk) =>
        map.ReadOptional("enhanced", value => value.ToMap().Bind(block => ReadBlock(block, perk)));

    private static Result<BuildElement, Errors> ReadBlock(YamlMap block, BuildElement perk) =>
        perk.Kind == ElementKind.WeaponPerk
            ? Combine(
                    block.CheckKeys(EnhancedKeys),
                    block.ReadRequired("hash", ReadItemHashes),
                    block.ReadOrDefault("values", ReadValues, EnhancedValues.None),
                    block.ReadOptional("description", YamlReading.ToText),
                    (_, hashes, values, description) => (Hashes: hashes, Values: values, Description: description))
                .Bind(read => CheckUsed(block, perk, read.Values).Map(_ => ToEnhanced(perk, read.Hashes, read.Values, read.Description)))
            : block.FailAt<BuildElement>($"{block.Label}: only a weaponPerk has an enhanced trait, not a {GameNotationParsing.ToVocabularyWord(perk.Kind)}");

    /// <summary><c>{ 8s: 9s, 0.87%: 1.1% }</c>: each pair two durations, or two game values.</summary>
    private static Result<EnhancedValues, Errors> ReadValues(YamlValue value) =>
        value.ToMap().Bind(map => map.Node.Children
            .Select(entry => ReadPair(map, entry))
            .CollectAll()
            .Map(pairs => new EnhancedValues(
                pairs.SelectMany(pair => pair.Duration.Match(found => new[] { found.Value }, _ => [])).ToImmutableDictionary(pair => pair.Base, pair => pair.Enhanced),
                pairs.SelectMany(pair => pair.Value.Match(found => new[] { found.Value }, _ => [])).ToImmutableDictionary(pair => pair.Base, pair => pair.Enhanced))));

    private sealed record Pair(Optional<(Seconds Base, Seconds Enhanced)> Duration, Optional<(GameValue Base, GameValue Enhanced)> Value);

    private static Result<Pair, Errors> ReadPair(YamlMap map, KeyValuePair<YamlNode, YamlNode> entry)
    {
        var label = entry.Key is YamlScalarNode { Value: { } name } ? $"{map.Label}.{name}" : map.Label;
        var key = new YamlValue(map.Path, entry.Key, label);
        var enhanced = new YamlValue(map.Path, entry.Value, label);
        return key.ToText().Bind(text => text.Trim().EndsWith('s')
            ? Combine(key.ParseWith(ParseSeconds), enhanced.ParseWith(ParseSeconds), (from, to) => new Pair(Optional.Some((from, to)), Optional.None<(GameValue, GameValue)>()))
            : Combine(key.ParseWith(ParseValue), enhanced.ParseWith(ParseValue), (from, to) => new Pair(Optional.None<(Seconds, Seconds)>(), Optional.Some((from, to)))));
    }

    private static Result<Seconds, string> ParseSeconds(string text) =>
        GameNotationParsing.ParseDuration(text).Bind(Result<Seconds, string> (duration) => duration.Match<Result<Seconds, string>>(
            some => new Result<Seconds, string>.Ok(some.Value),
            _ => new Result<Seconds, string>.Error($"'{text}' is no duration to replace (an enhanced figure is known: write it)")));

    /// <summary>A known or approximate game value: "?" isn't one the rules could be replaced by (the figure would be unknown — leave it out).</summary>
    private static Result<GameValue, string> ParseValue(string text) =>
        GameNotationParsing.ParseGameValue(text).Bind(Result<GameValue, string> (value) => value is GameValue.Known or GameValue.Approximate
            ? new Result<GameValue, string>.Ok(value)
            : new Result<GameValue, string>.Error($"'{text}' is no figure to replace: write one value (e.g. 1.1%)"));

    /// <summary>Every value to replace is one the perk's rules use (a typo would leave the base figure in).</summary>
    private static Result<Unit, Errors> CheckUsed(YamlMap block, BuildElement perk, EnhancedValues values)
    {
        var durations = perk.Rules.SelectMany(rule => rule.Then).SelectMany(ListDurations).ToImmutableHashSet();
        var used = perk.Rules.SelectMany(rule => rule.Then).SelectMany(ListValues)
            .Concat(perk.Passives.SelectMany(passive => ListValues(passive.Modifier)))
            .ToImmutableHashSet();
        var unused = values.Durations.Keys.Where(duration => !durations.Contains(duration)).Select(duration => $"{duration.Value}s")
            .Concat(values.Values.Keys.Where(value => !used.Contains(value)).Select(GameValuePhrasing))
            .ToImmutableArray();
        return unused.IsEmpty
            ? Succeed<Unit>(new Unit.Value())
            : block.FailAtKey<Unit>("values", $"{block.Label}.values: {string.Join(", ", unused)} isn't a value of {perk.Id}'s rules");
    }

    private static string GameValuePhrasing(GameValue value) =>
        value.Match(known => $"{known.Value}", _ => "per-copy values", approximate => $"~{approximate.Value}", _ => "?");

    private static BuildElement ToEnhanced(BuildElement perk, ImmutableArray<ItemHash> hashes, EnhancedValues values, Optional<string> description) =>
        perk with
        {
            Id = ElementId.From($"{perk.Id.Value}-enhanced"),
            Name = $"{perk.Name} (enhanced)",
            Hashes = hashes,
            Description = description.Match(own => Optional.Some(own.Value), _ => perk.Description),
            Rules = [.. perk.Rules.Select(rule => rule with { Then = [.. rule.Then.Select(outcome => ReplaceValues(outcome, values))] })],
            Passives = [.. perk.Passives.Select(passive => passive with { Modifier = ReplaceValues(passive.Modifier, values) })],
        };

    // ── the values of an outcome or a passive, and the same with the enhanced figures ──

    private static IEnumerable<Seconds> ListDurations(Outcome outcome) =>
        outcome switch
        {
            Outcome.ApplyBuff buff => ToSequence(buff.Duration),
            Outcome.DebuffTarget debuff => ToSequence(debuff.Duration),
            _ => [],
        };

    private static IEnumerable<GameValue> ListValues(Outcome outcome) =>
        outcome.Match(
            energy => ListValues(energy.Amount),
            convert => [convert.PerStack],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            _ => [],
            damage => [damage.Change],
            health => [health.Amount],
            _ => [],
            _ => [],
            least => ListValues(least.Amount));

    private static IEnumerable<GameValue> ListValues(EnergyGrant grant) =>
        grant.Match(fraction => new[] { fraction.Amount }, _ => []);

    private static IEnumerable<GameValue> ListValues(Passive passive) =>
        passive.Match(
            _ => [],
            _ => [],
            damage => [damage.Change],
            resist => [resist.Amount],
            stats => stats.Changes.Select(change => change.Change),
            raise => [raise.Max],
            stat => [stat.Change]);

    private static Outcome ReplaceValues(Outcome outcome, EnhancedValues values) =>
        outcome.Match<Outcome>(
            energy => energy with { Amount = ReplaceValues(energy.Amount, values) },
            convert => convert with { PerStack = Replace(convert.PerStack, values) },
            buff => buff with { Duration = Replace(buff.Duration, values) },
            remove => remove,
            debuff => debuff with { Duration = Replace(debuff.Duration, values) },
            spawn => spawn,
            summon => summon,
            strike => strike,
            damage => damage with { Change = Replace(damage.Change, values) },
            health => health with { Amount = Replace(health.Amount, values) },
            reset => reset,
            collect => collect,
            least => least with { Amount = ReplaceValues(least.Amount, values) });

    private static EnergyGrant ReplaceValues(EnergyGrant grant, EnhancedValues values) =>
        grant.Match<EnergyGrant>(fraction => fraction with { Amount = Replace(fraction.Amount, values) }, full => full);

    private static Passive ReplaceValues(Passive passive, EnhancedValues values) =>
        passive.Match<Passive>(
            stacks => stacks,
            charges => charges,
            damage => damage with { Change = Replace(damage.Change, values) },
            resist => resist with { Amount = Replace(resist.Amount, values) },
            stats => stats with { Changes = [.. stats.Changes.Select(change => change with { Change = Replace(change.Change, values) })] },
            raise => raise with { Max = Replace(raise.Max, values) },
            stat => stat with { Change = Replace(stat.Change, values) });

    private static Optional<Seconds> Replace(Optional<Seconds> duration, EnhancedValues values) =>
        duration.Map(seconds => values.Durations.TryGetValue(seconds, out var enhanced) ? enhanced : seconds);

    private static GameValue Replace(GameValue value, EnhancedValues values) =>
        values.Values.TryGetValue(value, out var enhanced) ? enhanced : value;

    private static IEnumerable<T> ToSequence<T>(Optional<T> optional) =>
        optional.Match(some => new[] { some.Value }, _ => []);
}
